using System.Security.Claims;

namespace Veriqo.Api.Services;

public static class ClaimsHelper
{
    public static string UserId(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    public static string Email(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
}
