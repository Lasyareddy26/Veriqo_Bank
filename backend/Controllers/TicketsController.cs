using Veriqo.Api.Data;
using Veriqo.Api.Dtos;
using Veriqo.Api.Models;
using Veriqo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace Veriqo.Api.Controllers;

[ApiController]
[Authorize(Roles = Roles.Customer)]
[Route("api/tickets")]
public class TicketsController : ControllerBase
{
    private readonly MongoDbContext _db;
    private readonly NotificationService _notify;

    public TicketsController(MongoDbContext db, NotificationService notify)
    {
        _db = db;
        _notify = notify;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateTicketRequest req)
    {
        var ticket = new Ticket
        {
            UserId = User.UserId(),
            Subject = req.Subject.Trim(),
            Category = string.IsNullOrWhiteSpace(req.Category) ? "General" : req.Category,
            ApplicationId = string.IsNullOrWhiteSpace(req.ApplicationId) ? null : req.ApplicationId,
            Status = TicketStatus.Open,
            Messages = new()
            {
                new TicketMessage { Sender = "Customer", SenderEmail = User.Email(), Body = req.Message.Trim() }
            }
        };
        await _db.Tickets.InsertOneAsync(ticket);
        return Ok(new { id = ticket.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Mine()
    {
        var tickets = await _db.Tickets.Find(t => t.UserId == User.UserId())
            .SortByDescending(t => t.UpdatedAt).ToListAsync();
        return Ok(tickets);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var t = await _db.Tickets.Find(x => x.Id == id && x.UserId == User.UserId()).FirstOrDefaultAsync();
        return t == null ? NotFound() : Ok(t);
    }

    [HttpPost("{id}/messages")]
    public async Task<IActionResult> AddMessage(string id, TicketMessageRequest req)
    {
        var t = await _db.Tickets.Find(x => x.Id == id && x.UserId == User.UserId()).FirstOrDefaultAsync();
        if (t == null) return NotFound();
        if (t.Status == TicketStatus.Closed)
            return BadRequest(new { message = "This ticket is closed. Please raise a new one." });

        t.Messages.Add(new TicketMessage { Sender = "Customer", SenderEmail = User.Email(), Body = req.Body.Trim() });
        t.Status = TicketStatus.Open; // customer replied → back to the agent queue
        t.UpdatedAt = DateTime.UtcNow;
        await _db.Tickets.ReplaceOneAsync(x => x.Id == id, t);
        return Ok();
    }
}
