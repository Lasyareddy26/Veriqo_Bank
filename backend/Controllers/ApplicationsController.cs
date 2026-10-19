using System.Text.Json;
using Veriqo.Api.Data;
using Veriqo.Api.Dtos;
using Veriqo.Api.Models;
using Veriqo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace Veriqo.Api.Controllers;

[ApiController]
[Authorize(Roles = Roles.Customer)]
[Route("api/applications")]
public class ApplicationsController : ControllerBase
{
    private readonly MongoDbContext _db;
    private readonly ApplicationService _apps;
    private readonly NotificationService _notify;

    public ApplicationsController(MongoDbContext db, ApplicationService apps, NotificationService notify)
    {
        _db = db;
        _apps = apps;
        _notify = notify;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateApplicationRequest req)
    {
        var profile = await _db.Profiles.Find(p => p.UserId == User.UserId()).FirstOrDefaultAsync();
        if (profile == null || !profile.IsComplete)
            return BadRequest(new { message = "Please complete your profile before applying." });

        var product = await _db.Products.Find(p => p.Key == req.ProductKey).FirstOrDefaultAsync();
        if (product == null) return NotFound(new { message = "Product not found." });

        // Validate required answers
        var missing = product.Fields
            .Where(f => f.Required && (!req.Answers.TryGetValue(f.Name, out var v) || string.IsNullOrWhiteSpace(v?.ToString())))
            .Select(f => f.Label).ToList();
        if (missing.Count > 0)
            return BadRequest(new { message = $"Please fill: {string.Join(", ", missing)}." });

        var app = new Application
        {
            UserId = User.UserId(),
            ProductKey = product.Key,
            ProductName = product.Name,
            Answers = Normalize(req.Answers),
            Status = ApplicationStatus.Submitted,
            History = new() { new StatusEvent { Status = ApplicationStatus.Submitted, Note = "Application submitted", By = User.Email() } }
        };
        await _db.Applications.InsertOneAsync(app);
        await _apps.RecomputeRiskAsync(app.Id);

        await _notify.NotifyAsync(User.UserId(), "Application received",
            $"Your {product.Name} application has been submitted and is awaiting document upload & review.",
            "success", $"/applications/{app.Id}");

        return Ok(new { id = app.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Mine()
    {
        var apps = await _db.Applications.Find(a => a.UserId == User.UserId())
            .SortByDescending(a => a.CreatedAt).ToListAsync();
        return Ok(apps);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var app = await _db.Applications.Find(a => a.Id == id && a.UserId == User.UserId()).FirstOrDefaultAsync();
        if (app == null) return NotFound();

        var product = await _db.Products.Find(p => p.Key == app.ProductKey).FirstOrDefaultAsync();
        var docs = await _db.Documents.Find(d => d.ApplicationId == id).ToListAsync();

        var requirements = product?.RequiredDocuments.Select(rt => new
        {
            type = rt.ToString(),
            label = Pretty(rt),
            satisfied = docs.Any(d => d.Type == rt && d.Status != DocumentStatus.Rejected)
        });

        return Ok(new { application = app, product, documents = docs, requirements });
    }

    /// <summary>Converts JSON values from the request body into BSON-friendly primitives.</summary>
    private static Dictionary<string, object> Normalize(Dictionary<string, object> answers)
    {
        var result = new Dictionary<string, object>();
        foreach (var (k, v) in answers)
        {
            if (v is JsonElement je)
            {
                result[k] = je.ValueKind switch
                {
                    JsonValueKind.Number => je.TryGetInt64(out var l) ? l : je.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => "",
                    _ => je.ToString()
                };
            }
            else result[k] = v ?? "";
        }
        return result;
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
