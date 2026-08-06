using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Admin;

public record GetDashboardQuery(DateTime? Date) : IRequest<DashboardDto>;

public class GetDashboardHandler(IAppDbContext db) : IRequestHandler<GetDashboardQuery, DashboardDto>
{
    public async Task<DashboardDto> Handle(GetDashboardQuery q, CancellationToken ct)
    {
        var basis = q.Date ?? DateTime.UtcNow;
        var day = DateTime.SpecifyKind(basis.Date, DateTimeKind.Utc);
        var next = day.AddDays(1);

        var todays = await db.Orders
            .Where(o => o.CreatedAt >= day && o.CreatedAt < next)
            .Select(o => new { o.Status, o.TotalAmount })
            .ToListAsync(ct);

        var revenueToday = Math.Round(
            todays.Where(o => o.Status != OrderStatus.Cancelled).Sum(o => o.TotalAmount), 2);

        var breakdown = await db.Orders
            .GroupBy(o => o.Status)
            .Select(g => new StatusCountDto { Status = g.Key.ToString(), Count = g.Count() })
            .ToListAsync(ct);

        return new DashboardDto
        {
            Date = day,
            OrdersToday = todays.Count,
            RevenueToday = revenueToday,
            PendingOrders = await db.Orders.CountAsync(o => o.Status == OrderStatus.Pending, ct),
            OutForDelivery = await db.Orders.CountAsync(o => o.Status == OrderStatus.OutForDelivery, ct),
            StatusBreakdown = breakdown,
        };
    }
}
