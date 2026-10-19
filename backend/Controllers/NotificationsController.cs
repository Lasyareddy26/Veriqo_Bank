using Veriqo.Api.Data;
using Veriqo.Api.Models;
using Veriqo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace Veriqo.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly MongoDbContext _db;
    public NotificationsController(MongoDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var items = await _db.Notifications.Find(n => n.UserId == User.UserId())
            .SortByDescending(n => n.CreatedAt).Limit(50).ToListAsync();
        return Ok(items);
    }

    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkRead(string id)
    {
        await _db.Notifications.UpdateOneAsync(
            n => n.Id == id && n.UserId == User.UserId(),
            Builders<Notification>.Update.Set(n => n.IsRead, true));
        return Ok();
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        await _db.Notifications.UpdateManyAsync(
            n => n.UserId == User.UserId() && !n.IsRead,
            Builders<Notification>.Update.Set(n => n.IsRead, true));
        return Ok();
    }
}
