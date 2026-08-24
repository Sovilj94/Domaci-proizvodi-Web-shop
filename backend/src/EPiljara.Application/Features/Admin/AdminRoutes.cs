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
            .Include(r => r.Stops).ThenInclude(s => s.Order).ThenInclude(o => o!.DeliveryZone)
            .Where(r => r.Date == date)
            .ToListAsync(ct);

        return routes.Select(r => r.ToDto()).ToList();
    }
}

/* ---------- Assign orders to a driver ---------- */

public record AssignRouteCommand(string Date, Guid DriverId, IReadOnlyList<Guid> OrderIds)
    : IRequest<DeliveryRouteDto>;

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
    : IRequestHandler<AssignRouteCommand, DeliveryRouteDto>
{
    public async Task<DeliveryRouteDto> Handle(AssignRouteCommand cmd, CancellationToken ct)
    {
        if (!DateOnly.TryParse(cmd.Date, out var date))
            throw new DomainException("Neispravan datum.", "invalid_date");

        var driver = await db.Drivers.FirstOrDefaultAsync(d => d.Id == cmd.DriverId, ct)
            ?? throw new NotFoundException("Vozač nije pronađen.");

        var validOrderIds = await db.Orders
            .Where(o => cmd.OrderIds.Contains(o.Id))
            .Select(o => o.Id)
            .ToListAsync(ct);

        // Replace any existing route for the same driver + date.
        var existing = await db.DeliveryRoutes
            .Include(r => r.Stops)
            .Where(r => r.DriverId == driver.Id && r.Date == date)
            .ToListAsync(ct);
        db.DeliveryRoutes.RemoveRange(existing);

        var route = new DeliveryRoute
        {
            Date = date,
            DriverId = driver.Id,
            Status = RouteStatus.Planned,
            // Distinct: dupli OrderId u zahtjevu bi inače stvorio dvije stanice sa istim
            // OrderId-jem, koje se PrimijeniAsync ne bi mogao pouzdano razlikovati.
            Stops = cmd.OrderIds
                .Distinct()
                .Where(validOrderIds.Contains)
                .Select((orderId, i) => new RouteStop { OrderId = orderId, Sequence = i + 1 })
                .ToList(),
        };

        db.DeliveryRoutes.Add(route);
        await db.SaveChangesAsync(ct);

        // Ponovo učitaj sa narudžbama, jer optimizacija treba koordinate i statuse.
        var saStavkama = await db.DeliveryRoutes
            .Include(r => r.Stops).ThenInclude(s => s.Order).ThenInclude(o => o!.DeliveryZone)
            .FirstAsync(r => r.Id == route.Id, ct);

        await Optimizacija.PrimijeniAsync(
            saStavkama, magacin.Value.Latitude, magacin.Value.Longitude, optimizer, ct);
        await db.SaveChangesAsync(ct);

        return await LoadRouteDtoAsync(db, route.Id, ct);
    }

    internal static async Task<DeliveryRouteDto> LoadRouteDtoAsync(IAppDbContext db, Guid routeId, CancellationToken ct)
    {
        var route = await db.DeliveryRoutes
            .Include(r => r.Driver)
            .Include(r => r.Stops).ThenInclude(s => s.Order).ThenInclude(o => o!.DeliveryZone)
            .FirstAsync(r => r.Id == routeId, ct);
        return route.ToDto();
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
