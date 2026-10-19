using System.ComponentModel.DataAnnotations;

namespace Veriqo.Api.Dtos;

public class CreateTicketRequest
{
    [Required] public string Subject { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string? ApplicationId { get; set; }
    [Required] public string Message { get; set; } = string.Empty;
}

public class TicketMessageRequest
{
    [Required] public string Body { get; set; } = string.Empty;
}

public class TicketStatusRequest
{
    [Required] public string Status { get; set; } = string.Empty;
}

public class AgentReplyRequest
{
    [Required] public string Body { get; set; } = string.Empty;
    public string? Status { get; set; }
}
