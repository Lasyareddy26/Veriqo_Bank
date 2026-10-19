using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Veriqo.Api.Models;

public class KycDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string? ApplicationId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public DocumentType Type { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string StoredName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    public string Sha256 { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.String)]
    public DocumentStatus Status { get; set; } = DocumentStatus.Pending;

    public Dictionary<string, object> ExtractedData { get; set; } = new();
    public List<string> RiskFlags { get; set; } = new();

    [BsonRepresentation(BsonType.String)]
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;
    public int RiskScore { get; set; }
    public string OcrPreview { get; set; } = string.Empty;

    public string ReviewedBy { get; set; } = string.Empty;
    public DateTime? ReviewedAt { get; set; }
    public string RejectReason { get; set; } = string.Empty;

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
