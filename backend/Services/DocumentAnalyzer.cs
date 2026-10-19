using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Veriqo.Api.Data;
using Veriqo.Api.Models;
using MetadataExtractor;
using MongoDB.Driver;
using Directory = MetadataExtractor.Directory;

namespace Veriqo.Api.Services;

public class AnalysisResult
{
    public string Sha256 { get; set; } = string.Empty;
    public Dictionary<string, object> ExtractedData { get; set; } = new();
    public List<string> RiskFlags { get; set; } = new();
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;
    public int RiskScore { get; set; }
    public string OcrPreview { get; set; } = string.Empty;
}

/// <summary>
/// Robust KYC verification pipeline. For each uploaded document it:
///   1. hashes the bytes and detects duplicates across all accounts,
///   2. inspects image metadata for signs of editing/forgery,
///   3. runs OCR and scores text confidence,
///   4. confirms the file actually looks like the document type claimed,
///   5. extracts fields and cross-checks them against the customer's profile
///      (name, DOB, ID number, expiry, address recency, income),
///   6. produces an explainable verdict + risk score.
/// It never auto-approves/rejects — it arms a human reviewer with evidence.
/// </summary>
public class DocumentAnalyzer
{
    private readonly IOcrEngine _ocr;
    private readonly MongoDbContext _db;
    private readonly ForensicsClient _forensics;
    private readonly IdentityVerifier _registry;
    private readonly ILogger<DocumentAnalyzer> _log;

    public DocumentAnalyzer(IOcrEngine ocr, MongoDbContext db, ForensicsClient forensics,
        IdentityVerifier registry, ILogger<DocumentAnalyzer> log)
    {
        _ocr = ocr;
        _db = db;
        _forensics = forensics;
        _registry = registry;
        _log = log;
    }

    private static readonly Dictionary<DocumentType, string[]> ExpectedKeywords = new()
    {
        [DocumentType.GovernmentId] = new[] { "government", "identity", "passport", "licence", "license", "national", "republic", "aadhaar", "voter", "pan", "id no", "uid", "date of birth", "dob" },
        [DocumentType.AddressProof] = new[] { "electricity", "utility", "bill", "statement", "address", "gas", "water", "telephone", "broadband", "rental", "lease", "municipal", "consumer" },
        [DocumentType.SalarySlip] = new[] { "salary", "payslip", "pay slip", "net pay", "gross", "earnings", "employee", "ctc", "basic", "deductions" },
        [DocumentType.BankStatement] = new[] { "statement", "balance", "transaction", "account", "debit", "credit", "ifsc", "opening balance", "closing balance" },
        [DocumentType.EmploymentLetter] = new[] { "employment", "offer", "appointment", "hereby", "designation", "joining", "employer", "confirm", "salary" },
    };

    private static readonly string[] EditingSoftware =
        { "photoshop", "gimp", "canva", "pixlr", "paint.net", "figma", "snapseed", "lightroom", "illustrator", "coreldraw", "inkscape", "affinity" };

    public async Task<AnalysisResult> AnalyzeAsync(
        string filePath, byte[] bytes, string contentType,
        DocumentType type, CustomerProfile profile, decimal declaredIncome)
    {
        var r = new AnalysisResult { Sha256 = Sha256(bytes) };
        var checks = new List<Dictionary<string, string>>();
        int score = 0;

        void Check(string label, string status, string detail, int penalty)
        {
            checks.Add(new() { ["label"] = label, ["status"] = status, ["detail"] = detail });
            if (status != "pass") { score += penalty; if (!string.IsNullOrEmpty(detail)) r.RiskFlags.Add(detail); }
        }

        // 1. Duplicate detection
        var dup = await _db.Documents.Find(d => d.Sha256 == r.Sha256).FirstOrDefaultAsync();
        if (dup != null)
        {
            var own = dup.UserId == profile.UserId;
            Check("Duplicate check", own ? "warn" : "fail",
                own ? "This exact file was already uploaded on your account."
                    : "This file is identical to a document from another account.",
                own ? 15 : 45);
        }
        else Check("Duplicate check", "pass", "", 0);

        // 2. Metadata / tamper inspection (EXIF)
        var editor = DetectEditingSoftware(filePath);
        if (editor != null)
            Check("Metadata inspection", "fail", $"Image metadata shows it was last saved in {editor} — possible editing.", 40);
        else
            Check("Metadata inspection", "pass", "", 0);

        // 2b. ML forensic tamper analysis (Python microservice: ELA + copy-move + CNN)
        var forensics = await _forensics.AnalyzeAsync(bytes, contentType, "document");
        if (forensics != null)
        {
            r.ExtractedData["forensicTamperProbability"] = forensics.TamperProbability;
            r.ExtractedData["forensicLabel"] = forensics.Label;
            // Supporting signal only — low weight, warn-only — so a model miss never on its own
            // flips a genuine document to fraud. The registry + OCR rules are authoritative.
            if (forensics.Label == "likely_tampered")
                Check("Forensic analysis (ML)", "warn", $"Tamper-detection model suggests possible editing (p={forensics.TamperProbability:P0}). Supporting signal only.", 15);
            else if (forensics.Label == "suspicious")
                Check("Forensic analysis (ML)", "warn", $"Tamper-detection model is unsure (p={forensics.TamperProbability:P0}). Supporting signal only.", 8);
            else
                Check("Forensic analysis (ML)", "pass", $"No tampering detected by the model (p={forensics.TamperProbability:P0}).", 0);
        }

        // 3. OCR + confidence
        var ocr = await _ocr.ExtractAsync(filePath, contentType);
        var text = ocr.Text ?? string.Empty;
        r.OcrPreview = text.Length > 700 ? text[..700] : text;
        r.ExtractedData["ocrEngine"] = ocr.Engine;
        r.ExtractedData["ocrConfidence"] = ocr.Confidence;
        r.ExtractedData["ocrWordCount"] = ocr.WordCount;

        if (!ocr.Success || text.Length < 15 || ocr.WordCount < 8)
        {
            Check("Text readability", "fail", "Too little readable text — the scan may be blank, blurry or not a document.", 35);
            return Finish(r, checks, score);
        }
        if (ocr.Confidence < 55)
            Check("Text readability", "warn", $"Low OCR confidence ({ocr.Confidence:F0}%) — image quality is poor.", 12);
        else
            Check("Text readability", "pass", $"OCR confidence {ocr.Confidence:F0}%.", 0);

        var lower = text.ToLowerInvariant();
        var normalized = Normalize(text);

        // 4. Authenticity — does it look like the claimed document type?
        var keywords = ExpectedKeywords.GetValueOrDefault(type, Array.Empty<string>());
        var hits = keywords.Count(k => lower.Contains(k));
        if (hits == 0)
            Check("Document type", "fail", $"This file doesn't read like a genuine {Pretty(type)} (expected markers not found).", 35);
        else
            Check("Document type", "pass", $"Recognised as a {Pretty(type)} ({hits} markers).", 0);

        // 5. Name match (shared across types)
        if (!string.IsNullOrWhiteSpace(profile.FullName))
        {
            var tokens = profile.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length > 2).Select(Normalize).ToList();
            var matched = tokens.Count(t => normalized.Contains(t));
            var ratio = tokens.Count == 0 ? 0 : (double)matched / tokens.Count;
            r.ExtractedData["nameMatchRatio"] = Math.Round(ratio, 2);
            if (ratio >= 0.5) Check("Name match", "pass", "Name on the document matches the profile.", 0);
            else Check("Name match", "fail", "The profile name was not clearly found on the document.", 25);
        }

        var dates = ExtractDates(text);
        if (dates.Count > 0) r.ExtractedData["datesFound"] = dates.Select(d => d.ToString("yyyy-MM-dd")).ToList();

        // 6. Type-specific checks
        switch (type)
        {
            case DocumentType.GovernmentId: CheckGovernmentId(profile, lower, normalized, dates, Check, r); break;
            case DocumentType.AddressProof: CheckAddressProof(lower, dates, Check); break;
            case DocumentType.SalarySlip: CheckSalarySlip(text, declaredIncome, Check, r); break;
            case DocumentType.BankStatement: CheckBankStatement(text, Check, r); break;
            case DocumentType.EmploymentLetter: CheckEmploymentLetter(profile, normalized, Check); break;
        }

        // Official issuer registry (DigiLocker-style) — the authoritative "does this ID exist" check.
        if (type == DocumentType.GovernmentId && !string.IsNullOrWhiteSpace(profile.GovernmentIdNumber))
        {
            var reg = await _registry.VerifyIdAsync(profile.GovernmentIdNumber, profile.FullName, profile.DateOfBirth);
            r.ExtractedData["registryProvider"] = reg.Provider;
            r.ExtractedData["registryVerified"] = reg.Available && reg.Exists && reg.NameMatch && reg.DobMatch;
            if (!reg.Available)
                Check("Official registry (DigiLocker)", "warn", "Could not reach the official registry to confirm this ID.", 5);
            else if (reg.Exists && reg.NameMatch && reg.DobMatch)
                Check("Official registry (DigiLocker)", "pass", "Verified against the official registry — the ID exists and the holder's details match.", 0);
            else if (reg.Exists)
                Check("Official registry (DigiLocker)", "fail", reg.Detail, 40);
            else
                Check("Official registry (DigiLocker)", reg.Provider == "mock" ? "warn" : "fail", reg.Detail, reg.Provider == "mock" ? 15 : 45);
        }

        return Finish(r, checks, score);
    }

    // ---- type-specific ----

    private void CheckGovernmentId(CustomerProfile p, string lower, string normalized,
        List<DateTime> dates, Action<string, string, string, int> check, AnalysisResult r)
    {
        // ID number
        if (!string.IsNullOrWhiteSpace(p.GovernmentIdNumber))
        {
            if (normalized.Contains(Normalize(p.GovernmentIdNumber)))
                check("ID number", "pass", "ID number matches the profile.", 0);
            else
                check("ID number", "fail", "The profile's government ID number was not found on the document.", 22);
        }

        // DOB
        if (p.DateOfBirth.HasValue)
        {
            if (dates.Any(d => d.Date == p.DateOfBirth.Value.Date))
                check("Date of birth", "pass", "Date of birth matches the profile.", 0);
            else
                check("Date of birth", "warn", "Date of birth on the ID did not match the profile.", 15);
        }

        // Expiry — the key "not expired" check
        var expiry = FindLabeledDate(lower, dates, new[] { "valid thru", "valid through", "valid until", "valid upto", "date of expiry", "expiry", "expires", "exp " });
        if (expiry.HasValue)
        {
            if (expiry.Value.Date < DateTime.UtcNow.Date)
            {
                r.ExtractedData["expiry"] = expiry.Value.ToString("yyyy-MM-dd");
                r.ExtractedData["expired"] = true;
                check("Expiry", "fail", $"The ID expired on {expiry.Value:dd MMM yyyy}.", 50);
            }
            else
            {
                r.ExtractedData["expiry"] = expiry.Value.ToString("yyyy-MM-dd");
                r.ExtractedData["expired"] = false;
                check("Expiry", "pass", $"Valid until {expiry.Value:dd MMM yyyy}.", 0);
            }
        }
        else
        {
            check("Expiry", "warn", "No expiry date could be read from the ID.", 8);
        }
    }

    private void CheckAddressProof(string lower, List<DateTime> dates, Action<string, string, string, int> check)
    {
        if (dates.Count == 0) { check("Recency", "warn", "No issue date was found on the address proof.", 14); return; }
        var recent = dates.Any(d => d >= DateTime.UtcNow.AddMonths(-3) && d <= DateTime.UtcNow.AddDays(1));
        if (recent) check("Recency", "pass", "Dated within the last 3 months.", 0);
        else check("Recency", "warn", "Address proof appears older than 3 months.", 20);
    }

    private void CheckSalarySlip(string text, decimal declaredIncome, Action<string, string, string, int> check, AnalysisResult r)
    {
        var amounts = ExtractAmounts(text);
        if (amounts.Count == 0) { check("Income", "warn", "No salary amount could be read from the slip.", 20); return; }
        var monthly = amounts.Max();
        r.ExtractedData["detectedMonthlyAmount"] = monthly;
        if (declaredIncome > 0)
        {
            var implied = monthly * 12m;
            var diff = Math.Abs(implied - declaredIncome) / declaredIncome;
            r.ExtractedData["impliedAnnualIncome"] = implied;
            r.ExtractedData["incomeDeviation"] = Math.Round(diff, 2);
            if (diff > 0.15m)
                check("Income", "warn", $"Slip income (~{implied:N0}/yr) differs from the declared {declaredIncome:N0} by {diff:P0}.", 25);
            else
                check("Income", "pass", "Slip income is consistent with the declared income.", 0);
        }
    }

    private void CheckBankStatement(string text, Action<string, string, string, int> check, AnalysisResult r)
    {
        var amounts = ExtractAmounts(text);
        if (amounts.Count < 2) { check("Transactions", "warn", "Too few transactions found to verify the statement.", 15); return; }
        r.ExtractedData["transactionsDetected"] = amounts.Count;
        r.ExtractedData["approxBalance"] = amounts.Max();
        check("Transactions", "pass", $"{amounts.Count} amounts detected.", 0);
    }

    private void CheckEmploymentLetter(CustomerProfile p, string normalized, Action<string, string, string, int> check)
    {
        if (string.IsNullOrWhiteSpace(p.EmployerName)) return;
        if (normalized.Contains(Normalize(p.EmployerName)))
            check("Employer", "pass", "Employer name matches the profile.", 0);
        else
            check("Employer", "warn", "The declared employer name was not found in the letter.", 20);
    }

    // ---- finish / scoring ----

    private AnalysisResult Finish(AnalysisResult r, List<Dictionary<string, string>> checks, int score)
    {
        r.RiskScore = Math.Min(score, 100);
        r.RiskLevel = score >= 55 ? RiskLevel.High : score >= 25 ? RiskLevel.Medium : RiskLevel.Low;
        r.ExtractedData["checks"] = checks;
        r.ExtractedData["verdict"] = r.RiskLevel switch
        {
            RiskLevel.High => "Likely fraudulent / tampered — manual review required",
            RiskLevel.Medium => "Needs review — some checks did not pass",
            _ => "Appears genuine — passed all automated checks"
        };
        return r;
    }

    // ---- helpers ----

    private string? DetectEditingSoftware(string path)
    {
        try
        {
            var dirs = ImageMetadataReader.ReadMetadata(path);
            foreach (var dir in dirs)
                foreach (var tag in dir.Tags)
                {
                    if (tag.Name.Contains("Software", StringComparison.OrdinalIgnoreCase) ||
                        tag.Name.Contains("Creator Tool", StringComparison.OrdinalIgnoreCase))
                    {
                        var v = (tag.Description ?? "").ToLowerInvariant();
                        var hit = EditingSoftware.FirstOrDefault(s => v.Contains(s));
                        if (hit != null) return tag.Description;
                    }
                }
        }
        catch { /* metadata not readable — not a penalty */ }
        return null;
    }

    private static DateTime? FindLabeledDate(string lower, List<DateTime> dates, string[] labels)
    {
        foreach (var label in labels)
        {
            var idx = lower.IndexOf(label, StringComparison.Ordinal);
            if (idx < 0) continue;
            var window = lower.Substring(idx, Math.Min(40, lower.Length - idx));
            foreach (var d in dates)
            {
                // match the date if its day/month/year digits appear in the window after the label
                if (window.Contains(d.Year.ToString()) &&
                    (window.Contains(d.Day.ToString("D2")) || window.Contains(d.Day.ToString())))
                    return d;
            }
        }
        // fall back: if an expiry-like label exists, return the latest future-ish date
        return null;
    }

    private static string Sha256(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        var sb = new StringBuilder();
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    private static string Normalize(string s) =>
        new string(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    private static string Pretty(DocumentType t) => t switch
    {
        DocumentType.GovernmentId => "Government ID",
        DocumentType.AddressProof => "address proof",
        DocumentType.SalarySlip => "salary slip",
        DocumentType.BankStatement => "bank statement",
        DocumentType.EmploymentLetter => "employment letter",
        _ => t.ToString()
    };

    private static readonly string[] DateFormats =
    {
        "dd/MM/yyyy","d/M/yyyy","dd-MM-yyyy","yyyy-MM-dd","yyyy/MM/dd",
        "dd MMM yyyy","d MMM yyyy","dd MMMM yyyy","MMM dd yyyy","MMMM d yyyy","dd.MM.yyyy"
    };

    private static List<DateTime> ExtractDates(string text)
    {
        var found = new List<DateTime>();
        var matches = Regex.Matches(text,
            @"\b(\d{1,2}[/\-.]\d{1,2}[/\-.]\d{2,4}|\d{4}[/\-]\d{1,2}[/\-]\d{1,2}|\d{1,2}\s+[A-Za-z]{3,9}\s+\d{4}|[A-Za-z]{3,9}\s+\d{1,2},?\s+\d{4})\b");
        foreach (Match m in matches)
            foreach (var fmt in DateFormats)
                if (DateTime.TryParseExact(m.Value.Replace(",", ""), fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                { found.Add(dt); break; }
        return found.Distinct().ToList();
    }

    private static List<decimal> ExtractAmounts(string text)
    {
        var amounts = new List<decimal>();
        var matches = Regex.Matches(text, @"(?:₹|rs\.?|inr|\$|usd)?\s?(\d{1,3}(?:,\d{2,3})+(?:\.\d{1,2})?|\d{4,}(?:\.\d{1,2})?)", RegexOptions.IgnoreCase);
        foreach (Match m in matches)
        {
            var raw = m.Groups[1].Value.Replace(",", "");
            if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var val) && val >= 1000)
                amounts.Add(val);
        }
        return amounts;
    }
}
