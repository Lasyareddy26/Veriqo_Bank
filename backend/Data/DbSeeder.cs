using Veriqo.Api.Models;
using Veriqo.Api.Settings;
using Microsoft.AspNetCore.Identity;
using MongoDB.Driver;

namespace Veriqo.Api.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(MongoDbContext db, UserManager<ApplicationUser> userManager, AdminSettings admin)
    {
        await SeedAdminAsync(userManager, admin);
        await SeedProductsAsync(db);
        await SeedRegistryAsync(db);
        await SeedKnowledgeAsync(db);
    }

    // Bank policy / FAQ knowledge base that the AI assistant's RAG step retrieves from.
    private static async Task SeedKnowledgeAsync(MongoDbContext db)
    {
        var articles = new List<KnowledgeArticle>
        {
            new() { Title = "Personal loan review timeline", Category = "Loans", Tags = new() { "loan", "review", "delay", "pending", "timeline", "sla" },
                Content = "Personal loan applications are reviewed within 2–3 business days once all required documents are verified. Reviews are delayed when documents are missing, unclear, or flagged by our automated checks. Customers are notified at every status change." },
            new() { Title = "Required documents for a personal loan", Category = "Loans", Tags = new() { "loan", "documents", "required", "kyc" },
                Content = "A personal loan needs: government ID, address proof, the last 3 salary slips, 6 months of bank statements, and an employment letter. Missing any of these keeps the application in Submitted/Under Review." },
            new() { Title = "Home loan eligibility and documents", Category = "Loans", Tags = new() { "home loan", "mortgage", "eligibility", "documents" },
                Content = "Home loans finance up to 80% of property value with tenures up to 25 years and a minimum annual income of 3,60,000. Required documents: government ID, address proof, salary slips, bank statements, and an employment letter." },
            new() { Title = "Credit card eligibility", Category = "Cards", Tags = new() { "credit card", "eligibility", "income", "limit" },
                Content = "Credit cards require a minimum annual income of 3,00,000 (Standard), 6,00,000 (Gold), or 12,00,000 (Platinum). The approved limit is typically up to about three times the applicant's monthly income." },
            new() { Title = "Application statuses explained", Category = "Applications", Tags = new() { "status", "under review", "submitted", "action required", "approved", "rejected" },
                Content = "Submitted means we've received it and await documents. Under Review means all documents are in and our team is assessing it. Action Required means we need something more from you. Approved and Rejected are final decisions, with a reason given on rejection." },
            new() { Title = "Why a document was rejected", Category = "KYC", Tags = new() { "document", "rejected", "reupload", "verification", "blurry", "expired" },
                Content = "Documents are rejected when they're unreadable/blurry, expired, don't match the profile name or date of birth, or appear edited. Re-upload a clear, valid copy and the application returns to review." },
            new() { Title = "Address proof must be recent", Category = "KYC", Tags = new() { "address proof", "recent", "utility bill", "3 months" },
                Content = "Address proof (utility bill, bank statement, rental agreement) must be dated within the last 3 months. Older documents are flagged and need to be replaced with a current one." },
            new() { Title = "How to update your phone number", Category = "Account", Tags = new() { "update", "phone", "mobile", "number", "contact", "change" },
                Content = "To update your registered phone number, go to Profile in your dashboard, edit the phone field, and save. Changes take effect immediately. For security, significant contact changes may require re-verification." },
            new() { Title = "How to update your address", Category = "Account", Tags = new() { "update", "address", "change", "profile" },
                Content = "Update your address under Profile. If you have pending applications, upload a fresh address proof dated within the last 3 months so KYC stays valid." },
            new() { Title = "Re-uploading KYC documents", Category = "KYC", Tags = new() { "reupload", "upload", "kyc", "documents", "resubmit" },
                Content = "When an application shows Action Required or a document is rejected, open the application and use the upload control next to the specific document. Each upload is automatically re-checked before it returns to the review queue." },
            new() { Title = "Complaint handling and escalation", Category = "Support", Tags = new() { "complaint", "escalate", "frustrated", "urgent", "senior" },
                Content = "Complaints are acknowledged within one business day. If an issue is unresolved after 10 days, or the customer is highly dissatisfied, it is escalated to a senior agent or the relevant specialist team for priority handling." },
            new() { Title = "Savings account opening", Category = "Accounts", Tags = new() { "savings", "account", "open", "minimum", "deposit" },
                Content = "A savings account needs government ID and address proof and a minimum opening deposit of 1,000. It offers up to 4.5% interest p.a. with no maintenance fee in the first year." },
            new() { Title = "Income verification for loans and cards", Category = "Verification", Tags = new() { "income", "salary", "verification", "mismatch", "slip" },
                Content = "Declared income is checked against uploaded salary slips and bank statements. A gap of more than about 15% between the declared figure and the documents is flagged and may require clarification or additional proof." },
            new() { Title = "Data security and privacy", Category = "Security", Tags = new() { "security", "privacy", "data", "encrypted", "safe" },
                Content = "Uploaded documents are stored privately and encrypted, accessible only to the customer and authorised reviewers. Veriqo never asks for full card numbers, passwords, or OTPs over chat." },
        };

        foreach (var a in articles)
            if (!await db.Knowledge.Find(x => x.Title == a.Title).AnyAsync())
                await db.Knowledge.InsertOneAsync(a);
    }

    // Simulated official issuer registry (stands in for DigiLocker/PAN/Aadhaar lookups).
    private static async Task SeedRegistryAsync(MongoDbContext db)
    {
        var records = new List<RegistryRecord>
        {
            new() { IdNumber = "P1234567", FullName = "Meera Nair", DateOfBirth = new DateTime(1995, 6, 15, 0, 0, 0, DateTimeKind.Utc) },
            new() { IdNumber = "P9988776", FullName = "Asha Rao", DateOfBirth = new DateTime(1992, 3, 10, 0, 0, 0, DateTimeKind.Utc) },
            new() { IdNumber = "P5566778", FullName = "Rahul Menon", DateOfBirth = new DateTime(1988, 11, 2, 0, 0, 0, DateTimeKind.Utc) },
            new() { IdNumber = "P4455667", FullName = "Divya Iyer", DateOfBirth = new DateTime(1998, 1, 25, 0, 0, 0, DateTimeKind.Utc) },
        };
        foreach (var r in records)
            if (!await db.Registry.Find(x => x.IdNumber == r.IdNumber).AnyAsync())
                await db.Registry.InsertOneAsync(r);
    }

    private static async Task SeedAdminAsync(UserManager<ApplicationUser> userManager, AdminSettings admin)
    {
        if (string.IsNullOrWhiteSpace(admin.Email) || string.IsNullOrWhiteSpace(admin.Password))
            return; // configure Admin:Email/Password (e.g. in appsettings.Development.json) to seed the admin
        var email = admin.Email.Trim().ToLowerInvariant();
        var existing = await userManager.FindByEmailAsync(email);
        if (existing != null)
        {
            if (existing.Role != Roles.Admin)
            {
                existing.Role = Roles.Admin;
                await userManager.UpdateAsync(existing);
            }
            return;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            Role = Roles.Admin
        };
        await userManager.CreateAsync(user, admin.Password);
    }

    private static async Task SeedProductsAsync(MongoDbContext db)
    {
        var products = new List<Product>
        {
            new()
            {
                Key = "savings",
                Name = "Savings Account",
                Category = "Everyday Banking",
                Tagline = "Grow your money with a flexible, zero-hassle account.",
                Description = "A secure savings account with competitive interest, instant online access and no minimum balance penalties for the first year.",
                Icon = "wallet",
                Highlights = new() { "Up to 4.5% interest p.a.", "Zero maintenance fees year one", "Instant digital access" },
                MinAnnualIncome = 0,
                SortOrder = 1,
                RequiredDocuments = new() { DocumentType.GovernmentId, DocumentType.AddressProof },
                Fields = new()
                {
                    new() { Name = "accountType", Label = "Account type", Type = "select", Options = new() { "Regular", "Student", "Senior" } },
                    new() { Name = "initialDeposit", Label = "Initial deposit (₹)", Type = "number", Min = 1000, Placeholder = "1000", HelpText = "Minimum ₹1,000 to open." },
                    new() { Name = "nomineeName", Label = "Nominee full name", Type = "text" },
                    new() { Name = "nomineeRelationship", Label = "Nominee relationship", Type = "text", Placeholder = "e.g. Spouse" },
                    new() { Name = "branch", Label = "Preferred branch", Type = "select", Options = new() { "Mumbai Main", "Delhi Connaught Place", "Bangalore MG Road", "Online Only" } }
                }
            },
            new()
            {
                Key = "credit-card",
                Name = "Credit Card",
                Category = "Cards",
                Tagline = "Rewards on every swipe, with bank-grade protection.",
                Description = "Choose Standard, Gold or Platinum. Enjoy cashback, airport lounge access and real-time fraud monitoring.",
                Icon = "credit-card",
                Highlights = new() { "Up to 5% cashback", "Lounge access (Gold+)", "Instant fraud alerts" },
                MinAnnualIncome = 300000,
                SortOrder = 2,
                RequiredDocuments = new() { DocumentType.GovernmentId, DocumentType.AddressProof, DocumentType.SalarySlip, DocumentType.EmploymentLetter },
                Fields = new()
                {
                    new() { Name = "cardType", Label = "Card type", Type = "select", Options = new() { "Standard", "Gold", "Platinum" }, HelpText = "Gold needs ₹6L+, Platinum ₹12L+ annual income." },
                    new() { Name = "employmentStatus", Label = "Employment status", Type = "select", Options = new() { "Salaried", "Self-employed" } },
                    new() { Name = "employerName", Label = "Employer name", Type = "text" },
                    new() { Name = "annualIncome", Label = "Annual income (₹)", Type = "number", Min = 300000, HelpText = "Minimum ₹3,00,000." },
                    new() { Name = "requestedLimit", Label = "Requested credit limit (₹)", Type = "number", Min = 10000 },
                    new() { Name = "hasExistingCards", Label = "Do you hold other credit cards?", Type = "select", Options = new() { "No", "Yes" } }
                }
            },
            new()
            {
                Key = "personal-loan",
                Name = "Personal Loan",
                Category = "Loans",
                Tagline = "Funds for what matters, with transparent EMIs.",
                Description = "Borrow from ₹50,000 to ₹25,00,000 with flexible tenures and no hidden charges. Decision-ready once your documents are verified.",
                Icon = "banknote",
                Highlights = new() { "Rates from 10.5% p.a.", "Tenure up to 60 months", "No prepayment penalty" },
                MinAnnualIncome = 240000,
                SortOrder = 3,
                RequiredDocuments = new() { DocumentType.GovernmentId, DocumentType.AddressProof, DocumentType.SalarySlip, DocumentType.BankStatement, DocumentType.EmploymentLetter },
                Fields = new()
                {
                    new() { Name = "loanAmount", Label = "Loan amount (₹)", Type = "number", Min = 50000, Max = 2500000 },
                    new() { Name = "tenureMonths", Label = "Tenure (months)", Type = "select", Options = new() { "12", "24", "36", "48", "60" } },
                    new() { Name = "purpose", Label = "Purpose", Type = "select", Options = new() { "Education", "Medical", "Travel", "Home Renovation", "Debt Consolidation", "Other" } },
                    new() { Name = "monthlyIncome", Label = "Monthly income (₹)", Type = "number", Min = 20000 },
                    new() { Name = "existingEmis", Label = "Existing monthly EMIs (₹)", Type = "number", Min = 0, Required = false, Placeholder = "0" }
                }
            },
            new()
            {
                Key = "current-account",
                Name = "Current Account",
                Category = "Business Banking",
                Tagline = "Run your business with unlimited transactions.",
                Description = "A business current account with high transaction limits, overdraft eligibility and dedicated relationship support.",
                Icon = "building",
                Highlights = new() { "Unlimited transactions", "Overdraft eligibility", "Dedicated support" },
                MinAnnualIncome = 0,
                SortOrder = 4,
                RequiredDocuments = new() { DocumentType.GovernmentId, DocumentType.AddressProof, DocumentType.BankStatement },
                Fields = new()
                {
                    new() { Name = "businessName", Label = "Business name", Type = "text" },
                    new() { Name = "businessType", Label = "Business type", Type = "select", Options = new() { "Sole Proprietor", "Partnership", "Private Limited", "LLP" } },
                    new() { Name = "registrationNumber", Label = "Business registration no.", Type = "text" },
                    new() { Name = "natureOfBusiness", Label = "Nature of business", Type = "text", Placeholder = "e.g. Retail, Consulting" },
                    new() { Name = "monthlyTurnover", Label = "Expected monthly turnover (₹)", Type = "number", Min = 0 }
                }
            },
            new()
            {
                Key = "fixed-deposit",
                Name = "Fixed Deposit",
                Category = "Investments",
                Tagline = "Lock in guaranteed returns, your way.",
                Description = "Earn higher, guaranteed interest for a fixed term with flexible payout options and premature-withdrawal support.",
                Icon = "banknote",
                Highlights = new() { "Up to 7.25% interest p.a.", "Flexible tenure 6–60 months", "Payout your way" },
                MinAnnualIncome = 0,
                SortOrder = 5,
                RequiredDocuments = new() { DocumentType.GovernmentId, DocumentType.AddressProof },
                Fields = new()
                {
                    new() { Name = "depositAmount", Label = "Deposit amount (₹)", Type = "number", Min = 10000, HelpText = "Minimum ₹10,000." },
                    new() { Name = "tenureMonths", Label = "Tenure (months)", Type = "select", Options = new() { "6", "12", "24", "36", "60" } },
                    new() { Name = "payout", Label = "Interest payout", Type = "select", Options = new() { "At maturity", "Monthly", "Quarterly" } },
                    new() { Name = "sourceOfFunds", Label = "Source of funds", Type = "select", Options = new() { "Salary", "Savings", "Business income", "Sale of asset", "Other" } },
                    new() { Name = "linkedAccount", Label = "Linked savings account (optional)", Type = "text", Required = false }
                }
            },
            new()
            {
                Key = "home-loan",
                Name = "Home Loan",
                Category = "Loans",
                Tagline = "Own your dream home with transparent EMIs.",
                Description = "Finance up to 80% of your property value with tenures to 25 years, competitive rates and no prepayment penalty.",
                Icon = "home",
                Highlights = new() { "Rates from 8.5% p.a.", "Tenure up to 25 years", "Up to 80% financing" },
                MinAnnualIncome = 360000,
                SortOrder = 6,
                RequiredDocuments = new() { DocumentType.GovernmentId, DocumentType.AddressProof, DocumentType.SalarySlip, DocumentType.BankStatement, DocumentType.EmploymentLetter },
                Fields = new()
                {
                    new() { Name = "propertyValue", Label = "Property value (₹)", Type = "number", Min = 500000 },
                    new() { Name = "loanAmount", Label = "Loan amount (₹)", Type = "number", Min = 100000 },
                    new() { Name = "downPayment", Label = "Down payment (₹)", Type = "number", Min = 0 },
                    new() { Name = "propertyAddress", Label = "Property address", Type = "text" },
                    new() { Name = "tenureMonths", Label = "Tenure (months)", Type = "select", Options = new() { "60", "120", "180", "240", "300" } },
                    new() { Name = "monthlyIncome", Label = "Monthly income (₹)", Type = "number", Min = 30000 },
                    new() { Name = "existingEmis", Label = "Existing monthly EMIs (₹)", Type = "number", Min = 0, Required = false, Placeholder = "0" }
                }
            }
        };

        foreach (var p in products)
            if (!await db.Products.Find(x => x.Key == p.Key).AnyAsync())
                await db.Products.InsertOneAsync(p);
    }
}
