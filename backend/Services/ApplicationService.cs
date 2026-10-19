using System.Globalization;
using Veriqo.Api.Data;
using Veriqo.Api.Models;
using MongoDB.Driver;

namespace Veriqo.Api.Services;

public class ApplicationService
{
    private readonly MongoDbContext _db;
    private readonly NotificationService _notify;
    public ApplicationService(MongoDbContext db, NotificationService notify)
    {
        _db = db;
        _notify = notify;
    }

    /// <summary>Rebuilds an application's risk flags & score from product rules + its documents,
    /// and advances it to Under Review once every required document has been provided.</summary>
    public async Task RecomputeRiskAsync(string applicationId)
    {
        var app = await _db.Applications.Find(a => a.Id == applicationId).FirstOrDefaultAsync();
        if (app == null) return;

        var product = await _db.Products.Find(p => p.Key == app.ProductKey).FirstOrDefaultAsync();
        var profile = await _db.Profiles.Find(p => p.UserId == app.UserId).FirstOrDefaultAsync();
        var docs = await _db.Documents.Find(d => d.ApplicationId == applicationId).ToListAsync();

        var flags = new List<string>();
        int score = 0;
        bool allDocsProvided = true;

        // 1. Eligibility rules from answers
        if (product != null)
            score += EvaluateEligibility(product, app.Answers, profile, flags);

        // 2. Document coverage
        if (product != null)
        {
            var providedTypes = docs.Where(d => d.Status != DocumentStatus.Rejected).Select(d => d.Type).ToHashSet();
            var missing = product.RequiredDocuments.Where(rt => !providedTypes.Contains(rt)).ToList();
            allDocsProvided = missing.Count == 0;
            if (missing.Count > 0)
            {
                flags.Add($"Missing documents: {string.Join(", ", missing.Select(Pretty))}.");
                score += missing.Count * 10;
            }
        }

        // 3. Aggregate document risk
        foreach (var d in docs)
        {
            foreach (var f in d.RiskFlags) flags.Add($"[{Pretty(d.Type)}] {f}");
            score += d.RiskScore / 2;
        }

        app.RiskFlags = flags;
        app.RiskScore = Math.Min(score, 100);
        app.RiskLevel = score >= 55 ? RiskLevel.High : score >= 25 ? RiskLevel.Medium : RiskLevel.Low;
        app.UpdatedAt = DateTime.UtcNow;

        // Auto-advance: once all required documents are in, move Submitted → Under Review.
        var advanced = false;
        if (allDocsProvided && app.Status == ApplicationStatus.Submitted)
        {
            app.Status = ApplicationStatus.UnderReview;
            app.History.Add(new StatusEvent
            {
                Status = ApplicationStatus.UnderReview,
                Note = "All documents received — application is now under review.",
                By = "system"
            });
            advanced = true;
        }

        await _db.Applications.ReplaceOneAsync(a => a.Id == applicationId, app);

        if (advanced)
            await _notify.NotifyAsync(app.UserId, "Application under review",
                $"We've received all documents for your {app.ProductName} application. Our team is now reviewing it.",
                "info", $"/applications/{app.Id}");
    }

    public int EvaluateEligibility(Product product, Dictionary<string, object> answers,
        CustomerProfile? profile, List<string> flags)
    {
        int s = 0;
        switch (product.Key)
        {
            case "savings":
                if (GetDecimal(answers, "initialDeposit") < 1000)
                {
                    flags.Add("Initial deposit is below the ₹1,000 minimum.");
                    s += 15;
                }
                break;

            case "credit-card":
                var income = GetDecimal(answers, "annualIncome");
                var cardType = GetString(answers, "cardType");
                var cardMin = cardType switch { "Platinum" => 1200000m, "Gold" => 600000m, _ => 300000m };
                if (income < cardMin)
                {
                    flags.Add($"Annual income {income:N0} is below the {cardType} card minimum of {cardMin:N0}.");
                    s += 25;
                }
                var limit = GetDecimal(answers, "requestedLimit");
                var maxLimit = income / 12m * 3m; // ~3x monthly income
                if (limit > maxLimit && maxLimit > 0)
                {
                    flags.Add($"Requested limit {limit:N0} exceeds ~3× monthly income ({maxLimit:N0}).");
                    s += 15;
                }
                break;

            case "personal-loan":
                s += LoanDtiCheck(answers, "loanAmount", 0.105m, flags);
                break;

            case "home-loan":
                var propertyValue = GetDecimal(answers, "propertyValue");
                var homeLoan = GetDecimal(answers, "loanAmount");
                if (propertyValue > 0 && homeLoan > propertyValue * 0.8m)
                {
                    flags.Add($"Loan {homeLoan:N0} exceeds 80% of the property value ({propertyValue * 0.8m:N0}).");
                    s += 20;
                }
                s += LoanDtiCheck(answers, "loanAmount", 0.085m, flags);
                break;

            case "fixed-deposit":
                if (GetDecimal(answers, "depositAmount") < 10000)
                {
                    flags.Add("Deposit amount is below the ₹10,000 minimum.");
                    s += 15;
                }
                break;
        }
        return s;
    }

    private int LoanDtiCheck(Dictionary<string, object> answers, string amountKey, decimal annualRate, List<string> flags)
    {
        var amount = GetDecimal(answers, amountKey);
        var tenure = (int)GetDecimal(answers, "tenureMonths");
        var monthly = GetDecimal(answers, "monthlyIncome");
        var existing = GetDecimal(answers, "existingEmis");
        if (tenure <= 0 || amount <= 0) return 0;

        var emi = CalculateEmi(amount, annualRate, tenure);
        var dti = monthly > 0 ? (existing + emi) / monthly : 1m;
        if (dti > 0.5m)
        {
            flags.Add($"Debt-to-income ratio is {dti:P0} (EMI ≈ {emi:N0}), above the 50% ceiling.");
            return 30;
        }
        return 0;
    }

    public static decimal CalculateEmi(decimal principal, decimal annualRate, int months)
    {
        var r = annualRate / 12m;
        if (r == 0) return principal / months;
        var pow = (decimal)Math.Pow((double)(1 + r), months);
        return principal * r * pow / (pow - 1);
    }

    private static string Pretty(DocumentType t) => t switch
    {
        DocumentType.GovernmentId => "Government ID",
        DocumentType.AddressProof => "Address proof",
        DocumentType.SalarySlip => "Salary slip",
        DocumentType.BankStatement => "Bank statement",
        DocumentType.EmploymentLetter => "Employment letter",
        _ => t.ToString()
    };

    private static decimal GetDecimal(Dictionary<string, object> a, string key)
    {
        if (!a.TryGetValue(key, out var v) || v == null) return 0;
        return decimal.TryParse(v.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0;
    }

    private static string GetString(Dictionary<string, object> a, string key) =>
        a.TryGetValue(key, out var v) ? v?.ToString() ?? string.Empty : string.Empty;
}
