using System.Text.Json;

namespace Veriqo.Api.Services;

public class ForensicsResult
{
    public bool ModelLoaded { get; set; }
    public double TamperProbability { get; set; }
    public double? CnnProbability { get; set; }
    public string Label { get; set; } = string.Empty;
    public Dictionary<string, JsonElement>? Signals { get; set; }
}

/// <summary>
/// Calls the Python forensics microservice (ELA + copy-move + a trained tamper CNN).
/// Fails soft — if the service is down the document pipeline continues without it.
/// </summary>
public class ForensicsClient
{
    private readonly HttpClient _http;
    private readonly ILogger<ForensicsClient> _log;
    private readonly string _baseUrl;

    public ForensicsClient(HttpClient http, IConfiguration config, ILogger<ForensicsClient> log)
    {
        _http = http;
        _log = log;
        _baseUrl = config["Forensics:Url"] ?? "http://localhost:8000";
        _http.Timeout = TimeSpan.FromSeconds(12);
    }

    public async Task<ForensicsResult?> AnalyzeAsync(byte[] bytes, string contentType, string fileName)
    {
        try
        {
            using var form = new MultipartFormDataContent();
            var file = new ByteArrayContent(bytes);
            file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            form.Add(file, "file", fileName);

            var resp = await _http.PostAsync($"{_baseUrl}/analyze", form);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ForensicsResult>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Forensics service unavailable — skipping ML tamper analysis.");
            return null;
        }
    }
}
