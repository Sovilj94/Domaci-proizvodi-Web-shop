using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace EPiljara.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IAppDbContext
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Producer> Producers => Set<Producer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<DeliveryZone> DeliveryZones => Set<DeliveryZone>();
    public DbSet<DeliverySlot> DeliverySlots => Set<DeliverySlot>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<DeliveryRoute> DeliveryRoutes => Set<DeliveryRoute>();
    public DbSet<RouteStop> RouteStops => Set<RouteStop>();
    public DbSet<GuestOrderRequest> GuestOrderRequests => Set<GuestOrderRequest>();
    public DbSet<GuestOrderRequestItem> GuestOrderRequestItems => Set<GuestOrderRequestItem>();
    public DbSet<KorpicaKategorija> KorpicaKategorije => Set<KorpicaKategorija>();
    public DbSet<SedmicniSadrzaj> SedmicniSadrzaji => Set<SedmicniSadrzaj>();
    public DbSet<StavkaSadrzaja> StavkeSadrzaja => Set<StavkaSadrzaja>();
    public DbSet<Magacin> Magacin => Set<Magacin>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Category>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(140).IsRequired();
            e.HasIndex(x => x.Slug).IsUnique();
        });

        b.Entity<Producer>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(160).IsRequired();
            e.Property(x => x.Village).HasMaxLength(120);
            e.Property(x => x.Description).HasMaxLength(2000);
        });

        b.Entity<Product>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(220).IsRequired();
            e.Property(x => x.Description).HasMaxLength(4000);
            e.Property(x => x.Unit).HasConversion<string>().HasMaxLength(8);
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => x.CategoryId);
            e.HasOne(x => x.Category).WithMany(c => c.Products)
                .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Producer).WithMany(p => p.Products)
                .HasForeignKey(x => x.ProducerId).OnDelete(DeleteBehavior.Restrict);

            // Bez eksplicitnog HasDefaultValue, EF za NOT NULL string kolonu bez
            // konfigurisanog defaulta generiše migraciju sa DEFAULT '' — a '' nije
            // ispravan naziv enum vrijednosti, pa bi EnumToStringConverter pucao pri
            // čitanju postojećih redova čim migracija dodijeli tu vrijednost. Obican je
            // ispravno stanje za sve postojeće redove (nijedan još nije korpica).
            e.Property(x => x.Vrsta).HasConversion<string>().HasMaxLength(16)
                .HasDefaultValue(VrstaProizvoda.Obican);
            e.Property(x => x.TipKorpice).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Velicina).HasConversion<string>().HasMaxLength(16);

            // Mjeseci kao CSV: Postgres integer[] bi bio prirodniji, ali testovi rade na
            // SQLite koji nema tip niza. Jedan oblik koji radi na oba providera.
            e.Property(x => x.Mjeseci)
                .HasConversion(
                    v => string.Join(',', v),
                    v => string.IsNullOrWhiteSpace(v)
                        ? Array.Empty<int>()
                        : v.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray(),
                    new ValueComparer<int[]>(
                        (a, c) => a!.SequenceEqual(c!),
                        v => v.Aggregate(0, (h, x) => HashCode.Combine(h, x)),
                        v => v.ToArray()))
                .HasMaxLength(60)
                .HasDefaultValue(Array.Empty<int>());

            e.HasIndex(x => x.Vrsta);

            // Soft delete: removed products are hidden from every query.
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        b.Entity<KorpicaKategorija>(e =>
        {
            e.HasKey(x => new { x.TipKorpice, x.CategoryId });
            e.Property(x => x.TipKorpice).HasConversion<string>().HasMaxLength(16);
            e.HasOne(x => x.Category).WithMany()
                .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<SedmicniSadrzaj>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.CiljnaVrijednost).HasPrecision(12, 2);
            e.HasIndex(x => new { x.KorpicaId, x.Godina, x.Sedmica }).IsUnique();
            e.HasIndex(x => new { x.Godina, x.Sedmica });
            e.HasOne(x => x.Korpica).WithMany()
                .HasForeignKey(x => x.KorpicaId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<StavkaSadrzaja>(e =>
        {
            e.Property(x => x.Kolicina).HasPrecision(10, 3);
            e.Property(x => x.JedinicnaCijena).HasPrecision(12, 2);
            e.HasOne(x => x.SedmicniSadrzaj).WithMany(s => s.Stavke)
                .HasForeignKey(x => x.SedmicniSadrzajId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Product).WithMany()
                .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<DeliveryZone>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
        });

        b.Entity<DeliverySlot>(e =>
        {
            e.Property(x => x.TimeFrom).HasMaxLength(5);
            e.Property(x => x.TimeTo).HasMaxLength(5);
            e.Property(x => x.Label).HasMaxLength(60);
        });

        b.Entity<User>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(160).IsRequired();
            e.Property(x => x.Email).HasMaxLength(256).IsRequired();
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(x => x.Email).IsUnique();
        });

        b.Entity<Address>(e =>
        {
            e.Property(x => x.Label).HasMaxLength(60);
            // 400 je isto kao Order.DeliveryAddress — adresa putuje odavde u narudžbu,
            // pa kraće polje ovdje bi značilo da se nešto što je stalo u narudžbu
            // ne može sačuvati u adresar.
            e.Property(x => x.FullAddress).HasMaxLength(400).IsRequired();
            e.HasIndex(x => x.UserId, "IX_Addresses_UserId");
            // Najviše jedna glavna adresa po korisniku — brana na nivou baze protiv
            // trke (dupli POST/SetDefault klik) koju handler sam ne može spriječiti.
            // Filter je obavezan: bez njega bi indeks tražio jedinstven UserId preko
            // SVIH redova, što bi zabranilo korisniku više od jedne adrese uopšte.
            e.HasIndex(x => x.UserId, "IX_Addresses_UserId_IsDefault")
                .IsUnique()
                .HasFilter("\"IsDefault\"");
            e.HasOne(x => x.User).WithMany()
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.DeliveryZone).WithMany()
                .HasForeignKey(x => x.DeliveryZoneId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Driver>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(160).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(40);
            e.Property(x => x.Vozilo).HasMaxLength(80);
            e.Property(x => x.Registracija).HasMaxLength(20);
            // Podrazumijevano true, pa postojeći redovi migracijom postaju aktivni vozači —
            // bez toga bi svi nestali sa dispečerskog ekrana čim migracija prođe.
            e.Property(x => x.Aktivan).HasDefaultValue(true);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Order>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(20).IsRequired();
            e.Property(x => x.CustomerName).HasMaxLength(160).IsRequired();
            e.Property(x => x.CustomerPhone).HasMaxLength(40);
            e.Property(x => x.CustomerEmail).HasMaxLength(256);
            e.Property(x => x.DeliveryAddress).HasMaxLength(400).IsRequired();
            e.Property(x => x.Note).HasMaxLength(1000);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.PublicToken).HasMaxLength(32).IsRequired();
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.PublicToken).IsUnique();
            e.HasIndex(x => x.CustomerId);
            e.HasMany(x => x.Items).WithOne(i => i.Order!)
                .HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.DeliverySlot).WithMany()
                .HasForeignKey(x => x.DeliverySlotId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.DeliveryZone).WithMany()
                .HasForeignKey(x => x.DeliveryZoneId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<OrderItem>(e =>
        {
            e.Property(x => x.ProductName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Unit).HasConversion<string>().HasMaxLength(8);
            e.Ignore(x => x.LineTotal);
        });

        b.Entity<DeliveryRoute>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Naziv).HasMaxLength(100).IsRequired();
            // Namjerno BEZ indeksa na (DriverId, Date) — više ruta po danu je sad
            // očekivano stanje otkad se dodjela dijeli po zonama; stari indeks je
            // implicirao "jedan red po vozaču i danu" i zato je uklonjen.
            e.HasOne(x => x.Driver).WithMany(d => d.Routes)
                .HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.DeliveryZone).WithMany()
                .HasForeignKey(x => x.DeliveryZoneId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Stops).WithOne(s => s.Route!)
                .HasForeignKey(s => s.RouteId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<RouteStop>(e =>
        {
            e.HasOne(x => x.Order).WithMany()
                .HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);

            // (RouteId, Sequence) is unique, but deliberately not configured here: it has to
            // be DEFERRABLE INITIALLY DEFERRED, which the Npgsql fluent API cannot express, so
            // it lives as raw SQL in the RouteStopSequenceUnique migration. Do not "fix" that
            // by adding HasIndex(...).IsUnique() — renumbering open stops permutes one range
            // of numbers, and a per-statement check would reject every re-optimization.
        });

        b.Entity<GuestOrderRequest>(b =>
        {
            b.Property(r => r.Token).HasMaxLength(32).IsRequired();
            b.HasIndex(r => r.Token).IsUnique();
            b.HasIndex(r => r.ExpiresAt);
            b.Property(r => r.CustomerEmail).IsRequired();
            b.Property(r => r.Subtotal).HasPrecision(10, 2);
            b.Property(r => r.DeliveryFee).HasPrecision(10, 2);
            b.Property(r => r.TotalAmount).HasPrecision(10, 2);
            b.HasOne(r => r.DeliveryZone).WithMany()
                .HasForeignKey(r => r.DeliveryZoneId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(r => r.DeliverySlot).WithMany()
                .HasForeignKey(r => r.DeliverySlotId).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(r => r.Items)
                .WithOne(i => i.Request!)
                .HasForeignKey(i => i.RequestId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<GuestOrderRequestItem>(b =>
        {
            b.Property(i => i.Unit).HasConversion<string>().HasMaxLength(8);
            b.Property(i => i.UnitPrice).HasPrecision(10, 2);
            b.Property(i => i.Quantity).HasPrecision(10, 3);
        });

        b.Entity<Magacin>(e =>
        {
            // Ime u jednini, jer ovo nije zbirka nego jedna vrijednost — i CHECK koji to
            // čini pravilom baze, a ne dogovorom: samo Magacin.JediniId smije postojati.
            e.ToTable("Magacin", t => t.HasCheckConstraint(
                "CK_Magacin_JedanRed", $"\"Id\" = '{Domain.Entities.Magacin.JediniId}'"));
            e.Property(x => x.Naziv).HasMaxLength(160).IsRequired();
        });

        // One convention for all money/quantity columns.
        foreach (var property in b.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            if (property.GetPrecision() == null)
            {
                property.SetPrecision(12);
                property.SetScale(2);
            }
        }
    }
}
