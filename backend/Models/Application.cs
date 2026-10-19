using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Veriqo.Api.Models;

public class StatusEvent
{
    public ApplicationStatus Status { get; set; }
    public string Note { get; set; } = string.Empty;
    public string By { get; set; } = string.Empty;
    public DateTime At { get; set; } = DateTime.UtcNow;
}

public class Application
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = string.Empty;

    public string ProductKey { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;

    [BsonExtraElements]
    public BsonDocument? Extra { get; set; }

    public Dictionary<string, object> Answers { get; set; } = new();

    [BsonRepresentation(BsonType.String)]
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Submitted;

    [BsonRepresentation(BsonType.String)]
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;
    public int RiskScore { get; set; }
    public List<string> RiskFlags { get; set; } = new();

    public List<string> DocumentIds { get; set; } = new();
    public List<StatusEvent> History { get; set; } = new();
    public string ReviewNote { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
