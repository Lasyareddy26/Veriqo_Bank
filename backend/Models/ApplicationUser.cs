using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Veriqo.Api.Models;

/// <summary>
/// The identity user, persisted in MongoDB and managed by ASP.NET Core Identity's
/// UserManager (PBKDF2 password hashing, account lockout, security stamps).
/// </summary>
public class ApplicationUser
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string UserName { get; set; } = string.Empty;
    public string NormalizedUserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public bool EmailConfirmed { get; set; }

    public string? PasswordHash { get; set; }
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString();
    public string ConcurrencyStamp { get; set; } = Guid.NewGuid().ToString();

    public string Role { get; set; } = Roles.Customer;
    public string Provider { get; set; } = "local"; // local | google

    public int AccessFailedCount { get; set; }
    public bool LockoutEnabled { get; set; } = true;
    public DateTimeOffset? LockoutEnd { get; set; }

    // Email OTP verification
    public string? OtpCode { get; set; }
    public DateTime? OtpExpiresAt { get; set; }
    public int OtpAttempts { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
