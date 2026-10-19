using Veriqo.Api.Data;
using Veriqo.Api.Models;

namespace Veriqo.Api.Services;

public class NotificationService
{
    private readonly MongoDbContext _db;
    private readonly ILogger<NotificationService> _log;

    public NotificationService(MongoDbContext db, ILogger<NotificationService> log)
    {
        _db = db;
        _log = log;
    }

    public async Task NotifyAsync(string userId, string title, string message,
        string type = "info", string link = "")
    {
        var n = new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            Link = link
        };
        await _db.Notifications.InsertOneAsync(n);

        // Email stub — swap for SES / Azure Communication Services in production.
        _log.LogInformation("EMAIL → user {UserId}: [{Title}] {Message}", userId, title, message);
    }
}

public class AuditService
{
    private readonly MongoDbContext _db;
    public AuditService(MongoDbContext db) => _db = db;

    public Task LogAsync(string actorEmail, string action, string entityType, string entityId, string details = "")
        => _db.AuditLogs.InsertOneAsync(new AuditLog
        {
            ActorEmail = actorEmail,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details
        });
}
