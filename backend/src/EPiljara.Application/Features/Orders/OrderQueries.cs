using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Mapping;
using EPiljara.Domain.Common;
using EPiljara.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Orders;

/* ---------- My orders ---------- */

public record GetMyOrdersQuery : IRequest<IReadOnlyList<OrderDto>>;

public class GetMyOrdersHandler(IAppDbContext db, ICurrentUser current)
    : IRequestHandler<GetMyOrdersQuery, IReadOnlyList<OrderDto>>
{
    public async Task<IReadOnlyList<OrderDto>> Handle(GetMyOrdersQuery q, CancellationToken ct)
    {
        var orders = await db.Orders
            .Include(o => o.Items)
            .Include(o => o.DeliverySlot)
            .Include(o => o.DeliveryZone)
            .Where(o => o.CustomerId == current.Id)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);

        return orders.Select(o => o.ToDto()).ToList();
    }
}

/* ---------- My saved delivery addresses ---------- */

public record GetMyAddressesQuery : IRequest<IReadOnlyList<string>>;

public class GetMyAddressesHandler(IAppDbContext db, ICurrentUser current)
    : IRequestHandler<GetMyAddressesQuery, IReadOnlyList<string>>
{
    public async Task<IReadOnlyList<string>> Handle(GetMyAddressesQuery q, CancellationToken ct)
    {
        // Addresses from the customer's delivered orders, most recent first, de-duplicated.
        var addresses = await db.Orders
            .Where(o => o.CustomerId == current.Id && o.Status == OrderStatus.Delivered)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => o.DeliveryAddress)
            .ToListAsync(ct);

        return addresses
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Distinct()
            .ToList();
    }
}

/* ---------- Single order ---------- */

public record GetOrderByIdQuery(Guid Id) : IRequest<OrderDto>;

public class GetOrderByIdHandler(IAppDbContext db, ICurrentUser current)
    : IRequestHandler<GetOrderByIdQuery, OrderDto>
{
    public async Task<OrderDto> Handle(GetOrderByIdQuery q, CancellationToken ct)
    {
        var order = await db.Orders
            .Include(o => o.Items)
            .Include(o => o.DeliverySlot)
            .Include(o => o.DeliveryZone)
            .FirstOrDefaultAsync(o => o.Id == q.Id, ct)
            ?? throw new NotFoundException("Narudžba nije pronađena.");

        if (order.CustomerId != current.Id && current.Role != UserRole.Admin)
            throw new ForbiddenException("Nemate pristup ovoj narudžbi.");

        return order.ToDto();
    }
}
