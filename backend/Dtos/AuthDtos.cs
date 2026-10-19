using System.ComponentModel.DataAnnotations;

namespace Veriqo.Api.Dtos;

public class RegisterRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(8)]
    public string Password { get; set; } = string.Empty;
}

public class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class GoogleLoginRequest
{
    [Required]
    public string Credential { get; set; } = string.Empty;
}

public class VerifyOtpRequest
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required] public string Otp { get; set; } = string.Empty;
}

public class ResendOtpRequest
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
}

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool ProfileComplete { get; set; }
}
