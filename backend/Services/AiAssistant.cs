using System.Text;
using System.Text.Json;
using Veriqo.Api.Models;
using Veriqo.Api.Settings;
using Microsoft.Extensions.Options;

namespace Veriqo.Api.Services;

/// <summary>Everything the assistant needs to analyse a ticket — the thread plus the linked application.</summary>
public class AiContext
{
    public string Subject { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public List<(string Sender, string Body)> Messages { get; set; } = new();

    public string? ProductName { get; set; }
    public string? Status { get; set; }
    public string? RiskLevel { get; set; }
    public List<string> RiskFlags { get; set; } = new();
    public List<string> MissingDocuments { get; set; } = new();

    public string LastCustomerMessage => Messages.LastOrDefault(m => m.Sender == "Customer").Body ?? Subject;

    public string Render()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Customer: {CustomerName}");
        sb.AppendLine($"Ticket subject: {Subject}");
        sb.AppendLine($"Category: {Category}");
        if (ProductName != null)
        {
            sb.AppendLine($"\nLinked application: {ProductName} — status: {Status}, risk: {RiskLevel}");
            if (MissingDocuments.Count > 0) sb.AppendLine($"Missing documents: {string.Join(", ", MissingDocuments)}");
            if (RiskFlags.Count > 0) sb.AppendLine($"Automated flags: {string.Join("; ", RiskFlags)}");
        }
        else sb.AppendLine("\nNot linked to a specific application.");
        sb.AppendLine("\nConversation (most recent last):");
        foreach (var (sender, body) in Messages) sb.AppendLine($"[{sender}] {body}");
        return sb.ToString();
    }
}

public interface IAiAssistant
{
    Task<AiAnalysis> AnalyzeAsync(AiContext ctx);
}

// ---- Low-level Groq chat client (OpenAI-compatible) ----
public class GroqChatClient
{
    private readonly HttpClient _http;
    private readonly AiSettings _cfg;
    private readonly ILogger<GroqChatClient> _log;

    public GroqChatClient(HttpClient http, IOptions<AiSettings> cfg, ILogger<GroqChatClient> log)
    {
        _http = http;
        _cfg = cfg.Value;
        _log = log;
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    public string Model => _cfg.Model;

    /// <summary>One chat completion in JSON mode; returns the parsed root element, or null on failure.</summary>
    public async Task<JsonElement?> CompleteJsonAsync(string system, string user)
    {
        try
        {
            var payload = new
            {
                model = _cfg.Model,
                temperature = 0.3,
                max_tokens = 2000,
                response_format = new { type = "json_object" },
                messages = new object[]
                {
                    new { role = "system", content = system },
                    new { role = "user", content = user }
                }
            };
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{_cfg.BaseUrl.TrimEnd('/')}/chat/completions");
            req.Headers.Add("Authorization", $"Bearer {_cfg.ApiKey}");
            req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var resp = await _http.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode) { _log.LogWarning("Groq {Status}: {Body}", resp.StatusCode, body); return null; }

            using var doc = JsonDocument.Parse(body);
            var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "{}";
            return JsonDocument.Parse(content).RootElement.Clone();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Groq call failed.");
            return null;
        }
    }
}

/// <summary>
/// Multi-agent support assistant with RAG. Pipeline:
///   1. Triage agent (NLP): intent, entities, sentiment, and a knowledge-base search query.
///   2. Retrieval (RAG): pull the most relevant bank policy/FAQ snippets.
///   3. Drafting agent: summary + reply grounded in the retrieved policy + application state.
///   4. Next-best-action agent: concrete steps for the human agent.
/// Falls back to the deterministic mock if the LLM is unavailable.
/// </summary>
public class MultiAgentAssistant : IAiAssistant
{
    private readonly GroqChatClient _groq;
    private readonly IKnowledgeRetriever _retriever;
    private readonly MockAiAssistant _fallback;
    private readonly ILogger<MultiAgentAssistant> _log;

    public MultiAgentAssistant(GroqChatClient groq, IKnowledgeRetriever retriever, MockAiAssistant fallback, ILogger<MultiAgentAssistant> log)
    {
        _groq = groq;
        _retriever = retriever;
        _fallback = fallback;
        _log = log;
    }

    public async Task<AiAnalysis> AnalyzeAsync(AiContext ctx)
    {
        var pipeline = new List<string>();

        // 1. Triage / NLP agent
        var triage = await _groq.CompleteJsonAsync(TriagePrompt, ctx.Render());
        if (triage == null) return await _fallback.AnalyzeAsync(ctx); // LLM unavailable → full fallback

        var intent = Str(triage, "intent", "general");
        var entities = StrList(triage, "entities");
        var sentiment = new AiSentiment
        {
            Label = triage.Value.TryGetProperty("sentiment", out var s) ? Str(s, "label", "Neutral") : "Neutral",
            Score = triage.Value.TryGetProperty("sentiment", out var s2) && s2.TryGetProperty("score", out var sc) && sc.TryGetDouble(out var d) ? Math.Round(d, 2) : 0,
            Urgency = triage.Value.TryGetProperty("sentiment", out var s3) ? Str(s3, "urgency", "Normal") : "Normal",
        };
        var searchQuery = Str(triage, "searchQuery", $"{ctx.Subject} {ctx.LastCustomerMessage}");
        pipeline.Add("Triage agent (NLP): intent, entities, sentiment");

        // 2. Retrieval (RAG)
        var articles = await _retriever.RetrieveAsync(searchQuery, 3);
        var sources = articles.Select(a => a.Title).ToList();
        var kb = articles.Count > 0
            ? string.Join("\n\n", articles.Select(a => $"• {a.Title}: {a.Content}"))
            : "(no specific policy found; answer generally and do not invent policy)";
        pipeline.Add($"Retrieval (RAG): {articles.Count} policy snippet(s)");

        // 3. Drafting agent — grounded in retrieved policy
        var draftUser = $"{ctx.Render()}\n\nDetected intent: {intent}\n\nRETRIEVED BANK POLICY (ground your specifics in this; do not invent policy):\n{kb}";
        var draft = await _groq.CompleteJsonAsync(DraftPrompt, draftUser);
        if (draft == null) return await _fallback.AnalyzeAsync(ctx);
        var summary = Str(draft, "summary", "");
        var reply = Str(draft, "suggestedReply", "");
        pipeline.Add("Drafting agent: policy-grounded reply");

        // 4. Next-best-action agent
        var actionUser = $"{ctx.Render()}\n\nIntent: {intent}\nRelevant policy:\n{kb}";
        var actionRes = await _groq.CompleteJsonAsync(ActionPrompt, actionUser);
        var actions = actionRes != null ? StrList(actionRes, "nextBestActions") : new();
        if (actions.Count == 0) actions = DefaultActions(ctx);
        pipeline.Add("Next-best-action agent");

        return new AiAnalysis
        {
            Summary = summary,
            Sentiment = sentiment,
            SuggestedReply = reply,
            NextBestActions = actions,
            Intent = intent,
            Entities = entities,
            Sources = sources,
            Pipeline = pipeline,
            Provider = $"groq:{_groq.Model} · multi-agent + RAG"
        };
    }

    private static List<string> DefaultActions(AiContext ctx)
    {
        var a = new List<string>();
        if (ctx.MissingDocuments.Count > 0) a.Add($"Request the missing {ctx.MissingDocuments[0]}");
        if (ctx.Status == "UnderReview") a.Add("Reassure the customer of the 2–3 business day review window");
        a.Add("Acknowledge the query and confirm next steps");
        return a;
    }

    // --- JSON helpers ---
    private static string Str(JsonElement e, string prop, string fallback) =>
        e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? fallback : fallback;
    private static string Str(JsonElement? e, string prop, string fallback) => e == null ? fallback : Str(e.Value, prop, fallback);
    private static List<string> StrList(JsonElement e, string prop)
    {
        if (!e.TryGetProperty(prop, out var v) || v.ValueKind != JsonValueKind.Array) return new();
        return v.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToList();
    }
    private static List<string> StrList(JsonElement? e, string prop) => e == null ? new() : StrList(e.Value, prop);

    private const string TriagePrompt = """
        You are the triage agent in a bank support pipeline. Do NLP analysis of the ticket.
        Return ONLY JSON:
        {
          "intent": "one of: status_check, document_help, account_change, complaint, product_info, general",
          "entities": ["short phrases actually mentioned: product, amounts, dates, document types"],
          "sentiment": { "label": "Negative|Neutral|Positive", "score": -1.0 to 1.0, "urgency": "Low|Normal|High|Urgent" },
          "searchQuery": "a short keyword query to search the bank policy knowledge base for this ticket"
        }
        """;

    private const string DraftPrompt = """
        You are the drafting agent for a bank support AGENT (you write a draft the human will send to the customer).
        Use the application context and the RETRIEVED BANK POLICY to write the reply. Ground every specific
        (timelines, required documents, procedures) in the retrieved policy — do NOT invent policy or promise approval.
        Return ONLY JSON: { "summary": "2-3 sentence summary of the ticket", "suggestedReply": "the draft reply" }
        """;

    private const string ActionPrompt = """
        You are the operations agent. Based on the ticket, the application status, and the bank policy, list 2-4
        concrete next-best actions for the SUPPORT AGENT (not the customer) to take. Be specific and policy-aligned.
        Return ONLY JSON: { "nextBestActions": ["action 1", "action 2"] }
        """;
}

/// <summary>Deterministic offline assistant — realistic multi-field output without any API key.</summary>
public class MockAiAssistant : IAiAssistant
{
    private readonly IKnowledgeRetriever _retriever;
    public MockAiAssistant(IKnowledgeRetriever retriever) => _retriever = retriever;

    private static readonly string[] Neg = { "angry", "frustrated", "unacceptable", "worst", "terrible", "delay", "stuck", "still waiting", "weeks", "ridiculous", "complaint", "escalate", "disappointed", "urgent", "asap" };
    private static readonly string[] Pos = { "thank", "thanks", "great", "appreciate", "happy", "excellent" };

    public async Task<AiAnalysis> AnalyzeAsync(AiContext ctx)
    {
        var text = string.Join(" ", ctx.Messages.Select(m => m.Body)).ToLowerInvariant();
        int neg = Neg.Count(text.Contains), pos = Pos.Count(text.Contains);
        var (label, score, urgency) = neg > pos ? ("Negative", -0.3 - Math.Min(neg, 4) * 0.15, neg >= 2 ? "High" : "Normal")
            : pos > neg ? ("Positive", 0.5, "Low") : ("Neutral", 0.0, "Normal");

        var intent = text.Contains("status") || text.Contains("pending") || text.Contains("review") ? "status_check"
            : text.Contains("document") || text.Contains("upload") || text.Contains("reupload") ? "document_help"
            : text.Contains("update") || text.Contains("change") || text.Contains("phone") || text.Contains("address") ? "account_change"
            : neg >= 2 ? "complaint" : "general";

        var articles = await _retriever.RetrieveAsync($"{ctx.Subject} {ctx.LastCustomerMessage}", 3);
        var sources = articles.Select(a => a.Title).ToList();
        var policy = articles.FirstOrDefault()?.Content;

        var actions = new List<string>();
        if (ctx.MissingDocuments.Count > 0) actions.Add($"Request the missing {ctx.MissingDocuments[0]}");
        if (ctx.Status == "UnderReview") actions.Add("Reassure the customer of the 2–3 business day review window");
        if (label == "Negative") actions.Add("Prioritise and consider escalating to a senior agent");
        if (actions.Count == 0) actions.Add("Acknowledge the query and confirm next steps");

        var appLine = ctx.ProductName != null ? $"your {ctx.ProductName} application (currently {ctx.Status})" : "your request";
        var reply = $"Hi {ctx.CustomerName.Split(' ').FirstOrDefault()},\n\nThanks for reaching out about {appLine}. " +
            (ctx.MissingDocuments.Count > 0
                ? $"To move things forward we still need your {string.Join(" and ", ctx.MissingDocuments)} — once uploaded, our team will review within 2–3 business days."
                : (policy != null ? policy.Split('.').FirstOrDefault() + ". " : "Our team is reviewing it and you'll be notified as soon as there's an update.")) +
            "\n\nWe appreciate your patience and are here if you have any other questions.\n\nBest regards,\nVeriqo Support";

        return new AiAnalysis
        {
            Summary = $"Customer raised a {ctx.Category.ToLower()} ticket" + (ctx.ProductName != null ? $" about their {ctx.ProductName} ({ctx.Status})" : "") + $". Subject: \"{ctx.Subject}\".",
            Sentiment = new AiSentiment { Label = label, Score = Math.Round(score, 2), Urgency = urgency },
            SuggestedReply = reply,
            NextBestActions = actions,
            Intent = intent,
            Entities = ctx.ProductName != null ? new() { ctx.ProductName } : new(),
            Sources = sources,
            Pipeline = new() { "Triage (rules)", $"Retrieval (RAG): {articles.Count} snippet(s)", "Draft (template)", "Next-best-action" },
            Provider = "mock · multi-agent + RAG"
        };
    }
}
