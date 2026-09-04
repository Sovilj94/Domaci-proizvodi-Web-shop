using EPiljara.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Common.Interfaces;

/// <summary>Persistence abstraction the Application layer depends on (implemented in Infrastructure).</summary>
public interface IAppDbContext
{
    DbSet<Category> Categories { get; }
    DbSet<Producer> Producers { get; }
    DbSet<Product> Products { get; }
    DbSet<DeliveryZone> DeliveryZones { get; }
    DbSet<DeliverySlot> DeliverySlots { get; }
    DbSet<User> Users { get; }
    DbSet<Address> Addresses { get; }
    DbSet<Driver> Drivers { get; }
    DbSet<Order> Orders { get; }
    DbSet<OrderItem> OrderItems { get; }
    DbSet<DeliveryRoute> DeliveryRoutes { get; }
    DbSet<RouteStop> RouteStops { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
