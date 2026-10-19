using System.Text.RegularExpressions;
using Veriqo.Api.Data;
using Veriqo.Api.Models;
using MongoDB.Driver;

namespace Veriqo.Api.Services;

public interface IKnowledgeRetriever
{
    Task<List<KnowledgeArticle>> RetrieveAsync(string query, int k = 3);
}

/// <summary>
/// RAG retrieval over the bank's policy/FAQ knowledge base using TF-IDF keyword scoring.
/// Interface-based so a semantic (embeddings) retriever can replace it without touching the
/// assistant. Title and tag matches are weighted higher than body matches.
/// </summary>
public class KeywordRetriever : IKnowledgeRetriever
{
    private readonly MongoDbContext _db;
    public KeywordRetriever(MongoDbContext db) => _db = db;

    private static readonly HashSet<string> Stop = new(StringComparer.OrdinalIgnoreCase)
    {
        "the","a","an","and","or","of","to","for","in","on","is","are","my","i","you","your","it","this","that",
        "with","how","can","do","does","please","we","our","us","be","will","at","as","was","not","but","if","so",
        "me","have","has","had","what","when","why","which","about","from","they","them","their"
    };

    public async Task<List<KnowledgeArticle>> RetrieveAsync(string query, int k = 3)
    {
        var articles = await _db.Knowledge.Find(_ => true).ToListAsync();
        if (articles.Count == 0) return new();

        var qTerms = Tokenize(query).Distinct().ToList();
        if (qTerms.Count == 0) return new();

        // document frequency for IDF
        int n = articles.Count;
        var df = new Dictionary<string, int>();
        var bags = articles.Select(a => new
        {
            Article = a,
            Body = TermFreq(Tokenize($"{a.Content}")),
            Boost = TermFreq(Tokenize($"{a.Title} {string.Join(' ', a.Tags)}")),
        }).ToList();
        foreach (var b in bags)
            foreach (var t in b.Body.Keys.Concat(b.Boost.Keys).Distinct())
                df[t] = df.GetValueOrDefault(t) + 1;

        double Idf(string t) => Math.Log((n + 1.0) / (df.GetValueOrDefault(t) + 1) + 1);

        var scored = bags.Select(b =>
        {
            double score = 0;
            foreach (var t in qTerms)
            {
                var idf = Idf(t);
                if (b.Body.TryGetValue(t, out var tf)) score += (1 + Math.Log(tf)) * idf;
                if (b.Boost.TryGetValue(t, out var tfb)) score += 2.2 * (1 + Math.Log(tfb)) * idf; // title/tags weigh more
            }
            return (b.Article, score);
        })
        .Where(x => x.score > 0)
        .OrderByDescending(x => x.score)
        .Take(k)
        .Select(x => x.Article)
        .ToList();

        return scored;
    }

    private static List<string> Tokenize(string text) =>
        Regex.Split(text.ToLowerInvariant(), @"[^a-z0-9]+")
            .Where(w => w.Length > 2 && !Stop.Contains(w)).ToList();

    private static Dictionary<string, int> TermFreq(IEnumerable<string> tokens)
    {
        var d = new Dictionary<string, int>();
        foreach (var t in tokens) d[t] = d.GetValueOrDefault(t) + 1;
        return d;
    }
}
