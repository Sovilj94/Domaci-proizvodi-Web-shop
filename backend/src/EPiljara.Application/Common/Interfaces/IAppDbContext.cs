using EPiljara.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

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
    DbSet<GuestOrderRequest> GuestOrderRequests { get; }
    DbSet<GuestOrderRequestItem> GuestOrderRequestItems { get; }
    DbSet<KorpicaKategorija> KorpicaKategorije { get; }
    DbSet<SedmicniSadrzaj> SedmicniSadrzaji { get; }
    DbSet<StavkaSadrzaja> StavkeSadrzaja { get; }
    DbSet<StavkaKorpiceNarudzbe> StavkeKorpiceNarudzbe { get; }

    /// <summary>Nalozi za ponavljanje korpice — vidi Pretplata entitet.</summary>
    DbSet<Pretplata> Pretplate { get; }

    /// <summary>Tabela sa jednim redom — polazište tura. Vidi Magacin entitet.</summary>
    DbSet<Magacin> Magacin { get; }

    /// <summary>Za transakcije kad jedna izmjena mora u više SaveChanges koraka (DbContext ga već ima).</summary>
    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
