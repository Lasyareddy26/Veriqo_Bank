namespace Veriqo.Api.Models;

public class AiSentiment
{
    public string Label { get; set; } = "Neutral"; // Negative | Neutral | Positive
    public double Score { get; set; }               // -1 .. 1
    public string Urgency { get; set; } = "Normal";  // Low | Normal | High | Urgent
}

/// <summary>The support assistant's analysis of a ticket (summary, sentiment, draft reply, actions).</summary>
public class AiAnalysis
{
    public string Summary { get; set; } = string.Empty;
    public AiSentiment Sentiment { get; set; } = new();
    public string SuggestedReply { get; set; } = string.Empty;
    public List<string> NextBestActions { get; set; } = new();

    // Multi-agent / NLP / RAG outputs
    public string Intent { get; set; } = string.Empty;          // e.g. status_check, document_help, complaint
    public List<string> Entities { get; set; } = new();          // extracted product/amount/date mentions
    public List<string> Sources { get; set; } = new();           // knowledge-base articles used (RAG citations)
    public List<string> Pipeline { get; set; } = new();          // which agents ran (for transparency)

    public string Provider { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
