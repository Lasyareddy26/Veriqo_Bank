using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Veriqo.Api.Models;

/// <summary>
/// A record in the simulated "official issuer registry" (stands in for DigiLocker / PAN /
/// Aadhaar lookups). In production this collection is replaced by a real government API call.
/// </summary>
public class RegistryRecord
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    public string IdNumber { get; set; } = string.Empty;   // normalized, upper-case
    public string FullName { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public string DocType { get; set; } = "GovernmentId";
}
