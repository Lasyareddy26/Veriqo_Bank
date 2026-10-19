using Veriqo.Api.Data;
using Veriqo.Api.Models;
using Veriqo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace Veriqo.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/documents")]
public class DocumentsController : ControllerBase
{
    private readonly MongoDbContext _db;
    private readonly IFileStorage _storage;
    private readonly DocumentAnalyzer _analyzer;
    private readonly ApplicationService _apps;
    private readonly NotificationService _notify;

    public DocumentsController(MongoDbContext db, IFileStorage storage, DocumentAnalyzer analyzer,
        ApplicationService apps, NotificationService notify)
    {
        _db = db;
        _storage = storage;
        _analyzer = analyzer;
        _apps = apps;
        _notify = notify;
    }

    [HttpPost("upload")]
    [Authorize(Roles = Roles.Customer)]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Upload([FromForm] string applicationId, [FromForm] string type, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file was uploaded." });

        if (!Enum.TryParse<DocumentType>(type, out var docType))
            return BadRequest(new { message = "Unknown document type." });

        var app = await _db.Applications.Find(a => a.Id == applicationId && a.UserId == User.UserId()).FirstOrDefaultAsync();
        if (app == null) return NotFound(new { message = "Application not found." });

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var bytes = ms.ToArray();

        var error = FileValidator.Validate(bytes, out var detectedType);
        if (!string.IsNullOrEmpty(error)) return BadRequest(new { message = error });

        var ext = detectedType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "application/pdf" => ".pdf",
            _ => ".bin"
        };
        var storedName = $"{Guid.NewGuid():N}{ext}";
        var path = _storage.GetPath(storedName);
        await _storage.SaveAsync(new MemoryStream(bytes), storedName);

        var profile = await _db.Profiles.Find(p => p.UserId == User.UserId()).FirstOrDefaultAsync()
                      ?? new CustomerProfile { UserId = User.UserId() };

        var analysis = await _analyzer.AnalyzeAsync(path, bytes, detectedType, docType, profile, profile.AnnualIncome);

        var doc = new KycDocument
        {
            UserId = User.UserId(),
            ApplicationId = applicationId,
            Type = docType,
            FileName = file.FileName,
            StoredName = storedName,
            ContentType = detectedType,
            Size = bytes.Length,
            Sha256 = analysis.Sha256,
            Status = DocumentStatus.Pending,
            ExtractedData = analysis.ExtractedData,
            RiskFlags = analysis.RiskFlags,
            RiskLevel = analysis.RiskLevel,
            RiskScore = analysis.RiskScore,
            OcrPreview = analysis.OcrPreview
        };
        await _db.Documents.InsertOneAsync(doc);

        await _db.Applications.UpdateOneAsync(a => a.Id == applicationId,
            Builders<Application>.Update.AddToSet(a => a.DocumentIds, doc.Id));
        await _apps.RecomputeRiskAsync(applicationId);

        return Ok(doc);
    }

    [HttpGet("{id}/file")]
    public async Task<IActionResult> Download(string id)
    {
        var doc = await _db.Documents.Find(d => d.Id == id).FirstOrDefaultAsync();
        if (doc == null) return NotFound();

        var isAdmin = User.IsInRole(Roles.Admin);
        if (!isAdmin && doc.UserId != User.UserId()) return Forbid();
        if (!_storage.Exists(doc.StoredName)) return NotFound();

        var stream = new FileStream(_storage.GetPath(doc.StoredName), FileMode.Open, FileAccess.Read);
        return File(stream, doc.ContentType, doc.FileName);
    }
}
