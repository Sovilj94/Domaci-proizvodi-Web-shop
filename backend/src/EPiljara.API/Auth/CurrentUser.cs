using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Enums;

namespace EPiljara.API.Auth;

/// <summary>Reads the authenticated principal from the current HTTP request's JWT claims.</summary>
public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public Guid? Id =>
        Guid.TryParse(Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;

    public string? Email => Principal?.FindFirstValue(JwtRegisteredClaimNames.Email);

    public UserRole? Role =>
        Enum.TryParse<UserRole>(Principal?.FindFirstValue(ClaimTypes.Role), out var role) ? role : null;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public bool IsInRole(UserRole role) => Role == role;

    /// <summary>`X-Pismo: cir` (bez obzira na velika slova) je ćirilica; sve ostalo, i odsustvo, latinica.</summary>
    public Pismo Pismo =>
        string.Equals(accessor.HttpContext?.Request.Headers["X-Pismo"].ToString(), "cir", StringComparison.OrdinalIgnoreCase)
            ? Pismo.Cirilica
            : Pismo.Latinica;
}
