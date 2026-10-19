using System.ComponentModel.DataAnnotations;

namespace Veriqo.Api.Dtos;

public class CreateApplicationRequest
{
    [Required] public string ProductKey { get; set; } = string.Empty;
    public Dictionary<string, object> Answers { get; set; } = new();
}

public class DecisionRequest
{
    [Required] public string Decision { get; set; } = string.Empty; // Approved, Rejected, ActionRequired
    public string Note { get; set; } = string.Empty;
}

public class DocumentReviewRequest
{
    [Required] public string Decision { get; set; } = string.Empty; // Verified, Rejected
    public string Reason { get; set; } = string.Empty;
}
