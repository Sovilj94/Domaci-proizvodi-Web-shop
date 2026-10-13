using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Features.Pretplate;
using EPiljara.Application.Mapping;
using EPiljara.Domain.Common;
using EPiljara.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Driver;

/* ---------- The driver's route for a day (mobile app) ---------- */

/// <summary>
/// Vraća PRVU rutu vozača za dati dan (poređano po nazivu) — koristi je popup na dolasku i
/// detekcija dolaska preko svih stanica. Otkad jedan dan može imati više ruta (po zoni),
/// "prva" mora biti determinisana, zato je poredak isti kao GET /driver/routes.
/// Endpoint namjerno ostaje — uklanjanje bi bilo šire od posla oko više ruta po danu.
/// </summary>
public record GetDriverRouteQuery(string? Date) : IRequest<DeliveryRouteDto?>;

public class GetDriverRouteHandler(IAppDbContext db, ICurrentUser current)
    : IRequestHandler<GetDriverRouteQuery, DeliveryRouteDto?>
{
    public async Task<DeliveryRouteDto?> Handle(GetDriverRouteQuery q, CancellationToken ct)
    {
        var driver = await db.Drivers.FirstOrDefaultAsync(d => d.UserId == current.Id, ct)
            ?? throw new ForbiddenException("Niste registrovani kao vozač.");

        var date = DateOnly.TryParse(q.Date, out var parsed)
            ? parsed
            : DateOnly.FromDateTime(DateTime.UtcNow);

        var route = await db.DeliveryRoutes
            .Include(r => r.Driver)
            .Include(r => r.DeliveryZone)
            .Include(r => r.Stops).ThenInclude(s => s.Order).ThenInclude(o => o!.DeliveryZone)
            .Where(r => r.DriverId == driver.Id && r.Date == date)
            .OrderBy(r => r.Naziv)
            .FirstOrDefaultAsync(ct);

        return route?.ToDto();
    }
}

/* ---------- Sve rute vozača za dati dan (mobile app) ---------- */

/// <summary>
/// Vraća sve rute vozača za dati dan, poređane po nazivu. Prazna lista je uredan
/// odgovor (vozač nema dodijeljenih ruta tog dana), ne greška.
/// </summary>
public record GetDriverRoutesQuery(string? Date) : IRequest<IReadOnlyList<DeliveryRouteDto>>;

public class GetDriverRoutesHandler(IAppDbContext db, ICurrentUser current)
    : IRequestHandler<GetDriverRoutesQuery, IReadOnlyList<DeliveryRouteDto>>
{
    public async Task<IReadOnlyList<DeliveryRouteDto>> Handle(GetDriverRoutesQuery q, CancellationToken ct)
    {
        var driver = await db.Drivers.FirstOrDefaultAsync(d => d.UserId == current.Id, ct)
            ?? throw new ForbiddenException("Niste registrovani kao vozač.");

        var date = DateOnly.TryParse(q.Date, out var parsed)
            ? parsed
            : DateOnly.FromDateTime(DateTime.UtcNow);

        var rute = await db.DeliveryRoutes
            .Include(r => r.Driver)
            .Include(r => r.DeliveryZone)
            .Include(r => r.Stops).ThenInclude(s => s.Order).ThenInclude(o => o!.DeliveryZone)
            .Where(r => r.DriverId == driver.Id && r.Date == date)
            .OrderBy(r => r.Naziv)
            .ToListAsync(ct);

        return rute.Select(r => r.ToDto()).ToList();
    }
}

/* ---------- Driver marks a stop Delivered / FailedDelivery ---------- */

public record UpdateStopStatusCommand(Guid OrderId, string Status) : IRequest<OrderDto>;

public class UpdateStopStatusHandler(IAppDbContext db, ICurrentUser current)
    : IRequestHandler<UpdateStopStatusCommand, OrderDto>
{
    private static readonly HashSet<OrderStatus> Allowed = [OrderStatus.Delivered, OrderStatus.FailedDelivery];

    public async Task<OrderDto> Handle(UpdateStopStatusCommand cmd, CancellationToken ct)
    {
        if (!Enum.TryParse<OrderStatus>(cmd.Status, ignoreCase: true, out var status) || !Allowed.Contains(status))
            throw new DomainException("Vozač može označiti samo Delivered ili FailedDelivery.", "invalid_status");

        var driver = await db.Drivers.FirstOrDefaultAsync(d => d.UserId == current.Id, ct)
            ?? throw new ForbiddenException("Niste registrovani kao vozač.");

        var assigned = await db.RouteStops
            .AnyAsync(s => s.OrderId == cmd.OrderId && s.Route!.DriverId == driver.Id, ct);
        if (!assigned)
            throw new ForbiddenException("Ova narudžba nije na vašoj ruti.");

        var order = await db.Orders
            .Include(o => o.Items)
            .Include(o => o.DeliverySlot)
            .Include(o => o.DeliveryZone)
            .FirstOrDefaultAsync(o => o.Id == cmd.OrderId, ct)
            ?? throw new NotFoundException("Narudžba nije pronađena.");

        // Prije prelaza, jer pomoćnik gleda i STARI status da isporuku ne bi brojao dvaput —
        // a ispod se status mijenja i po tri puta. Drugo mjesto koje postavlja Delivered je
        // admin panel (AdminOrders); otuda pomoćnik, a ne dvije kopije istog uslova.
        await BrojacIsporuka.ZabiljeziAsync(db, order, status, ct);

        // Ruta se može dodijeliti narudžbi koja je još Confirmed/Packing (vidi ASSIGNABLE
        // u admin panelu). Mašina stanja dozvoljava Delivered samo iz OutForDelivery, pa je
        // ovdje prevodimo kroz međukorake — svaki pojedinačni prelaz je dozvoljen.
        if (order.Status is OrderStatus.Confirmed) order.TransitionTo(OrderStatus.Packing);
        if (order.Status is OrderStatus.Packing) order.TransitionTo(OrderStatus.OutForDelivery);
        order.TransitionTo(status);
        order.ZatvorenaU = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return order.ToDto();
    }
}

/* ---------- Istorija prošlih ruta vozača (mobile app) ---------- */

public record GetDriverRouteHistoryQuery : IRequest<DriverRouteHistoryDto>;

public class GetDriverRouteHistoryHandler(IAppDbContext db, ICurrentUser current)
    : IRequestHandler<GetDriverRouteHistoryQuery, DriverRouteHistoryDto>
{
    public async Task<DriverRouteHistoryDto> Handle(GetDriverRouteHistoryQuery q, CancellationToken ct)
    {
        var driver = await db.Drivers.FirstOrDefaultAsync(d => d.UserId == current.Id, ct)
            ?? throw new ForbiddenException("Niste registrovani kao vozač.");

        var danas = DateOnly.FromDateTime(DateTime.UtcNow);

        // "Istorija" su isključivo ZAVRŠENE rute — današnja (aktivna) ruta se prikazuje
        // na ekranu rute, ne ovdje.
        var prosleRute = await db.DeliveryRoutes
            .Where(r => r.DriverId == driver.Id && r.Date < danas)
            .Include(r => r.Stops).ThenInclude(s => s.Order)
            .OrderByDescending(r => r.Date)
            .ToListAsync(ct);

        var ruteOvogMjeseca = prosleRute
            .Where(r => r.Date.Year == danas.Year && r.Date.Month == danas.Month)
            .ToList();

        var isporuceno = ruteOvogMjeseca.Sum(r => r.Stops.Count(s => s.Order!.Status == OrderStatus.Delivered));
        var neuspjelo = ruteOvogMjeseca.Sum(r => r.Stops.Count(s => s.Order!.Status == OrderStatus.FailedDelivery));
        var ukupnoZaProcenat = isporuceno + neuspjelo;
        var uspjesnost = ukupnoZaProcenat == 0 ? 0 : (int)Math.Round(isporuceno * 100.0 / ukupnoZaProcenat);

        // DuzinaMetara je nullable jer null znači "optimizacija nije uspjela", a ne
        // "nula metara" — preskačemo null umjesto da ga računamo kao 0. Ako nijedna ruta
        // ovog mjeseca nema poznatu dužinu, rezultat je null (nema podatka), ne 0.
        var poznateDuzine = ruteOvogMjeseca
            .Where(r => r.DuzinaMetara.HasValue)
            .Select(r => r.DuzinaMetara!.Value)
            .ToList();
        double? predjenoMetara = poznateDuzine.Count == 0 ? null : poznateDuzine.Sum();

        return new DriverRouteHistoryDto
        {
            UkupnoIsporuka = isporuceno,
            Uspjesnost = uspjesnost,
            PredjenoMetara = predjenoMetara,
            Rute = prosleRute.Select(r => r.ToHistoryDto()).ToList(),
        };
    }
}

/* ---------- Profil vozača (mobile app) ---------- */

public record GetDriverProfileQuery : IRequest<DriverProfileDto>;

public class GetDriverProfileHandler(IAppDbContext db, ICurrentUser current)
    : IRequestHandler<GetDriverProfileQuery, DriverProfileDto>
{
    public async Task<DriverProfileDto> Handle(GetDriverProfileQuery q, CancellationToken ct)
    {
        var driver = await db.Drivers.Include(d => d.User)
            .FirstOrDefaultAsync(d => d.UserId == current.Id, ct)
            ?? throw new ForbiddenException("Niste registrovani kao vozač.");

        return new DriverProfileDto
        {
            Name = driver.Name,
            Phone = driver.Phone,
            Email = driver.User?.Email ?? string.Empty,
            Vozilo = driver.Vozilo,
            Registracija = driver.Registracija,
        };
    }
}
