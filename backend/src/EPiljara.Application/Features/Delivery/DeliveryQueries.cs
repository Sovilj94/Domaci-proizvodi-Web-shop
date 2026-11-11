using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Features.Geocoding;
using EPiljara.Application.Mapping;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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

/// <summary>Javna pravila dostave (checkout i korpa prikazuju isto što server naplati).</summary>
public record DostavaPravilaDto(decimal DostavaSaKorpicom, decimal DostavaBezKorpice, decimal MinimalnaNarudzba);

public record GetDostavaPravilaQuery : IRequest<DostavaPravilaDto>;

public class GetDostavaPravilaHandler(IOptions<DostavaOpcije> dostava)
    : IRequestHandler<GetDostavaPravilaQuery, DostavaPravilaDto>
{
    public Task<DostavaPravilaDto> Handle(GetDostavaPravilaQuery q, CancellationToken ct) =>
        Task.FromResult(new DostavaPravilaDto(
            dostava.Value.DostavaSaKorpicom, dostava.Value.DostavaBezKorpice, dostava.Value.MinimalnaNarudzba));
}
