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
/// Rezultat optimizacije. Kilometraža, trajanje i geometrija su nullable jer
/// rezervno sortiranje po zonama ne zna ništa o putevima — vraća samo redoslijed.
/// </summary>
public record RouteOptimizationResult(
    IReadOnlyList<Guid> Order,
    double? DuzinaMetara,
    double? TrajanjeSekundi,
    string? Geometrija);

/// <summary>
/// Računa redoslijed obilaska stanica, počev od zadatog polazišta.
/// Polazište je magacin, ili trenutna pozicija vozača kod ručne ponovne optimizacije.
/// </summary>
public interface IRouteOptimizer
{
    Task<RouteOptimizationResult> OptimizeAsync(
        double startLat,
        double startLon,
        IReadOnlyList<RouteStopInput> stops,
        CancellationToken cancellationToken = default);
}
