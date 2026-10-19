using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Veriqo.Api.Models;

/// <summary>A bank policy / FAQ snippet, retrieved by the RAG step to ground replies.</summary>
public class KnowledgeArticle
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
}
