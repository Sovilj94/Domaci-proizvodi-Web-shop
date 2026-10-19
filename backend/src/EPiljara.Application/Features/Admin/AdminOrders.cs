using EPiljara.Application.Common;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Features.Poeni;
using EPiljara.Application.Features.Pretplate;
using EPiljara.Application.Mapping;
using EPiljara.Domain.Common;
using EPiljara.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Features.Admin;

/* ---------- List ---------- */

/// <param name="Unassigned">
/// Kad je true, vraća samo narudžbe koje čekaju vozača za <paramref name="Date"/>: one bez
/// ijedne stanice na ruti tog dana i u statusu koji se još može voziti. Dispečerski ekran
/// puni tab „Nedodijeljeno" iz ovoga. Mijenja i značenje <paramref name="Date"/> — vidi dolje.
/// </param>
public record GetAdminOrdersQuery(
    string? Status, DateTime? Date, bool Unassigned = false,
    int? Godina = null, int? Sedmica = null, string? Pretraga = null)
    : IRequest<IReadOnlyList<OrderDto>>;

public class GetAdminOrdersHandler(IAppDbContext db)
    : IRequestHandler<GetAdminOrdersQuery, IReadOnlyList<OrderDto>>
{
    /// <summary>
    /// Statusi u kojima narudžba još čeka vožnju. <see cref="OrderStatus.Pending"/> je
    /// NAMJERNO unutra: web shop pravi svaku narudžbu kao Pending, pa ju je izostavljanje
    /// činilo nevidljivom dispečeru dok je neko ručno ne potvrdi — to je bio prijavljen bug.
    /// </summary>
    private static readonly OrderStatus[] ZaDostavu =
    [
        OrderStatus.Pending,
        OrderStatus.Confirmed,
        OrderStatus.Packing,
        OrderStatus.OutForDelivery,
    ];

    public async Task<IReadOnlyList<OrderDto>> Handle(GetAdminOrdersQuery q, CancellationToken ct)
    {
        var query = db.Orders
            .Include(o => o.Items)
            .Include(o => o.DeliverySlot)
            .Include(o => o.DeliveryZone)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q.Status) &&
            Enum.TryParse<OrderStatus>(q.Status, ignoreCase: true, out var status))
        {
            query = query.Where(o => o.Status == status);
        }

        // Sedmica isporuke i pretraga (šifra, kupac, telefon, adresa) — za praćenje u admin panelu.
        if (q.Godina is { } g && q.Sedmica is { } sed)
            query = query.Where(o => o.Godina == g && o.Sedmica == sed);
        var t = q.Pretraga?.Trim().ToLower();
        if (!string.IsNullOrEmpty(t))
            query = query.Where(o => o.Code.ToLower().Contains(t) || o.CustomerName.ToLower().Contains(t)
                || (o.CustomerPhone != null && o.CustomerPhone.Contains(t)) || o.DeliveryAddress.ToLower().Contains(t));

        if (q.Unassigned)
        {
            // Datum je ovdje dan DOSTAVE (dan rute), ne dan kreiranja — narudžba napravljena
            // juče se danas i dalje vozi, pa filtriranje po CreatedAt bi je sakrilo.
            var datum = DateOnly.FromDateTime(q.Date?.Date ?? DateTime.UtcNow.Date);

            query = query
                .Where(o => ZaDostavu.Contains(o.Status))
                .Where(o => !db.RouteStops.Any(s => s.OrderId == o.Id && s.Route!.Date == datum));

            // Najstarije prvo: dispečer raspoređuje redom kojim su narudžbe stigle.
            var cekaju = await query.OrderBy(o => o.CreatedAt).ThenBy(o => o.Id).ToListAsync(ct);
            return cekaju.Select(o => o.ToDto()).ToList();
        }

        if (q.Date is { } date)
        {
            var day = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
            var next = day.AddDays(1);
            query = query.Where(o => o.CreatedAt >= day && o.CreatedAt < next);
        }

        var orders = await query.OrderByDescending(o => o.CreatedAt).ToListAsync(ct);
        return orders.Select(o => o.ToDto()).ToList();
    }
}

/* ---------- Update status ---------- */

public record UpdateOrderStatusCommand(Guid Id, string Status) : IRequest<OrderDto>;

public class UpdateOrderStatusHandler(IAppDbContext db, IOptions<PoeniOpcije> poeni)
    : IRequestHandler<UpdateOrderStatusCommand, OrderDto>
{
    public async Task<OrderDto> Handle(UpdateOrderStatusCommand cmd, CancellationToken ct)
    {
        if (!Enum.TryParse<OrderStatus>(cmd.Status, ignoreCase: true, out var status))
            throw new DomainException("Nepoznat status narudžbe.", "invalid_status");

        var order = await db.Orders
            .Include(o => o.Items)
            .Include(o => o.DeliverySlot)
            .Include(o => o.DeliveryZone)
            .FirstOrDefaultAsync(o => o.Id == cmd.Id, ct)
            ?? throw new NotFoundException("Narudžba nije pronađena.");

        // Prije prelaza, jer pomoćnik gleda i STARI status da isporuku ne bi brojao dvaput.
        // Drugo mjesto koje postavlja Delivered je vozačev DriverFeature — otuda pomoćnik,
        // a ne dvije kopije istog uslova.
        await BrojacIsporuka.ZabiljeziAsync(db, order, status, ct);
        // Poeni se upisuju uz stari status, u isti SaveChanges kao i prelaz (kao i brojač).
        await PoeniPravila.ZabiljeziPromjenuStatusaAsync(db, order, status, poeni.Value, ct);

        order.TransitionTo(status);
        await IstovremenaPromjena.SacuvajAsync(db, ct);
        return order.ToDto();
    }
}
