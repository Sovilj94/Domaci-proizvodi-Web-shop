using EPiljara.Application.Common;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Infrastructure.Persistence;

/// <summary>Idempotent demo seed: Manjača producers, catalog, zones, users, sample orders.</summary>
public static class DbSeeder
{
    private static string Img(string seed) => $"https://picsum.photos/seed/{seed}/700/700";

    public static async Task SeedAsync(AppDbContext db, IPasswordHasher hasher, CancellationToken ct = default)
    {
        if (await db.Categories.AnyAsync(ct))
            return;

        // ----- Categories -----
        var povrce = Cat("Povrće", 1, "povrce");
        var voce = Cat("Voće", 2, "voce");
        var mlijecni = Cat("Mliječni proizvodi", 3, "mlijecni-proizvodi");
        var meso = Cat("Meso i suhomesnato", 4, "meso");
        var jaja = Cat("Jaja", 5, "jaja");
        var med = Cat("Med i pčelinji proizvodi", 6, "med");
        var zimnica = Cat("Zimnica i džemovi", 7, "zimnica");
        var categories = new[] { povrce, voce, mlijecni, meso, jaja, med, zimnica };
        db.Categories.AddRange(categories);

        // ----- Producers -----
        var kovacevic = new Producer
        {
            Name = "Porodica Kovačević", Village = "Han Kola",
            Description = "Treća generacija koja obrađuje zemlju na obroncima Manjače. Povrće bez pesticida, ubrano istog jutra kad ide na dostavu.",
            PhotoUrl = "/producers/kovacevic.jpg", Latitude = 44.6321, Longitude = 17.0954, Phone = "+387 65 111 222",
        };
        var maric = new Producer
        {
            Name = "OPG Marić", Village = "Stričići",
            Description = "Mala porodična mljekara. Krave na ispaši cijelo ljeto, sir i kajmak po receptu koji se ne mijenja 40 godina.",
            PhotoUrl = "/producers/maric.jpg", Latitude = 44.667, Longitude = 17.02, Phone = "+387 66 333 444",
        };
        var jovanovic = new Producer
        {
            Name = "Gazdinstvo Jovanović", Village = "Rekavice",
            Description = "Pčelinjak sa 120 košnica i voćnjak starih sorti jabuka i šljiva. Med, džemovi i rakija iz vlastite proizvodnje.",
            PhotoUrl = "/producers/jovanovic.jpg", Latitude = 44.6, Longitude = 17.15, Phone = "+387 65 555 666",
        };
        var savic = new Producer
        {
            Name = "Domaćinstvo Savić", Village = "Bronzani Majdan",
            Description = "Živina i jaja iz slobodnog uzgoja, domaća suha slanina i kobasice sušene na bukovom dimu.",
            PhotoUrl = "/producers/savic.jpg", Latitude = 44.72, Longitude = 16.95, Phone = "+387 66 777 888",
        };
        db.Producers.AddRange(kovacevic, maric, jovanovic, savic);

        // ----- Products -----
        var products = new List<Product>
        {
            Prod("Domaći krompir", 1.2m, Unit.Kg, 400, povrce, kovacevic, "Sorta desiree, čuvana u zemljanom podrumu. Idealan za pečenje i variva.", featured: true),
            Prod("Paradajz iz plastenika", 3.5m, Unit.Kg, 80, povrce, kovacevic, "Mesnati paradajz pun ukusa, bran zrio — ne skladišni.", featured: true),
            Prod("Babura paprika", 4.0m, Unit.Kg, 60, povrce, kovacevic, "Debela mesnata babura, savršena za punjenje ili ajvar."),
            Prod("Crni luk", 1.5m, Unit.Kg, 200, povrce, kovacevic, "Ljutkasti domaći luk, dugo se čuva."),
            Prod("Mrkva", 2.0m, Unit.Kg, 120, povrce, kovacevic, "Slatka mrkva iz bašte, bez voska i premaza."),
            Prod("Jabuka idared", 2.2m, Unit.Kg, 150, voce, jovanovic, "Hrskava i sočna, iz voćnjaka na 700m nadmorske visine.", featured: true),
            Prod("Šljiva požegača", 2.8m, Unit.Kg, 90, voce, jovanovic, "Klasična rakijska i džem šljiva, slatka kao med."),
            Prod("Mladi kravlji sir", 9.0m, Unit.Kg, 40, mlijecni, maric, "Punomasni sir od jutarnjeg pomuza, blago slan.", featured: true),
            Prod("Kajmak", 14.0m, Unit.Kg, 25, mlijecni, maric, "Zreli kajmak sa ispaše — pravi domaći doručak."),
            Prod("Domaća pavlaka", 6.0m, Unit.L, 30, mlijecni, maric, "Gusta kisela pavlaka, kašika stoji uspravno."),
            Prod("Svježe kravlje mlijeko", 2.5m, Unit.L, 60, mlijecni, maric, "Nehomogenizovano, prokuhati prije upotrebe."),
            Prod("Jaja iz slobodnog uzgoja", 0.5m, Unit.Kom, 600, jaja, savic, "Koke se šetaju dvorištem, žumance narandžasto.", featured: true),
            Prod("Suha slanina", 18.0m, Unit.Kg, 20, meso, savic, "Dimljena na bukovom dimu, sušena tri mjeseca."),
            Prod("Domaća kobasica", 16.0m, Unit.Kg, 18, meso, savic, "Miješano svinjsko meso, ljuto po tradiciji."),
            Prod("Livadski med", 12.0m, Unit.Kg, 50, med, jovanovic, "Med sa manjačkih livada, kristališe prirodno — znak kvaliteta.", featured: true),
            Prod("Bagremov med", 15.0m, Unit.Kg, 35, med, jovanovic, "Svijetli, blagi med, dugo ostaje tečan."),
            Prod("Džem od šljive", 7.0m, Unit.Kom, 40, zimnica, jovanovic, "Tegla 720ml, kuhan bez konzervansa, 60% voća."),
            Prod("Domaći ajvar", 9.0m, Unit.Kom, 45, zimnica, kovacevic, "Pečena paprika, blagi, tegla 500ml.", available: false),
        };
        db.Products.AddRange(products);

        // ----- Delivery zones & slots -----
        var centar = Zone("Centar", 20, 3);
        var borik = Zone("Borik", 20, 3);
        var laus = Zone("Lauš", 25, 4);
        var starcevica = Zone("Starčevica", 25, 4);
        db.DeliveryZones.AddRange(centar, borik, laus, starcevica);

        var cet = new DeliverySlot { DayOfWeek = 4, TimeFrom = "16:00", TimeTo = "20:00", CutoffHours = 48, Label = "Četvrtak 16–20h" };
        var pet = new DeliverySlot { DayOfWeek = 5, TimeFrom = "16:00", TimeTo = "20:00", CutoffHours = 48, Label = "Petak 16–20h" };
        db.DeliverySlots.AddRange(cet, pet);

        // ----- Users -----
        var admin = new User { Name = "E piljara Admin", Email = "admin@epiljara.ba", Phone = "+387 51 000 000", Role = UserRole.Admin, PasswordHash = hasher.Hash("admin123") };
        var mujo = new User { Name = "Mujo Mujić", Email = "mujo@example.com", Phone = "+387 65 123 456", Address = "Kralja Petra I 12, Banja Luka", DeliveryZoneId = centar.Id, Role = UserRole.Customer, PasswordHash = hasher.Hash("mujo123") };
        var driverUser = new User { Name = "Mladen Vozač", Email = "vozac@epiljara.ba", Phone = "+387 65 900 100", Role = UserRole.Driver, PasswordHash = hasher.Hash("vozac123") };

        // Extra demo customers so route stops show varied names/addresses.
        var fata = new User { Name = "Fata Hadžić", Email = "fata@example.com", Phone = "+387 65 222 333", Address = "Cara Lazara 5, Banja Luka", DeliveryZoneId = borik.Id, Role = UserRole.Customer, PasswordHash = hasher.Hash("fata123") };
        var haso = new User { Name = "Haso Begić", Email = "haso@example.com", Phone = "+387 65 444 555", Address = "Vojvode Momčila 22, Banja Luka", DeliveryZoneId = laus.Id, Role = UserRole.Customer, PasswordHash = hasher.Hash("haso123") };
        var ana = new User { Name = "Ana Kovač", Email = "ana@example.com", Phone = "+387 65 666 777", Address = "Starine Novaka 14, Banja Luka", DeliveryZoneId = starcevica.Id, Role = UserRole.Customer, PasswordHash = hasher.Hash("ana123") };
        foreach (var u in new[] { admin, mujo, driverUser, fata, haso, ana })
            u.EmailConfirmed = true; // demo accounts are pre-confirmed so they can log in
        db.Users.AddRange(admin, mujo, driverUser, fata, haso, ana);

        // ----- Drivers (Mladen linked to the driver user for the mobile app) -----
        var mladen = new Driver { Name = "Mladen (kombi 1)", Phone = "+387 65 900 100", User = driverUser };
        var sasa = new Driver { Name = "Saša (kombi 2)", Phone = "+387 65 900 200" };
        db.Drivers.AddRange(mladen, sasa);

        // ----- Sample orders (today, so the dashboard is populated) -----
        var findProduct = (string name) => products.First(p => p.Name == name);
        var seq = 1;
        db.Orders.AddRange(
            // --- Centar ---
            Order(ref seq, mujo, centar, cet, "Kralja Petra I 12, Banja Luka", OrderStatus.Confirmed,
                (findProduct("Domaći krompir"), 5m), (findProduct("Mladi kravlji sir"), 1m), (findProduct("Jaja iz slobodnog uzgoja"), 10m)),
            Order(ref seq, ana, centar, cet, "Veselina Masleše 30, Banja Luka", OrderStatus.Confirmed,
                (findProduct("Paradajz iz plastenika"), 2m), (findProduct("Domaća pavlaka"), 1m)),
            Order(ref seq, haso, centar, pet, "Gospodska 4, Banja Luka", OrderStatus.OutForDelivery,
                (findProduct("Bagremov med"), 1m), (findProduct("Suha slanina"), 1m)),
            // --- Borik ---
            Order(ref seq, mujo, borik, cet, "Bulevar 42, Banja Luka", OrderStatus.Packing,
                (findProduct("Livadski med"), 2m), (findProduct("Jabuka idared"), 3m)),
            Order(ref seq, fata, borik, cet, "Cara Lazara 5, Banja Luka", OrderStatus.Confirmed,
                (findProduct("Domaća kobasica"), 1m), (findProduct("Crni luk"), 3m)),
            Order(ref seq, ana, borik, pet, "Reljina 18, Banja Luka", OrderStatus.Packing,
                (findProduct("Mrkva"), 2m), (findProduct("Džem od šljive"), 2m)),
            // --- Lauš ---
            Order(ref seq, haso, laus, cet, "Vojvode Momčila 22, Banja Luka", OrderStatus.Confirmed,
                (findProduct("Šljiva požegača"), 4m), (findProduct("Kajmak"), 1m)),
            Order(ref seq, fata, laus, pet, "Braće Pišteljića 9, Banja Luka", OrderStatus.OutForDelivery,
                (findProduct("Babura paprika"), 3m), (findProduct("Svježe kravlje mlijeko"), 3m)),
            // --- Starčevica ---
            Order(ref seq, ana, starcevica, cet, "Starine Novaka 14, Banja Luka", OrderStatus.Confirmed,
                (findProduct("Domaći krompir"), 4m), (findProduct("Livadski med"), 1m)),
            Order(ref seq, haso, starcevica, pet, "Kozarska 7, Banja Luka", OrderStatus.Packing,
                (findProduct("Jaja iz slobodnog uzgoja"), 20m), (findProduct("Mladi kravlji sir"), 2m)),
            // --- Not yet assignable (Pending) — stays out of the route menu ---
            Order(ref seq, mujo, starcevica, cet, "Carice Milice 8, Banja Luka", OrderStatus.Pending,
                (findProduct("Kajmak"), 1m), (findProduct("Svježe kravlje mlijeko"), 4m)));

        await db.SaveChangesAsync(ct);
    }

    private static Category Cat(string name, int order, string slug) =>
        new() { Name = name, Slug = slug, SortOrder = order, ImageUrl = $"/categories/{slug}.jpg" };

    private static Product Prod(string name, decimal price, Unit unit, decimal stock,
        Category category, Producer producer, string description, bool featured = false, bool available = true)
    {
        var slug = Slug.From(name);
        return new Product
        {
            Name = name, Slug = slug, Price = price, Unit = unit, Stock = stock,
            IsAvailable = available, IsFeatured = featured, Description = description,
            ImageUrl = $"/products/{slug}.jpg", Category = category, Producer = producer,
        };
    }

    private static DeliveryZone Zone(string name, decimal min, decimal fee) =>
        new() { Name = name, MinOrderAmount = min, DeliveryFee = fee, IsActive = true };

    private static Order Order(ref int seq, User customer, DeliveryZone zone, DeliverySlot slot,
        string address, OrderStatus status, params (Product product, decimal qty)[] lines)
    {
        var items = lines.Select(l => new OrderItem
        {
            ProductId = l.product.Id,
            ProductName = l.product.Name,
            Unit = l.product.Unit,
            Quantity = l.qty,
            UnitPrice = l.product.Price,
            ImageUrl = l.product.ImageUrl,
        }).ToList();

        var subtotal = Math.Round(items.Sum(i => i.UnitPrice * i.Quantity), 2);
        var now = DateTime.UtcNow;

        return new Order
        {
            Code = $"EP-{now:yyMM}-{seq++:D4}",
            PublicToken = Guid.NewGuid().ToString("N"),
            CustomerId = customer.Id,
            CustomerName = customer.Name,
            CustomerPhone = customer.Phone,
            CustomerEmail = customer.Email,
            Status = status,
            Items = items,
            Subtotal = subtotal,
            DeliveryFee = zone.DeliveryFee,
            TotalAmount = Math.Round(subtotal + zone.DeliveryFee, 2),
            DeliverySlotId = slot.Id,
            DeliveryZoneId = zone.Id,
            DeliveryAddress = address,
            CreatedAt = now,
        };
    }
}
