using System.Security.Cryptography;
using Veriqo.Api.Data;
using Veriqo.Api.Dtos;
using Veriqo.Api.Models;
using Veriqo.Api.Services;
using Veriqo.Api.Settings;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Veriqo.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly MongoDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly TokenService _tokens;
    private readonly IEmailService _email;
    private readonly AdminSettings _admin;
    private readonly GoogleSettings _google;
    private readonly IWebHostEnvironment _env;

    public AuthController(MongoDbContext db, UserManager<ApplicationUser> users, TokenService tokens,
        IEmailService email, IOptions<AdminSettings> admin, IOptions<GoogleSettings> google, IWebHostEnvironment env)
    {
        _db = db;
        _users = users;
        _tokens = tokens;
        _email = email;
        _admin = admin.Value;
        _google = google.Value;
        _env = env;
    }

    private bool IsAdminEmail(string email) =>
        string.Equals(email, _admin.Email.Trim(), StringComparison.OrdinalIgnoreCase);

    [HttpGet("config")]
    public IActionResult Config() => Ok(new
    {
        googleEnabled = !string.IsNullOrWhiteSpace(_google.ClientId),
        googleClientId = _google.ClientId
    });

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        if (IsAdminEmail(email))
            return BadRequest(new { message = "This email is reserved. Please use a different one." });

        if (await _users.FindByEmailAsync(email) != null)
            return Conflict(new { message = "An account with this email already exists." });

        var user = new ApplicationUser { UserName = email, Email = email, Role = Roles.Customer, EmailConfirmed = false };
        var result = await _users.CreateAsync(user, req.Password);
        if (!result.Succeeded)
            return BadRequest(new { message = result.Errors.First().Description });

        await _db.Profiles.InsertOneAsync(new CustomerProfile { UserId = user.Id });

        var (sent, devOtp) = await IssueOtpAsync(user);
        return Ok(new
        {
            requiresVerification = true,
            email = user.Email,
            emailSent = sent,
            devOtp // populated only when SMTP isn't configured (dev convenience)
        });
    }

    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp(VerifyOtpRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var user = await _users.FindByEmailAsync(email);
        if (user == null) return NotFound(new { message = "Account not found." });
        if (user.EmailConfirmed) return BadRequest(new { message = "Email already verified. Please sign in." });

        if (user.OtpExpiresAt == null || user.OtpExpiresAt < DateTime.UtcNow)
            return BadRequest(new { message = "Your code has expired. Please request a new one." });
        if (user.OtpAttempts >= 5)
            return BadRequest(new { message = "Too many attempts. Please request a new code." });

        if (user.OtpCode != req.Otp.Trim())
        {
            await _db.Users.UpdateOneAsync(u => u.Id == user.Id,
                Builders<ApplicationUser>.Update.Inc(u => u.OtpAttempts, 1));
            return BadRequest(new { message = "Incorrect code. Please try again." });
        }

        // success — confirm email, clear OTP
        await _db.Users.UpdateOneAsync(u => u.Id == user.Id, Builders<ApplicationUser>.Update
            .Set(u => u.EmailConfirmed, true)
            .Set(u => u.OtpCode, null)
            .Set(u => u.OtpExpiresAt, null)
            .Set(u => u.OtpAttempts, 0));

        var profile = await _db.Profiles.Find(p => p.UserId == user.Id).FirstOrDefaultAsync();
        return Ok(Auth(user, profile?.IsComplete ?? false));
    }

    [HttpPost("resend-otp")]
    public async Task<IActionResult> ResendOtp(ResendOtpRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var user = await _users.FindByEmailAsync(email);
        if (user == null) return NotFound(new { message = "Account not found." });
        if (user.EmailConfirmed) return BadRequest(new { message = "Email already verified. Please sign in." });

        var (sent, devOtp) = await IssueOtpAsync(user);
        return Ok(new { emailSent = sent, devOtp });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var user = await _users.FindByEmailAsync(email);
        if (user == null)
            return Unauthorized(new { message = "Invalid email or password." });

        if (await _users.IsLockedOutAsync(user))
            return StatusCode(423, new { message = "Account locked after too many attempts. Try again in a few minutes." });

        if (string.IsNullOrEmpty(user.PasswordHash) || !await _users.CheckPasswordAsync(user, req.Password))
        {
            await _users.AccessFailedAsync(user);
            return Unauthorized(new { message = "Invalid email or password." });
        }
        await _users.ResetAccessFailedCountAsync(user);

        // Email OTP is only required at sign-up. Sign-in is email + password only.
        var profile = await _db.Profiles.Find(p => p.UserId == user.Id).FirstOrDefaultAsync();
        return Ok(Auth(user, profile?.IsComplete ?? false));
    }

    [HttpPost("google")]
    public async Task<IActionResult> Google(GoogleLoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(_google.ClientId))
            return BadRequest(new { message = "Google sign-in is not configured on the server." });

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(req.Credential,
                new GoogleJsonWebSignature.ValidationSettings { Audience = new[] { _google.ClientId } });
        }
        catch { return Unauthorized(new { message = "Invalid Google token." }); }

        if (!payload.EmailVerified)
            return Unauthorized(new { message = "Your Google email is not verified." });

        var email = payload.Email.Trim().ToLowerInvariant();
        if (IsAdminEmail(email))
            return BadRequest(new { message = "Admin accounts must sign in with email and password." });

        var user = await _users.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = email, Email = email, EmailConfirmed = true, // Google already verified the address
                Role = Roles.Customer, Provider = "google"
            };
            await _users.CreateAsync(user);
            await _db.Profiles.InsertOneAsync(new CustomerProfile
            {
                UserId = user.Id, FullName = payload.Name ?? "", PictureUrl = payload.Picture ?? ""
            });
        }

        var profile = await _db.Profiles.Find(p => p.UserId == user.Id).FirstOrDefaultAsync();
        return Ok(Auth(user, profile?.IsComplete ?? false));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var user = await _users.FindByIdAsync(User.UserId());
        if (user == null) return Unauthorized();
        var profile = await _db.Profiles.Find(p => p.UserId == user.Id).FirstOrDefaultAsync();
        return Ok(new { email = user.Email, role = user.Role, profileComplete = profile?.IsComplete ?? false });
    }

    // ---- helpers ----

    private async Task<(bool sent, string? devOtp)> IssueOtpAsync(ApplicationUser user)
    {
        var otp = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        await _db.Users.UpdateOneAsync(u => u.Id == user.Id, Builders<ApplicationUser>.Update
            .Set(u => u.OtpCode, otp)
            .Set(u => u.OtpExpiresAt, DateTime.UtcNow.AddMinutes(10))
            .Set(u => u.OtpAttempts, 0));

        var sent = await _email.SendOtpAsync(user.Email, otp);
        // In dev, expose the code whenever the email didn't actually send (not configured,
        // or a send failure) so verification never dead-ends.
        var devOtp = (!sent && _env.IsDevelopment()) ? otp : null;
        return (sent, devOtp);
    }

    private AuthResponse Auth(ApplicationUser user, bool profileComplete) => new()
    {
        Token = _tokens.Create(user),
        Email = user.Email,
        Role = user.Role,
        ProfileComplete = profileComplete
    };
}
