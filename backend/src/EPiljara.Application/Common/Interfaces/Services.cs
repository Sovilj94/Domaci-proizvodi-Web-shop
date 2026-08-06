using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;

namespace EPiljara.Application.Common.Interfaces;

/// <summary>The authenticated principal for the current request (implemented in the API layer).</summary>
public interface ICurrentUser
{
    Guid? Id { get; }
    string? Email { get; }
    UserRole? Role { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(UserRole role);
}

public interface IJwtTokenService
{
    /// <summary>Issues a role-scoped JWT (scope: shop for Customer/Admin, driver for Driver).</summary>
    string CreateToken(User user);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface IEmailService
{
    Task SendOrderConfirmationAsync(Order order, CancellationToken cancellationToken = default);

    /// <summary>Sends the address-confirmation email at registration. The full link is built from the token.</summary>
    Task SendEmailConfirmationAsync(User user, string token, CancellationToken cancellationToken = default);
}

/// <summary>Input for route optimization — one entry per delivery stop.</summary>
public record RouteStopInput(Guid OrderId, string Address, string ZoneName, double? Latitude, double? Longitude);

/// <summary>
/// Produces an optimized visiting order for a set of stops. The default implementation
/// groups by zone; a Google Routes-backed implementation can replace it without changes elsewhere.
/// </summary>
public interface IRouteOptimizer
{
    Task<IReadOnlyList<Guid>> OptimizeAsync(
        IReadOnlyList<RouteStopInput> stops,
        CancellationToken cancellationToken = default);
}
