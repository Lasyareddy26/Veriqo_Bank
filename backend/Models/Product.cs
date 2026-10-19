using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Veriqo.Api.Models;

public class ProductField
{
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = "text"; // text, number, select, date, textarea
    public bool Required { get; set; } = true;
    public string Placeholder { get; set; } = string.Empty;
    public string HelpText { get; set; } = string.Empty;
    public List<string> Options { get; set; } = new();
    public double? Min { get; set; }
    public double? Max { get; set; }
}

public class Product
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Tagline { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public List<string> Highlights { get; set; } = new();

    public decimal MinAnnualIncome { get; set; }
    public List<ProductField> Fields { get; set; } = new();
    public List<DocumentType> RequiredDocuments { get; set; } = new();
    public bool Active { get; set; } = true;
    public int SortOrder { get; set; }
}
