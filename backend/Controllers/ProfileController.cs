using Veriqo.Api.Data;
using Veriqo.Api.Dtos;
using Veriqo.Api.Models;
using Veriqo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace Veriqo.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/profile")]
public class ProfileController : ControllerBase
{
    private readonly MongoDbContext _db;
    public ProfileController(MongoDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var profile = await _db.Profiles.Find(p => p.UserId == User.UserId()).FirstOrDefaultAsync();
        return Ok(profile ?? new CustomerProfile { UserId = User.UserId() });
    }

    [HttpPut]
    public async Task<IActionResult> Update(ProfileRequest req)
    {
        var age = (DateTime.UtcNow - req.DateOfBirth).TotalDays / 365.25;
        if (age < 18) return BadRequest(new { message = "You must be at least 18 years old." });

        var profile = await _db.Profiles.Find(p => p.UserId == User.UserId()).FirstOrDefaultAsync()
                      ?? new CustomerProfile { UserId = User.UserId() };

        profile.FullName = req.FullName.Trim();
        // Treat the incoming calendar date as a plain UTC date so it isn't shifted
        // by the server's local timezone when MongoDB stores it.
        profile.DateOfBirth = DateTime.SpecifyKind(req.DateOfBirth.Date, DateTimeKind.Utc);
        profile.Gender = req.Gender;
        profile.Nationality = req.Nationality;
        profile.GovernmentIdNumber = req.GovernmentIdNumber.Trim();
        profile.Phone = req.Phone.Trim();
        profile.Address = req.Address;
        profile.EmploymentStatus = req.EmploymentStatus;
        profile.EmployerName = req.EmployerName;
        profile.AnnualIncome = req.AnnualIncome;
        profile.IsComplete = true;
        profile.UpdatedAt = DateTime.UtcNow;

        await _db.Profiles.ReplaceOneAsync(
            p => p.UserId == User.UserId(), profile,
            new ReplaceOptions { IsUpsert = true });

        return Ok(profile);
    }
}
