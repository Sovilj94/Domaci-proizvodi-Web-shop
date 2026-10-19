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

    /// <summary>Knjiga poena — vidi PoeniStavka entitet.</summary>
    DbSet<PoeniStavka> PoeniStavke { get; }

    /// <summary>Utisci kupaca za početnu — vidi Utisak entitet.</summary>
    DbSet<Utisak> Utisci { get; }
    DbSet<PrijavaNovosti> PrijaveNovosti { get; }
    DbSet<SadrzajSajta> SadrzajSajta { get; }
    DbSet<OcjenaIsporuke> OcjeneIsporuke { get; }

    /// <summary>Recepti i njihovi sastojci — vidi Recept entitet.</summary>
    DbSet<Recept> Recepti { get; }
    DbSet<ReceptSastojak> ReceptSastojci { get; }

    /// <summary>Objavljene laboratorijske analize — vidi Analiza entitet.</summary>
    DbSet<Analiza> Analize { get; }

    /// <summary>Prijave partnera — vidi PartnerPrijava entitet.</summary>
    DbSet<PartnerPrijava> PartnerPrijave { get; }

    /// <summary>Tabela sa jednim redom — polazište tura. Vidi Magacin entitet.</summary>
    DbSet<Magacin> Magacin { get; }

    /// <summary>Promo kodovi za popust na robu — vidi PromoKod entitet.</summary>
    DbSet<PromoKod> PromoKodovi { get; }

    /// <summary>Za transakcije kad jedna izmjena mora u više SaveChanges koraka (DbContext ga već ima).</summary>
    DatabaseFacade Database { get; }

    /// <summary>B1/B2: da li baza podržava zaključavanje redova (`SELECT … FOR UPDATE`).</summary>
    bool PodrzavaZakljucavanje { get; }

    /// <summary>B1/B2: zaključava red kupca i red promo koda do kraja tekuće transakcije.</summary>
    Task ZakljucajZaNarudzbuAsync(Guid? kupacId, string? promoKod, CancellationToken ct);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
