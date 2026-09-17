using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Entities;
using Microsoft.EntityFrameworkCore;

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
            // Soft delete: removed products are hidden from every query.
            e.HasQueryFilter(x => !x.IsDeleted);
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
            e.HasIndex(x => x.Code).IsUnique();
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
        });

        // One convention for all money/quantity columns.
        foreach (var property in b.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            property.SetPrecision(12);
            property.SetScale(2);
        }
    }
}
