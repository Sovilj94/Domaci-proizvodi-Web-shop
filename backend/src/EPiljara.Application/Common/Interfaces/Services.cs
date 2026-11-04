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

    /// <summary>
    /// Pismo kojim tekući zahtjev traži odgovor (zaglavlje <c>X-Pismo</c>). Bez zahtjeva —
    /// pozadinski posao — uvijek latinica; posao pretplata zato čita pismo vlasnika sa naloga.
    /// </summary>
    Pismo Pismo { get; }
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

    /// <summary>Šalje gostu link za potvrdu narudžbe. Link se gradi iz tokena zahtjeva.</summary>
    Task SendGuestOrderRequestAsync(GuestOrderRequest request, CancellationToken cancellationToken = default);

    /// <summary>A2: pretplatniku ove sedmice nije poslata korpica (sadržaj nije objavljen ili korpica nije u ponudi).</summary>
    Task SendPreskocenaIsporukaAsync(User kupac, string korpica, DateTime isporuka, bool korpicaUPonudi = true, CancellationToken cancellationToken = default);

    /// <summary>A2b: adminima — korpice sa aktivnim pretplatama kojima sadržaj ciljne sedmice nije objavljen, a rok se bliži.</summary>
    Task SendAlarmSadrzajaAsync(IReadOnlyList<User> admini, IReadOnlyList<(string Korpica, int Pretplata)> stavke, DateTime rok, CancellationToken cancellationToken = default);

    /// <summary>Kupcu: narudžba je potvrđena, isporučena, nije uručena ili otkazana (automatski = nije potvrđena do roka).</summary>
    Task SendStatusNarudzbeAsync(Order order, OrderStatus status, bool automatski = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Radnicima i vozačima: kratko obavještenje (naslov, redovi teksta, dugme sa putanjom u aplikaciji).
    /// Ključ sprečava da isti događaj ode dvaput (npr. "plan:{vozac}:{datum}").
    /// </summary>
    Task SendObavjestenjeAsync(IReadOnlyList<string> primaoci, string vrsta, string naslov, IReadOnlyList<string> redovi,
        string? dugme = null, string? putanja = null, string? kljuc = null, CancellationToken cancellationToken = default);

    /// <summary>Link za novu lozinku (token je jednokratan, važi 1 h).</summary>
    Task SendResetLozinkeAsync(User user, string token, CancellationToken cancellationToken = default);

    /// <summary>Ručno obavještenje grupi; primalac sa Odjava=true dobija link za odjavu.</summary>
    Task SendKampanjaAsync(EmailKampanja kampanja, IReadOnlyList<PrimalacKampanje> primaoci, CancellationToken cancellationToken = default);

    /// <summary>Probni mejl sa stranice Mejlovi — provjera da dobavljač radi.</summary>
    Task SendProbniAsync(string primalac, CancellationToken cancellationToken = default);
}

public sealed record PrimalacKampanje(string Email, string? Ime, bool Odjava, Pismo Pismo = Pismo.Latinica);

/* ---------- Slanje mejlova iz reda ---------- */

public sealed record EmailZaSlanje(Guid Id, string Primalac, string? PrimalacIme, string Naslov, string Html);

/// <summary>Trajna = dobavljač je odbio poruku (npr. neispravna adresa); ponovni pokušaj ne bi pomogao.</summary>
public sealed record EmailRezultat(bool Uspjeh, string? DobavljacId = null, string? Greska = null, bool Trajna = false);

/// <summary>Kako mejl stvarno odlazi: SMTP (dev Mailpit) ili HTTP API dobavljača (Brevo) — bira konfiguracija.</summary>
public interface IEmailTransport
{
    string Naziv { get; }
    Task<EmailRezultat> PosaljiAsync(EmailZaSlanje poruka, CancellationToken ct);
}

/// <summary>Budi posao slanja čim je mejl upisan u red, da kupac ne čeka sljedeći krug.</summary>
public interface IEmailSignal
{
    void Probudi();
    Task CekajAsync(TimeSpan najduze, CancellationToken ct);
}

/// <summary>Potpis adrese za link „Odjavi se" — bez tabele tokena, a link ne može da se izmisli za tuđu adresu.</summary>
public interface IOdjavaPotpis
{
    string Potpisi(string email);
    bool Provjeri(string email, string potpis);
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
