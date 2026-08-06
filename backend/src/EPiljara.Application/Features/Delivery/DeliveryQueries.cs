using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Mapping;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Delivery;

public record GetDeliveryZonesQuery : IRequest<IReadOnlyList<DeliveryZoneDto>>;

public class GetDeliveryZonesHandler(IAppDbContext db)
    : IRequestHandler<GetDeliveryZonesQuery, IReadOnlyList<DeliveryZoneDto>>
{
    public async Task<IReadOnlyList<DeliveryZoneDto>> Handle(GetDeliveryZonesQuery q, CancellationToken ct)
    {
        var zones = await db.DeliveryZones.Where(z => z.IsActive).OrderBy(z => z.Name).ToListAsync(ct);
        return zones.Select(z => z.ToDto()).ToList();
    }
}

public record GetDeliverySlotsQuery : IRequest<IReadOnlyList<DeliverySlotDto>>;

public class GetDeliverySlotsHandler(IAppDbContext db)
    : IRequestHandler<GetDeliverySlotsQuery, IReadOnlyList<DeliverySlotDto>>
{
    public async Task<IReadOnlyList<DeliverySlotDto>> Handle(GetDeliverySlotsQuery q, CancellationToken ct)
    {
        var slots = await db.DeliverySlots.OrderBy(s => s.DayOfWeek).ToListAsync(ct);
        return slots.Select(s => s.ToDto()).ToList();
    }
}
