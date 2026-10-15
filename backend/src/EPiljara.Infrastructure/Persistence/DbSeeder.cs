using EPiljara.Application.Common;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Infrastructure.Persistence;

/// <summary>Idempotent demo seed: Manjača producers, catalog, zones, users, sample orders.</summary>
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, IPasswordHasher hasher, CancellationToken ct = default)
    {
        // Recepti imaju svoj provjeren seed — izvršava se i na bazi koja je već seedovana.
        if (await db.Categories.AnyAsync(ct))
        {
            await SeedReceptiAsync(db, ct);
            await SeedPromoKodoviAsync(db, ct);
            return;
        }

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
            Prod("Krastavac", 2.0m, Unit.Kg, 100, povrce, kovacevic, "Hrskav salatni krastavac bez gorčine, bran svako jutro."),
            Prod("Kupus", 1.0m, Unit.Kg, 150, povrce, kovacevic, "Čvrsta glavica iz jesenje berbe, dobra i za salatu i za zimnicu."),
            Prod("Jabuka idared", 2.2m, Unit.Kg, 150, voce, jovanovic, "Hrskava i sočna, iz voćnjaka na 700m nadmorske visine.", featured: true),
            Prod("Šljiva požegača", 2.8m, Unit.Kg, 90, voce, jovanovic, "Klasična rakijska i džem šljiva, slatka kao med."),
            Prod("Kruška", 2.5m, Unit.Kg, 80, voce, jovanovic, "Jesenja kruška, bere se kad počne sama da pada sa grane."),
            Prod("Grožđe", 3.0m, Unit.Kg, 60, voce, jovanovic, "Stona sorta bez semena, zrna čvrsta i ne pucaju pri branju."),
            Prod("Malina", 6.0m, Unit.Kg, 25, voce, jovanovic, "Bere se u hladu da ne omekša, kutija stoji u frižideru do isporuke."),
            Prod("Dunja", 2.0m, Unit.Kg, 50, voce, jovanovic, "Opora dok je sirova — promijeni miris cijele kuće kad odstoji dan-dva."),
            Prod("Orah", 8.0m, Unit.Kg, 40, voce, jovanovic, "Oljušten domaći orah, krupna jedra, bez gorčine opne."),
            Prod("Mladi kravlji sir", 9.0m, Unit.Kg, 40, mlijecni, maric, "Punomasni sir od jutarnjeg pomuza, blago slan.", featured: true),
            Prod("Kajmak", 14.0m, Unit.Kg, 25, mlijecni, maric, "Zreli kajmak sa ispaše — pravi domaći doručak."),
            Prod("Domaća pavlaka", 6.0m, Unit.L, 30, mlijecni, maric, "Gusta kisela pavlaka, kašika stoji uspravno."),
            Prod("Svježe kravlje mlijeko", 2.5m, Unit.L, 60, mlijecni, maric, "Nehomogenizovano, prokuhati prije upotrebe."),
            Prod("Bijeli sir u rasoli", 10.0m, Unit.Kg, 30, mlijecni, maric, "Slan sir soljen po starom receptu, drži se u rasoli do prodaje."),
            Prod("Skorup", 16.0m, Unit.Kg, 15, mlijecni, maric, "Kožica skupljena sa prokuhanog mlijeka, gušća i masnija od kajmaka."),
            Prod("Domaći jogurt", 3.0m, Unit.L, 40, mlijecni, maric, "Gust domaći jogurt, kiseli se preko noći, bez zgušnjivača."),
            Prod("Jaja iz slobodnog uzgoja", 0.5m, Unit.Kom, 600, jaja, savic, "Koke se šetaju dvorištem, žumance narandžasto.", featured: true),
            Prod("Suha slanina", 18.0m, Unit.Kg, 20, meso, savic, "Dimljena na bukovom dimu, sušena tri mjeseca."),
            Prod("Domaća kobasica", 16.0m, Unit.Kg, 18, meso, savic, "Miješano svinjsko meso, ljuto po tradiciji."),
            Prod("Pečenica", 22.0m, Unit.Kg, 15, meso, savic, "Komad svinjske pečenice, dimljen i sušen mjesec dana."),
            Prod("Sudžuk", 17.0m, Unit.Kg, 20, meso, savic, "Ljuti sudžuk nabijen u crijevo, sušen na promaji dva mjeseca."),
            Prod("Čvarci", 14.0m, Unit.Kg, 25, meso, savic, "Hrskavi čvarci, cijeđeni u kazanu na otvorenoj vatri."),
            Prod("Suho meso", 20.0m, Unit.Kg, 18, meso, savic, "Komad buta sušen uz dimljenje, reže se u tankim listovima."),
            Prod("Pršut", 25.0m, Unit.Kg, 12, meso, savic, "Sušen najmanje šest mjeseci, blago dimljen bukovim drvetom."),
            Prod("Livadski med", 12.0m, Unit.Kg, 50, med, jovanovic, "Med sa manjačkih livada, kristališe prirodno — znak kvaliteta.", featured: true),
            Prod("Bagremov med", 15.0m, Unit.Kg, 35, med, jovanovic, "Svijetli, blagi med, dugo ostaje tečan."),
            Prod("Džem od šljive", 7.0m, Unit.Kom, 40, zimnica, jovanovic, "Tegla 720ml, kuhan bez konzervansa, 60% voća."),
            Prod("Domaći ajvar", 9.0m, Unit.Kom, 45, zimnica, kovacevic, "Pečena paprika, blagi, tegla 500ml.", available: false),
        };
        // Obična roba je podobna za sedmične korpice; korpice same (ispod) ostaju MozeUKorpicu = false.
        foreach (var p in products)
        {
            p.MozeUKorpicu = true;
            p.Mjeseci = [];
        }
        db.Products.AddRange(products);

        // ----- Korpice -----
        // Kategorije su već unesene iznad. "Meso i suhomesnato" (slug "meso") već pokriva
        // suhomesnato (suha slanina, domaća kobasica), pa nema potrebe za zasebnom
        // "suhomesnati-proizvodi" kategorijom. Ista kategorija smije u više tipova —
        // mliječno i suhomesnato idu i u svoj tip i u Domaćinsku.
        var vezeKorpica = new (TipKorpice Tip, string KategorijaSlug)[]
        {
            (TipKorpice.Povrce, "povrce"),
            (TipKorpice.Voce, "voce"),
            (TipKorpice.Mlijecna, "mlijecni-proizvodi"),
            (TipKorpice.Suhomesnata, "meso"),
            (TipKorpice.Domacinska, "mlijecni-proizvodi"),
            (TipKorpice.Domacinska, "meso"),
        };

        foreach (var (tip, slug) in vezeKorpica)
        {
            var kat = categories.FirstOrDefault(k => k.Slug == slug);
            if (kat is null) continue;
            db.KorpicaKategorije.Add(new KorpicaKategorija { TipKorpice = tip, CategoryId = kat.Id });
        }

        var definicije = new (TipKorpice Tip, string Naziv, string Opis)[]
        {
            (TipKorpice.Povrce, "korpica povrća", "Sezonsko povrće sa Manjače, ubrano istog jutra."),
            (TipKorpice.Voce, "korpica voća", "Sezonsko voće sa domaćih gazdinstava."),
            (TipKorpice.Mlijecna, "mliječna korpica", "Sir, kajmak, mlijeko i pavlaka sa Manjače."),
            (TipKorpice.Suhomesnata, "suhomesnata korpica", "Pečenica, slanina, kobasica i suho meso."),
            (TipKorpice.Domacinska, "domaćinska korpica", "Miješano: komad pečenice, slanina, mlijeko i sir."),
        };

        var velicine = new (VelicinaKorpice V, string Prefiks, decimal Faktor, string Clanovi)[]
        {
            (VelicinaKorpice.Mala, "Mala", 1.0m, "Za 1 do 2 člana"),
            (VelicinaKorpice.Srednja, "Srednja", 1.4m, "Za 2 do 4 člana"),
            (VelicinaKorpice.Velika, "Velika", 1.8m, "Za 5 i više članova"),
        };

        var osnovneCijene = new Dictionary<TipKorpice, decimal>
        {
            [TipKorpice.Povrce] = 25m,
            [TipKorpice.Voce] = 28m,
            [TipKorpice.Mlijecna] = 32m,
            [TipKorpice.Suhomesnata] = 45m,
            [TipKorpice.Domacinska] = 48m,
        };

        // Korpice i same moraju imati kategoriju/proizvođača (strani ključ), iako se
        // ni jedno ni drugo ne prikazuje za korpicu (vidi KorpiceQueries.ToKorpicaDto) —
        // Povrće/Kovačević su samo neškodljiv placeholder.
        var prvaKategorija = povrce;
        var prviProizvodjac = kovacevic;

        foreach (var (tip, naziv, opis) in definicije)
        foreach (var (v, prefiks, faktor, clanovi) in velicine)
        {
            var punNaziv = $"{prefiks} {naziv}";
            var slug = Slug.From(punNaziv);
            var korpica = new Product
            {
                Name = punNaziv,
                Slug = slug,
                Price = Math.Round(osnovneCijene[tip] * faktor, 0),
                Unit = Unit.Kom,
                Stock = 40,
                IsAvailable = true,
                Description = $"{opis} {clanovi}.",
                ImageUrl = tip switch
                {
                    TipKorpice.Povrce => "/categories/povrce.jpg",
                    TipKorpice.Voce => "/categories/voce.jpg",
                    TipKorpice.Mlijecna => "/products/mladi-kravlji-sir.jpg",
                    TipKorpice.Suhomesnata => "/products/suha-slanina.jpg",
                    TipKorpice.Domacinska => "/hero/hero-domacinstvo.jpg",
                    _ => throw new InvalidOperationException($"Nepoznat tip korpice: {tip}"),
                },
                Category = prvaKategorija,
                Producer = prviProizvodjac,
                Vrsta = VrstaProizvoda.Korpica,
                TipKorpice = tip,
                Velicina = v,
            };
            korpica.ProvjeriInvarijantuKorpice();
            db.Products.Add(korpica);
        }

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
        var marko = new User { Name = "Marko Marković", Email = "marko@example.com", Phone = "+387 65 123 456", Address = "Kralja Petra I 12, Banja Luka", DeliveryZoneId = centar.Id, Role = UserRole.Customer, PasswordHash = hasher.Hash("marko123") };
        var driverUser = new User { Name = "Mladen Vozač", Email = "vozac@epiljara.ba", Phone = "+387 65 900 100", Role = UserRole.Driver, PasswordHash = hasher.Hash("vozac123") };

        // Extra demo customers so route stops show varied names/addresses.
        var jelena = new User { Name = "Jelena Petrović", Email = "jelena@example.com", Phone = "+387 65 222 333", Address = "Cara Lazara 5, Banja Luka", DeliveryZoneId = borik.Id, Role = UserRole.Customer, PasswordHash = hasher.Hash("jelena123") };
        var dragan = new User { Name = "Dragan Stojanović", Email = "dragan@example.com", Phone = "+387 65 444 555", Address = "Vojvode Momčila 22, Banja Luka", DeliveryZoneId = laus.Id, Role = UserRole.Customer, PasswordHash = hasher.Hash("dragan123") };
        var ana = new User { Name = "Ana Kovač", Email = "ana@example.com", Phone = "+387 65 666 777", Address = "Starine Novaka 14, Banja Luka", DeliveryZoneId = starcevica.Id, Role = UserRole.Customer, PasswordHash = hasher.Hash("ana123") };
        foreach (var u in new[] { admin, marko, driverUser, jelena, dragan, ana })
            u.EmailConfirmed = true; // demo accounts are pre-confirmed so they can log in
        db.Users.AddRange(admin, marko, driverUser, jelena, dragan, ana);

        // ----- Drivers (Mladen linked to the driver user for the mobile app) -----
        var mladen = new Driver { Name = "Mladen (kombi 1)", Phone = "+387 65 900 100", User = driverUser };
        var sasa = new Driver { Name = "Saša (kombi 2)", Phone = "+387 65 900 200" };
        db.Drivers.AddRange(mladen, sasa);

        // ----- Sample orders (today, so the dashboard is populated) -----
        var findProduct = (string name) => products.First(p => p.Name == name);
        var seq = 1;
        db.Orders.AddRange(
            // --- Centar ---
            Order(ref seq, marko, centar, cet, "Kralja Petra I 12, Banja Luka", OrderStatus.Confirmed,
                (findProduct("Domaći krompir"), 5m), (findProduct("Mladi kravlji sir"), 1m), (findProduct("Jaja iz slobodnog uzgoja"), 10m)),
            Order(ref seq, ana, centar, cet, "Veselina Masleše 30, Banja Luka", OrderStatus.Confirmed,
                (findProduct("Paradajz iz plastenika"), 2m), (findProduct("Domaća pavlaka"), 1m)),
            Order(ref seq, dragan, centar, pet, "Gospodska 4, Banja Luka", OrderStatus.OutForDelivery,
                (findProduct("Bagremov med"), 1m), (findProduct("Suha slanina"), 1m)),
            // --- Borik ---
            Order(ref seq, marko, borik, cet, "Bulevar 42, Banja Luka", OrderStatus.Packing,
                (findProduct("Livadski med"), 2m), (findProduct("Jabuka idared"), 3m)),
            Order(ref seq, jelena, borik, cet, "Cara Lazara 5, Banja Luka", OrderStatus.Confirmed,
                (findProduct("Domaća kobasica"), 1m), (findProduct("Crni luk"), 3m)),
            Order(ref seq, ana, borik, pet, "Reljina 18, Banja Luka", OrderStatus.Packing,
                (findProduct("Mrkva"), 2m), (findProduct("Džem od šljive"), 2m)),
            // --- Lauš ---
            Order(ref seq, dragan, laus, cet, "Vojvode Momčila 22, Banja Luka", OrderStatus.Confirmed,
                (findProduct("Šljiva požegača"), 4m), (findProduct("Kajmak"), 1m)),
            Order(ref seq, jelena, laus, pet, "Braće Pišteljića 9, Banja Luka", OrderStatus.OutForDelivery,
                (findProduct("Babura paprika"), 3m), (findProduct("Svježe kravlje mlijeko"), 3m)),
            // --- Starčevica ---
            Order(ref seq, ana, starcevica, cet, "Starine Novaka 14, Banja Luka", OrderStatus.Confirmed,
                (findProduct("Domaći krompir"), 4m), (findProduct("Livadski med"), 1m)),
            Order(ref seq, dragan, starcevica, pet, "Kozarska 7, Banja Luka", OrderStatus.Packing,
                (findProduct("Jaja iz slobodnog uzgoja"), 20m), (findProduct("Mladi kravlji sir"), 2m)),
            // --- Not yet assignable (Pending) — stays out of the route menu ---
            Order(ref seq, marko, starcevica, cet, "Carice Milice 8, Banja Luka", OrderStatus.Pending,
                (findProduct("Kajmak"), 1m), (findProduct("Svježe kravlje mlijeko"), 4m)));

        // ----- Pretplata (P3) -----
        // Jedna aktivna, sa četiri isporuke iza sebe: ekran se tako vidi u oba stanja —
        // traka „još jedna isporuka do niže cijene" sada, a pretplatnička cijena poslije
        // naredne isporuke. Narudžbe za te četiri isporuke se ne izmišljaju; brojač je
        // izveden podatak i demo baza ne mora da ima istoriju koja ga potkrepljuje.
        db.Pretplate.Add(new Pretplata
        {
            UserId = marko.Id,
            ProductId = db.Products.Local.First(p =>
                p.Vrsta == VrstaProizvoda.Korpica
                && p.TipKorpice == TipKorpice.Domacinska
                && p.Velicina == VelicinaKorpice.Mala).Id,
            Status = StatusPretplate.Aktivna,
            DeliveryZoneId = centar.Id,
            DeliverySlotId = cet.Id,
            DeliveryAddress = "Kralja Petra I 12, Banja Luka",
            BrojIsporuka = 4,
        });

        await db.SaveChangesAsync(ct);
        await SeedReceptiAsync(db, ct);
        await SeedPromoKodoviAsync(db, ct);
    }

    /// <summary>Demo promo kod; idempotentno kao i recepti, i samo u demo seedu.</summary>
    private static async Task SeedPromoKodoviAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.PromoKodovi.AnyAsync(ct))
            return;

        db.PromoKodovi.Add(new PromoKod
        {
            Kod = "DOBRODOSLI",
            Opis = "Dobrodošlica — 10 % na prvu narudžbu",
            Tip = TipPopusta.Procenat,
            Vrijednost = 10m,
            SamoNoviKupci = true,
            Aktivan = true,
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Dva demo recepta (spec O3); sastojci se vezuju za seed proizvode po imenu.</summary>
    private static async Task SeedReceptiAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.Recepti.AnyAsync(ct))
            return;

        var proizvodi = await db.Products.Select(p => new { p.Name, p.Id }).ToListAsync(ct);
        ReceptSastojak S(string naziv, string kolicina, string? proizvod, int redoslijed) => new()
        {
            Naziv = naziv, Kolicina = kolicina, Redoslijed = redoslijed,
            // Proizvod koji ne postoji u bazi ostavlja sastojak bez veze (bez dugmeta).
            ProductId = proizvod is null ? null : proizvodi.FirstOrDefault(p => p.Name == proizvod)?.Id,
        };

        db.Recepti.AddRange(
            new Recept
            {
                Naslov = "Kajgana sa mladim sirom i paprikom", Slug = "kajgana-sa-mladim-sirom-i-paprikom",
                Opis = "Brz i sit doručak od jaja, mladog sira i hrskave babure.",
                VrijemeMinuta = 15, Porcije = 2, Tezina = TezinaRecepta.Lako, Objavljen = true,
                Koraci = "Papriku operi, očisti od sjemenki i isjeci na sitne kocke, a luk sitno nasjeckaj.\n"
                    + "Na zagrijanoj masnoći propirji luk i papriku dok ne omekšaju, oko pet minuta.\n"
                    + "Razbij jaja, prelij preko povrća i miješaj na srednjoj vatri.\n"
                    + "Kad se jaja skoro stegnu, dodaj izmrvljen mladi sir, posoli i odmah služi.",
                Sastojci =
                [
                    S("Jaja iz slobodnog uzgoja", "4 kom", "Jaja iz slobodnog uzgoja", 0),
                    S("Mladi kravlji sir", "100 g", "Mladi kravlji sir", 1),
                    S("Babura paprika", "1 kom", "Babura paprika", 2),
                    S("Crni luk", "1/2 glavice", "Crni luk", 3),
                    S("So", "po ukusu", null, 4),
                ],
            },
            new Recept
            {
                Naslov = "Krompir salata sa pavlakom", Slug = "krompir-salata-sa-pavlakom",
                Opis = "Topla krompir salata sa lukom i domaćom pavlakom, uz svaku pečenu hranu.",
                VrijemeMinuta = 35, Porcije = 4, Tezina = TezinaRecepta.Lako, Objavljen = true,
                Koraci = "Krompir dobro operi i skuhaj u ljusci dvadesetak minuta, dok ne omekša.\n"
                    + "Ocijedi ga, malo prohladi, oljušti i isjeci na kolutove.\n"
                    + "Luk sitno nasjeckaj i pomiješaj sa još toplim krompirom.\n"
                    + "Umiješaj pavlaku, posoli i pobiberi, pa ostavi desetak minuta da se sjedine okusi.",
                Sastojci =
                [
                    S("Domaći krompir", "1 kg", "Domaći krompir", 0),
                    S("Crni luk", "1 glavica", "Crni luk", 1),
                    S("Domaća pavlaka", "200 g", "Domaća pavlaka", 2),
                    S("So i biber", "po ukusu", null, 3),
                ],
            });

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
