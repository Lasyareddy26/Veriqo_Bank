using System.Text.Json;
using Veriqo.Api.Data;
using Veriqo.Api.Models;
using Veriqo.Api.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Veriqo.Api.Services;

public class RegistryResult
{
    public bool Available { get; set; }      // was a registry actually consulted
    public bool Exists { get; set; }
    public bool NameMatch { get; set; }
    public bool DobMatch { get; set; }
    public string Provider { get; set; } = "mock";
    public string Detail { get; set; } = string.Empty;
}

/// <summary>
/// Verifies an ID against the official issuer registry (DigiLocker-style existence check).
/// Uses a real government/KYC API when configured; otherwise a seeded local registry that
/// simulates the same contract so the flow works end-to-end in development.
/// </summary>
public class IdentityVerifier
{
    private readonly MongoDbContext _db;
    private readonly HttpClient _http;
    private readonly RegistrySettings _cfg;
    private readonly ILogger<IdentityVerifier> _log;

    public IdentityVerifier(MongoDbContext db, HttpClient http, IOptions<RegistrySettings> cfg, ILogger<IdentityVerifier> log)
    {
        _db = db;
        _http = http;
        _cfg = cfg.Value;
        _log = log;
        _http.Timeout = TimeSpan.FromSeconds(12);
    }

    public async Task<RegistryResult> VerifyIdAsync(string idNumber, string fullName, DateTime? dob)
    {
        if (string.IsNullOrWhiteSpace(idNumber)) return new RegistryResult { Available = false };
        return _cfg.RealConfigured
            ? await VerifyRealAsync(idNumber, fullName, dob)
            : await VerifyMockAsync(idNumber, fullName, dob);
    }

    // ---- Seeded local registry (development / demo) ----
    private async Task<RegistryResult> VerifyMockAsync(string idNumber, string fullName, DateTime? dob)
    {
        var norm = Normalize(idNumber);
        var rec = await _db.Registry.Find(r => r.IdNumber == norm).FirstOrDefaultAsync();
        if (rec == null)
            return new RegistryResult
            {
                Available = true, Exists = false, Provider = "mock",
                Detail = $"ID {idNumber} was not found in the official registry."
            };

        var nameMatch = NameMatches(rec.FullName, fullName);
        var dobMatch = !dob.HasValue || !rec.DateOfBirth.HasValue || rec.DateOfBirth.Value.Date == dob.Value.Date;
        return new RegistryResult
        {
            Available = true, Exists = true, NameMatch = nameMatch, DobMatch = dobMatch, Provider = "mock",
            Detail = nameMatch && dobMatch
                ? "ID found in the official registry and the holder's details match."
                : "ID exists but the registered holder's details do not match this profile."
        };
    }

    // ---- Real provider (DigiLocker / KYC aggregator) ----
    private async Task<RegistryResult> VerifyRealAsync(string idNumber, string fullName, DateTime? dob)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{_cfg.BaseUrl.TrimEnd('/')}/verify");
            req.Headers.Add("x-api-key", _cfg.ApiKey);
            if (!string.IsNullOrEmpty(_cfg.ApiSecret)) req.Headers.Add("x-api-secret", _cfg.ApiSecret);
            req.Content = JsonContent(new { idNumber, name = fullName, dob = dob?.ToString("yyyy-MM-dd") });

            var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
                return new RegistryResult { Available = false, Provider = _cfg.Provider, Detail = "Registry API error." };

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            bool exists = root.TryGetProperty("exists", out var e) && e.GetBoolean();
            bool name = root.TryGetProperty("nameMatch", out var n) && n.GetBoolean();
            bool d = root.TryGetProperty("dobMatch", out var db) && db.GetBoolean();
            return new RegistryResult
            {
                Available = true, Exists = exists, NameMatch = name, DobMatch = d, Provider = _cfg.Provider,
                Detail = exists ? "Verified against the official registry." : "Not found in the official registry."
            };
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Registry provider {Provider} unavailable.", _cfg.Provider);
            return new RegistryResult { Available = false, Provider = _cfg.Provider, Detail = "Registry unavailable." };
        }
    }

    private static HttpContent JsonContent(object o) =>
        new StringContent(JsonSerializer.Serialize(o), System.Text.Encoding.UTF8, "application/json");

    private static string Normalize(string s) =>
        new string(s.ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

    private static bool NameMatches(string a, string b)
    {
        var at = a.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var bt = b.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(t => t.Length > 2).ToList();
        if (bt.Count == 0) return false;
        return bt.Count(at.Contains) >= Math.Ceiling(bt.Count / 2.0);
    }
}
