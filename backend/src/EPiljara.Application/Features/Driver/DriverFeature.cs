using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Mapping;
using EPiljara.Domain.Common;
using EPiljara.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Driver;

/* ---------- The driver's route for a day (mobile app) ---------- */

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
            .Include(r => r.Stops).ThenInclude(s => s.Order).ThenInclude(o => o!.DeliveryZone)
            .FirstOrDefaultAsync(r => r.DriverId == driver.Id && r.Date == date, ct);

        return route?.ToDto();
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

        // Ruta se može dodijeliti narudžbi koja je još Confirmed/Packing (vidi ASSIGNABLE
        // u admin panelu). Mašina stanja dozvoljava Delivered samo iz OutForDelivery, pa je
        // ovdje prevodimo kroz međukorake — svaki pojedinačni prelaz je dozvoljen.
        if (order.Status is OrderStatus.Confirmed) order.TransitionTo(OrderStatus.Packing);
        if (order.Status is OrderStatus.Packing) order.TransitionTo(OrderStatus.OutForDelivery);
        order.TransitionTo(status);
        await db.SaveChangesAsync(ct);
        return order.ToDto();
    }
}
