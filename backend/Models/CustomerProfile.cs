using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Veriqo.Api.Models;

public class Address
{
    public string Line1 { get; set; } = string.Empty;
    public string Line2 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

public class CustomerProfile
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;
    public string PictureUrl { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string Nationality { get; set; } = string.Empty;
    public string GovernmentIdNumber { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;
    public Address Address { get; set; } = new();

    public string EmploymentStatus { get; set; } = string.Empty;
    public string EmployerName { get; set; } = string.Empty;
    public decimal AnnualIncome { get; set; }

    public bool IsComplete { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
