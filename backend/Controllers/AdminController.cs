using Veriqo.Api.Data;
using Veriqo.Api.Dtos;
using Veriqo.Api.Models;
using Veriqo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace Veriqo.Api.Controllers;

[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly MongoDbContext _db;
    private readonly ApplicationService _apps;
    private readonly NotificationService _notify;
    private readonly AuditService _audit;
    private readonly IAiAssistant _ai;
    private readonly IEmailService _email;

    public AdminController(MongoDbContext db, ApplicationService apps, NotificationService notify, AuditService audit, IAiAssistant ai, IEmailService email)
    {
        _db = db;
        _apps = apps;
        _notify = notify;
        _audit = audit;
        _ai = ai;
        _email = email;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> Stats()
    {
        var all = await _db.Applications.Find(_ => true).ToListAsync();
        return Ok(new
        {
            total = all.Count,
            submitted = all.Count(a => a.Status == ApplicationStatus.Submitted),
            underReview = all.Count(a => a.Status == ApplicationStatus.UnderReview),
            actionRequired = all.Count(a => a.Status == ApplicationStatus.ActionRequired),
            approved = all.Count(a => a.Status == ApplicationStatus.Approved),
            rejected = all.Count(a => a.Status == ApplicationStatus.Rejected),
            highRisk = all.Count(a => a.RiskLevel == RiskLevel.High),
            customers = await _db.Users.CountDocumentsAsync(u => u.Role == Roles.Customer)
        });
    }

    [HttpGet("applications")]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] string? risk)
    {
        var filter = Builders<Application>.Filter.Empty;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<ApplicationStatus>(status, out var st))
            filter &= Builders<Application>.Filter.Eq(a => a.Status, st);
        if (!string.IsNullOrEmpty(risk) && Enum.TryParse<RiskLevel>(risk, out var rl))
            filter &= Builders<Application>.Filter.Eq(a => a.RiskLevel, rl);

        var apps = await _db.Applications.Find(filter)
            .SortByDescending(a => a.CreatedAt).ToListAsync();

        var userIds = apps.Select(a => a.UserId).Distinct().ToList();
        var profiles = await _db.Profiles.Find(p => userIds.Contains(p.UserId)).ToListAsync();
        var users = await _db.Users.Find(u => userIds.Contains(u.Id)).ToListAsync();

        var rows = apps.Select(a => new
        {
            a.Id,
            a.ProductName,
            status = a.Status.ToString(),
            riskLevel = a.RiskLevel.ToString(),
            a.RiskScore,
            flagCount = a.RiskFlags.Count,
            a.CreatedAt,
            customerName = profiles.FirstOrDefault(p => p.UserId == a.UserId)?.FullName ?? "—",
            customerEmail = users.FirstOrDefault(u => u.Id == a.UserId)?.Email ?? "—"
        });
        return Ok(rows);
    }

    [HttpGet("applications/{id}")]
    public async Task<IActionResult> Detail(string id)
    {
        var app = await _db.Applications.Find(a => a.Id == id).FirstOrDefaultAsync();
        if (app == null) return NotFound();

        var profile = await _db.Profiles.Find(p => p.UserId == app.UserId).FirstOrDefaultAsync();
        var user = await _db.Users.Find(u => u.Id == app.UserId).FirstOrDefaultAsync();
        var product = await _db.Products.Find(p => p.Key == app.ProductKey).FirstOrDefaultAsync();
        var docs = await _db.Documents.Find(d => d.ApplicationId == id).ToListAsync();

        return Ok(new { application = app, profile, email = user?.Email, product, documents = docs });
    }

    [HttpPut("applications/{id}/decision")]
    public async Task<IActionResult> Decide(string id, DecisionRequest req)
    {
        var app = await _db.Applications.Find(a => a.Id == id).FirstOrDefaultAsync();
        if (app == null) return NotFound();

        if (!Enum.TryParse<ApplicationStatus>(req.Decision, out var newStatus) ||
            newStatus is not (ApplicationStatus.Approved or ApplicationStatus.Rejected
                or ApplicationStatus.ActionRequired or ApplicationStatus.UnderReview))
            return BadRequest(new { message = "Invalid decision." });

        app.Status = newStatus;
        app.ReviewNote = req.Note;
        app.History.Add(new StatusEvent { Status = newStatus, Note = req.Note, By = User.Email() });
        app.UpdatedAt = DateTime.UtcNow;
        await _db.Applications.ReplaceOneAsync(a => a.Id == id, app);

        var (title, msg, type) = newStatus switch
        {
            ApplicationStatus.Approved => ("Application approved 🎉", $"Great news — your {app.ProductName} application has been approved.", "success"),
            ApplicationStatus.Rejected => ("Application rejected", $"Your {app.ProductName} application was not approved. {req.Note}", "error"),
            ApplicationStatus.ActionRequired => ("Action needed", $"We need more from you on your {app.ProductName} application. {req.Note}", "warning"),
            _ => ("Application under review", $"Your {app.ProductName} application is now under review.", "info")
        };
        await _notify.NotifyAsync(app.UserId, title, msg, type, $"/applications/{app.Id}");
        await _audit.LogAsync(User.Email(), $"Decision: {newStatus}", "Application", id, req.Note);

        // On approval, send the customer a personalized congratulations email (best-effort —
        // a mail failure never blocks the decision).
        bool emailSent = false;
        string? emailTo = null;
        if (newStatus == ApplicationStatus.Approved)
        {
            var user = await _db.Users.Find(u => u.Id == app.UserId).FirstOrDefaultAsync();
            var profile = await _db.Profiles.Find(p => p.UserId == app.UserId).FirstOrDefaultAsync();
            if (user != null && !string.IsNullOrWhiteSpace(user.Email))
            {
                emailTo = user.Email;
                var name = profile?.FullName ?? user.UserName;
                emailSent = await _email.SendApplicationApprovedAsync(user.Email, name, app.ProductName, req.Note);
                await _audit.LogAsync(User.Email(), emailSent ? "Approval email sent" : "Approval email not sent (SMTP off/failed)",
                    "Application", id, user.Email);
            }
        }

        return Ok(new { status = newStatus.ToString(), emailSent, emailTo });
    }

    [HttpPut("documents/{id}/review")]
    public async Task<IActionResult> ReviewDocument(string id, DocumentReviewRequest req)
    {
        var doc = await _db.Documents.Find(d => d.Id == id).FirstOrDefaultAsync();
        if (doc == null) return NotFound();

        if (!Enum.TryParse<DocumentStatus>(req.Decision, out var status) ||
            status is not (DocumentStatus.Verified or DocumentStatus.Rejected))
            return BadRequest(new { message = "Invalid decision." });

        doc.Status = status;
        doc.ReviewedBy = User.Email();
        doc.ReviewedAt = DateTime.UtcNow;
        doc.RejectReason = status == DocumentStatus.Rejected ? req.Reason : string.Empty;
        await _db.Documents.ReplaceOneAsync(d => d.Id == id, doc);

        if (!string.IsNullOrEmpty(doc.ApplicationId))
            await _apps.RecomputeRiskAsync(doc.ApplicationId);

        if (status == DocumentStatus.Rejected)
            await _notify.NotifyAsync(doc.UserId, "Document rejected",
                $"Your {doc.Type} was rejected: {req.Reason}. Please re-upload.", "warning");

        await _audit.LogAsync(User.Email(), $"Document {status}", "Document", id, req.Reason);
        return Ok(new { status = status.ToString() });
    }

    [HttpGet("audit")]
    public async Task<IActionResult> Audit()
    {
        var logs = await _db.AuditLogs.Find(_ => true)
            .SortByDescending(l => l.At).Limit(100).ToListAsync();
        return Ok(logs);
    }

    // ---- Tickets ----

    [HttpGet("tickets")]
    public async Task<IActionResult> Tickets([FromQuery] string? status)
    {
        var filter = Builders<Ticket>.Filter.Empty;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<TicketStatus>(status, out var st))
            filter &= Builders<Ticket>.Filter.Eq(t => t.Status, st);

        var tickets = await _db.Tickets.Find(filter).SortByDescending(t => t.UpdatedAt).ToListAsync();
        var userIds = tickets.Select(t => t.UserId).Distinct().ToList();
        var users = await _db.Users.Find(u => userIds.Contains(u.Id)).ToListAsync();

        var rows = tickets.Select(t => new
        {
            t.Id,
            t.Subject,
            t.Category,
            status = t.Status.ToString(),
            priority = t.Priority.ToString(),
            messages = t.Messages.Count,
            t.UpdatedAt,
            customerEmail = users.FirstOrDefault(u => u.Id == t.UserId)?.Email ?? "—"
        });
        return Ok(rows);
    }

    [HttpGet("tickets/{id}")]
    public async Task<IActionResult> TicketDetail(string id)
    {
        var t = await _db.Tickets.Find(x => x.Id == id).FirstOrDefaultAsync();
        if (t == null) return NotFound();
        var user = await _db.Users.Find(u => u.Id == t.UserId).FirstOrDefaultAsync();
        return Ok(new { ticket = t, customerEmail = user?.Email });
    }

    [HttpPost("tickets/{id}/reply")]
    public async Task<IActionResult> ReplyTicket(string id, AgentReplyRequest req)
    {
        var t = await _db.Tickets.Find(x => x.Id == id).FirstOrDefaultAsync();
        if (t == null) return NotFound();

        t.Messages.Add(new TicketMessage { Sender = "Agent", SenderEmail = User.Email(), Body = req.Body.Trim() });
        t.Status = Enum.TryParse<TicketStatus>(req.Status, out var st) ? st : TicketStatus.PendingCustomer;
        t.UpdatedAt = DateTime.UtcNow;
        await _db.Tickets.ReplaceOneAsync(x => x.Id == id, t);

        await _notify.NotifyAsync(t.UserId, "Support replied",
            $"An agent replied to your ticket \"{t.Subject}\".", "info");
        await _audit.LogAsync(User.Email(), "Ticket reply", "Ticket", id, "");
        return Ok();
    }

    [HttpPut("tickets/{id}/status")]
    public async Task<IActionResult> SetTicketStatus(string id, TicketStatusRequest req)
    {
        if (!Enum.TryParse<TicketStatus>(req.Status, out var st))
            return BadRequest(new { message = "Invalid status." });
        var t = await _db.Tickets.Find(x => x.Id == id).FirstOrDefaultAsync();
        if (t == null) return NotFound();

        t.Status = st;
        t.UpdatedAt = DateTime.UtcNow;
        await _db.Tickets.ReplaceOneAsync(x => x.Id == id, t);
        await _audit.LogAsync(User.Email(), $"Ticket {st}", "Ticket", id, "");
        return Ok(new { status = st.ToString() });
    }

    [HttpPost("tickets/{id}/analyze")]
    public async Task<IActionResult> AnalyzeTicket(string id)
    {
        var t = await _db.Tickets.Find(x => x.Id == id).FirstOrDefaultAsync();
        if (t == null) return NotFound();

        var profile = await _db.Profiles.Find(p => p.UserId == t.UserId).FirstOrDefaultAsync();
        var customerName = profile?.FullName ?? "the customer";

        var ctx = new AiContext
        {
            Subject = t.Subject,
            Category = t.Category,
            CustomerName = customerName,
            Messages = t.Messages.Select(m => (m.Sender, MaskPii(m.Body, profile))).ToList()
        };

        // Pull the linked application's real state so the assistant answers specifically.
        if (!string.IsNullOrEmpty(t.ApplicationId))
        {
            var app = await _db.Applications.Find(a => a.Id == t.ApplicationId).FirstOrDefaultAsync();
            if (app != null)
            {
                ctx.ProductName = app.ProductName;
                ctx.Status = app.Status.ToString();
                ctx.RiskLevel = app.RiskLevel.ToString();
                ctx.RiskFlags = app.RiskFlags;
                var product = await _db.Products.Find(p => p.Key == app.ProductKey).FirstOrDefaultAsync();
                var docs = await _db.Documents.Find(d => d.ApplicationId == app.Id).ToListAsync();
                if (product != null)
                {
                    var have = docs.Where(d => d.Status != DocumentStatus.Rejected).Select(d => d.Type).ToHashSet();
                    ctx.MissingDocuments = product.RequiredDocuments.Where(rt => !have.Contains(rt)).Select(Pretty).ToList();
                }
            }
        }

        var analysis = await _ai.AnalyzeAsync(ctx);

        // Persist the latest analysis on the ticket (audit + so the UI can reload it)
        await _db.Tickets.UpdateOneAsync(x => x.Id == id,
            Builders<Ticket>.Update.Set(x => x.LastAiAnalysis, analysis));
        await _audit.LogAsync(User.Email(), "AI analyze", "Ticket", id, analysis.Provider);

        return Ok(analysis);
    }

    // Light PII masking before text leaves the server for the LLM.
    private static string MaskPii(string text, CustomerProfile? profile)
    {
        if (string.IsNullOrEmpty(text)) return text;
        if (profile != null && !string.IsNullOrWhiteSpace(profile.GovernmentIdNumber))
            text = text.Replace(profile.GovernmentIdNumber, "[ID]", StringComparison.OrdinalIgnoreCase);
        // mask any long digit runs (account/card numbers)
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\b\d{7,}\b", "[REDACTED]");
        return text;
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
}
