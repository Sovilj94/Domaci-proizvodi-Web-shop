using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Mapping;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

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

public class AssignRouteHandler(IAppDbContext db) : IRequestHandler<AssignRouteCommand, DeliveryRouteDto>
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
            Stops = cmd.OrderIds
                .Where(validOrderIds.Contains)
                .Select((orderId, i) => new RouteStop { OrderId = orderId, Sequence = i + 1 })
                .ToList(),
        };

        db.DeliveryRoutes.Add(route);
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

public class OptimizeRouteHandler(IAppDbContext db, IRouteOptimizer optimizer)
    : IRequestHandler<OptimizeRouteCommand, DeliveryRouteDto>
{
    public async Task<DeliveryRouteDto> Handle(OptimizeRouteCommand cmd, CancellationToken ct)
    {
        var route = await db.DeliveryRoutes
            .Include(r => r.Stops).ThenInclude(s => s.Order).ThenInclude(o => o!.DeliveryZone)
            .FirstOrDefaultAsync(r => r.Id == cmd.RouteId, ct)
            ?? throw new NotFoundException("Ruta nije pronađena.");

        var inputs = route.Stops.Select(s => new RouteStopInput(
            s.OrderId,
            s.Order?.DeliveryAddress ?? string.Empty,
            s.Order?.DeliveryZone?.Name ?? string.Empty,
            s.Order?.Latitude,
            s.Order?.Longitude)).ToList();

        var orderedIds = await optimizer.OptimizeAsync(inputs, ct);

        var sequence = 1;
        foreach (var orderId in orderedIds)
        {
            var stop = route.Stops.FirstOrDefault(s => s.OrderId == orderId);
            if (stop is not null) stop.Sequence = sequence++;
        }

        route.Status = RouteStatus.Optimized;
        await db.SaveChangesAsync(ct);

        return await AssignRouteHandler.LoadRouteDtoAsync(db, route.Id, ct);
    }
}
