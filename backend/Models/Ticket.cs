using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Veriqo.Api.Models;

public enum TicketStatus
{
    Open,
    PendingCustomer,
    Resolved,
    Closed
}

public enum TicketPriority
{
    Low,
    Normal,
    High,
    Urgent
}

public class TicketMessage
{
    public string Sender { get; set; } = "Customer"; // Customer | Agent
    public string SenderEmail { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime At { get; set; } = DateTime.UtcNow;
}

public class Ticket
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string? ApplicationId { get; set; }

    public string Subject { get; set; } = string.Empty;
    public string Category { get; set; } = "General";

    [BsonRepresentation(BsonType.String)]
    public TicketStatus Status { get; set; } = TicketStatus.Open;

    [BsonRepresentation(BsonType.String)]
    public TicketPriority Priority { get; set; } = TicketPriority.Normal;

    public List<TicketMessage> Messages { get; set; } = new();
    public AiAnalysis? LastAiAnalysis { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
