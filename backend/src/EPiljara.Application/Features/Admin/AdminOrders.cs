using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Mapping;
using EPiljara.Domain.Common;
using EPiljara.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Admin;

/* ---------- List ---------- */

public record GetAdminOrdersQuery(string? Status, DateTime? Date) : IRequest<IReadOnlyList<OrderDto>>;

public class GetAdminOrdersHandler(IAppDbContext db)
    : IRequestHandler<GetAdminOrdersQuery, IReadOnlyList<OrderDto>>
{
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

public class UpdateOrderStatusHandler(IAppDbContext db)
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

        order.TransitionTo(status);
        await db.SaveChangesAsync(ct);
        return order.ToDto();
    }
}
