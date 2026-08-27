using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Mapping;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Features.Admin;

/* ---------- Drivers ---------- */

public record GetDriversQuery : IRequest<IReadOnlyList<DriverDto>>;

public class GetDriversHandler(IAppDbContext db) : IRequestHandler<GetDriversQuery, IReadOnlyList<DriverDto>>
{
    public async Task<IReadOnlyList<DriverDto>> Handle(GetDriversQuery q, CancellationToken ct)
    {
        var drivers = await db.Drivers.OrderBy(d => d.Name).ToListAsync(ct);
        return drivers.Select(d => d.ToDto()).ToList();
    }
}

/* ---------- Routes for a date ---------- */

public record GetRoutesQuery(string Date) : IRequest<IReadOnlyList<DeliveryRouteDto>>;

public class GetRoutesHandler(IAppDbContext db) : IRequestHandler<GetRoutesQuery, IReadOnlyList<DeliveryRouteDto>>
{
    public async Task<IReadOnlyList<DeliveryRouteDto>> Handle(GetRoutesQuery q, CancellationToken ct)
    {
        if (!DateOnly.TryParse(q.Date, out var date))
            throw new DomainException("Neispravan datum.", "invalid_date");

        var routes = await db.DeliveryRoutes
            .Include(r => r.Driver)
            .Include(r => r.DeliveryZone)
            .Include(r => r.Stops).ThenInclude(s => s.Order).ThenInclude(o => o!.DeliveryZone)
            .Where(r => r.Date == date)
            .ToListAsync(ct);

        return routes.Select(r => r.ToDto()).ToList();
    }
}

/* ---------- Assign orders to a driver ---------- */

public record AssignRouteCommand(string Date, Guid DriverId, IReadOnlyList<Guid> OrderIds)
    : IRequest<IReadOnlyList<DeliveryRouteDto>>;

/// <summary>
/// Računa redoslijed i upisuje ga na stanice. Zatvorene stanice se ne diraju i
/// zadržavaju svoje brojeve; otvorene se prenumerišu počev od broja za jedan većeg
/// od najvećeg zatvorenog, da se brojevi ne bi sudarili i pokvarili sječenje na serije.
/// </summary>
internal static class Optimizacija
{
    private static readonly HashSet<OrderStatus> Zatvoreni =
        [OrderStatus.Delivered, OrderStatus.FailedDelivery, OrderStatus.Cancelled];

    public static async Task PrimijeniAsync(
        DeliveryRoute route, double startLat, double startLon,
        IRouteOptimizer optimizer, CancellationToken ct)
    {
        // Ako bar jedna stanica nema učitanu narudžbu, poziv je vjerovatno zaboravio
        // .ThenInclude(s => s.Order) na upitu koji je učitao rutu — OrderId je obavezan
        // strani ključ, pa Order stvarno null biti ne može. Provjera namjerno koristi
        // Any, ne All: djelimičan EF change-tracker fixup zna već pratiti poneku Order
        // (npr. iz ranijeg upita u istom kontekstu), pa bi All ispao false i propustio
        // guard. Filter ispod se oslanja na ovaj guard i koristi Order! bez provjere —
        // bez njega bi to bio NullReferenceException umjesto jasne poruke ovdje.
        if (route.Stops.Count > 0 && route.Stops.Any(s => s.Order is null))
            throw new InvalidOperationException(
                "Optimizacija.PrimijeniAsync zahtijeva rutu učitanu sa " +
                ".Include(r => r.Stops).ThenInclude(s => s.Order) — narudžbe nisu učitane.");

        // Guard iznad je već odbacio svaku null Order — ovdje se Order! bezbjedno koristi.
        var otvorene = route.Stops
            .Where(s => !Zatvoreni.Contains(s.Order!.Status))
            .ToList();

        if (otvorene.Count < 2) return;

        var inputs = otvorene.Select(s => new RouteStopInput(
            s.OrderId,
            s.Order?.DeliveryAddress ?? string.Empty,
            s.Order?.DeliveryZone?.Name ?? string.Empty,
            s.Order?.Latitude,
            s.Order?.Longitude)).ToList();

        var rezultat = await optimizer.OptimizeAsync(startLat, startLon, inputs, ct);

        var pocetak = route.Stops
            .Where(s => s.Order is not null && Zatvoreni.Contains(s.Order.Status))
            .Select(s => s.Sequence)
            .DefaultIfEmpty(0)
            .Max() + 1;

        // Po instanci stanice, ne po OrderId: da dupli OrderId u rezultat.Order (npr.
        // stigao iz nedovoljno deduplikovanog unosa) ne pogodi istu stanicu dvaput i
        // ostavi drugu sa zastarjelim brojem koji se sudara sa sljedećom. Stanica koja
        // se nikad ne pojavi u rezultatu — današnji optimizatori uvijek vraćaju
        // kompletan skup, ali buduci ne mora — ipak dobije broj, na kraju niza.
        var dodijeljene = new HashSet<RouteStop>();
        foreach (var orderId in rezultat.Order)
        {
            var stop = otvorene.FirstOrDefault(s => s.OrderId == orderId && !dodijeljene.Contains(s));
            if (stop is null) continue;
            stop.Sequence = pocetak++;
            dodijeljene.Add(stop);
        }

        foreach (var stop in otvorene.Where(s => !dodijeljene.Contains(s)))
            stop.Sequence = pocetak++;

        route.DuzinaMetara = rezultat.DuzinaMetara;
        route.TrajanjeSekundi = rezultat.TrajanjeSekundi;
        route.Geometrija = rezultat.Geometrija;

        // Status "Optimized" samo kad je stvarno optimizovano. Rezerva po zonama
        // vraća redoslijed ali nema kilometražu — kad bismo i tada pisali Optimized,
        // ništa u sistemu ne bi razlikovalo pravu rutu od degradirane.
        if (rezultat.DuzinaMetara.HasValue) route.Status = RouteStatus.Optimized;
    }
}

public class AssignRouteHandler(IAppDbContext db, IRouteOptimizer optimizer, IOptions<Magacin> magacin)
    : IRequestHandler<AssignRouteCommand, IReadOnlyList<DeliveryRouteDto>>
{
    /// <summary>Google Maps prima najviše ovoliko koordinata po linku — tvrda granica
    /// spoljnog alata (isto ograničenje koje je NavigacijaLink.MaxStanica već kodirao u
    /// aplikaciji vozača), ne estetika.</summary>
    private const int MaxStanicaPoRuti = 10;
    private const string NazivBezZone = "Bez zone";

    private static readonly HashSet<OrderStatus> Zatvoreni =
        [OrderStatus.Delivered, OrderStatus.FailedDelivery, OrderStatus.Cancelled];

    public async Task<IReadOnlyList<DeliveryRouteDto>> Handle(AssignRouteCommand cmd, CancellationToken ct)
    {
        if (!DateOnly.TryParse(cmd.Date, out var date))
            throw new DomainException("Neispravan datum.", "invalid_date");

        var driver = await db.Drivers.FirstOrDefaultAsync(d => d.Id == cmd.DriverId, ct)
            ?? throw new NotFoundException("Vozač nije pronađen.");

        // Distinct: dupli Id u zahtjevu ne smije napraviti dvije stanice za istu narudžbu.
        var trazeneIds = cmd.OrderIds.Distinct().ToList();
        var narudzbe = await db.Orders
            .Include(o => o.DeliveryZone)
            .Where(o => trazeneIds.Contains(o.Id))
            // Stabilan redoslijed sječenja na komade od najviše 10 — bez ovoga bi dva
            // uzastopna poziva mogla razdvojiti iste narudžbe drugačije. Id kao tie-breaker
            // jer CreatedAt teoretski može biti isti za dvije narudžbe.
            .OrderBy(o => o.CreatedAt).ThenBy(o => o.Id)
            .ToListAsync(ct);

        // Postojeće rute ovog vozača za ovaj dan. Zatvorene stanice (Delivered,
        // FailedDelivery, Cancelled) se NE premještaju ni brišu — istorija mora ostati
        // tačna. Otvorene stanice se brišu jer se ruta gradi iznova iz zahtjeva; ruta koja
        // poslije toga ostane bez ijedne stanice se briše — prazna ruta nema smisla. Ruta
        // koja zadrži bar jednu zatvorenu stanicu ostaje u bazi netaknuta.
        var postojece = await db.DeliveryRoutes
            .Include(r => r.Stops).ThenInclude(s => s.Order)
            .Where(r => r.DriverId == driver.Id && r.Date == date)
            .ToListAsync(ct);

        var zatvoreneOrderIds = new HashSet<Guid>();
        foreach (var ruta in postojece)
        {
            var otvoreneStanice = ruta.Stops.Where(s => !Zatvoreni.Contains(s.Order!.Status)).ToList();
            foreach (var s in ruta.Stops.Where(s => Zatvoreni.Contains(s.Order!.Status)))
                zatvoreneOrderIds.Add(s.OrderId);

            foreach (var s in otvoreneStanice)
                db.RouteStops.Remove(s);

            if (otvoreneStanice.Count == ruta.Stops.Count)
                db.DeliveryRoutes.Remove(ruta); // ništa zatvoreno nije ostalo — prazna ruta ide
        }

        // Narudžba koja već ima zatvorenu stanicu negdje ovog dana ne dobija novu — istorija
        // je već napisana za nju, dupliranje bi je pomjerilo iz prošlosti u novu rutu.
        var zaRasporedjivanje = narudzbe.Where(o => !zatvoreneOrderIds.Contains(o.Id)).ToList();

        var noveRute = new List<DeliveryRoute>();
        foreach (var grupa in zaRasporedjivanje.GroupBy(o => o.DeliveryZoneId))
        {
            // DeliveryZoneId je non-nullable na Order u trenutnim pravilima, pa "Bez zone"
            // u praksi ne nastaje — grana ostaje kao odbrana za stariju bazu bez tog pravila.
            var bazniNaziv = grupa.First().DeliveryZone?.Name ?? NazivBezZone;
            var komadi = grupa.Chunk(MaxStanicaPoRuti).ToList();

            for (var i = 0; i < komadi.Count; i++)
            {
                noveRute.Add(new DeliveryRoute
                {
                    Date = date,
                    DriverId = driver.Id,
                    DeliveryZoneId = grupa.Key,
                    // Redni broj samo kad je zona podijeljena na više komada.
                    Naziv = komadi.Count > 1 ? $"{bazniNaziv} {i + 1}" : bazniNaziv,
                    Status = RouteStatus.Planned,
                    Stops = komadi[i]
                        .Select((o, idx) => new RouteStop { OrderId = o.Id, Sequence = idx + 1 })
                        .ToList(),
                });
            }
        }

        db.DeliveryRoutes.AddRange(noveRute);
        await db.SaveChangesAsync(ct);

        // Optimizacija radi po ruti, svaka iz magacina — ponovo učitaj sa narudžbama, jer
        // AddRange gore nije učitao koordinate i statuse potrebne optimizatoru.
        var noviIds = noveRute.Select(r => r.Id).ToList();
        var saStavkama = await db.DeliveryRoutes
            .Include(r => r.Stops).ThenInclude(s => s.Order).ThenInclude(o => o!.DeliveryZone)
            .Where(r => noviIds.Contains(r.Id))
            .ToListAsync(ct);

        foreach (var ruta in saStavkama)
            await Optimizacija.PrimijeniAsync(
                ruta, magacin.Value.Latitude, magacin.Value.Longitude, optimizer, ct);
        await db.SaveChangesAsync(ct);

        return await LoadDriverDateRoutesAsync(db, driver.Id, date, ct);
    }

    internal static async Task<DeliveryRouteDto> LoadRouteDtoAsync(IAppDbContext db, Guid routeId, CancellationToken ct)
    {
        var route = await db.DeliveryRoutes
            .Include(r => r.Driver)
            .Include(r => r.DeliveryZone)
            .Include(r => r.Stops).ThenInclude(s => s.Order).ThenInclude(o => o!.DeliveryZone)
            .FirstAsync(r => r.Id == routeId, ct);
        return route.ToDto();
    }

    /// <summary>Sve rute vozača za dati dan, poređane po nazivu — isti redoslijed kao
    /// GET /driver/routes, da odgovor dodjele odmah pokaže konačno stanje.</summary>
    internal static async Task<IReadOnlyList<DeliveryRouteDto>> LoadDriverDateRoutesAsync(
        IAppDbContext db, Guid driverId, DateOnly date, CancellationToken ct)
    {
        var rute = await db.DeliveryRoutes
            .Include(r => r.Driver)
            .Include(r => r.DeliveryZone)
            .Include(r => r.Stops).ThenInclude(s => s.Order).ThenInclude(o => o!.DeliveryZone)
            .Where(r => r.DriverId == driverId && r.Date == date)
            .OrderBy(r => r.Naziv)
            .ToListAsync(ct);
        return rute.Select(r => r.ToDto()).ToList();
    }
}

/* ---------- Optimize sequence ---------- */

public record OptimizeRouteCommand(Guid RouteId) : IRequest<DeliveryRouteDto>;

/// <summary>Polazište tura. Jedna piljara, jedan magacin, pa stoji u konfiguraciji.
/// Obična klasa sa set svojstvima, ne pozicioni record — AddOptions traži new().</summary>
public class Magacin
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public class OptimizeRouteHandler(IAppDbContext db, IRouteOptimizer optimizer, IOptions<Magacin> magacin)
    : IRequestHandler<OptimizeRouteCommand, DeliveryRouteDto>
{
    public async Task<DeliveryRouteDto> Handle(OptimizeRouteCommand cmd, CancellationToken ct)
    {
        var route = await db.DeliveryRoutes
            .Include(r => r.Stops).ThenInclude(s => s.Order).ThenInclude(o => o!.DeliveryZone)
            .FirstOrDefaultAsync(r => r.Id == cmd.RouteId, ct)
            ?? throw new NotFoundException("Ruta nije pronađena.");

        await Optimizacija.PrimijeniAsync(
            route, magacin.Value.Latitude, magacin.Value.Longitude, optimizer, ct);
        await db.SaveChangesAsync(ct);

        return await AssignRouteHandler.LoadRouteDtoAsync(db, route.Id, ct);
    }
}
