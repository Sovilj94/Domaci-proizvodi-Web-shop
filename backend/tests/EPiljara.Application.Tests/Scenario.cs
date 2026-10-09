using EPiljara.Application.DTOs;
using EPiljara.Application.Features.Korpice;
using EPiljara.Application.Features.Orders;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using EPiljara.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Tests;

/// <summary>
/// Zajednički pomoćnik za P2b testove (Taskovi 5–8). Pravi zonu, termin, opcije korpica,
/// dvije kategorije, proizvođača, puni skup artikala iz scenario-spec.md i korpicu — a kad
/// se traži, i objavljen sedmični sadržaj za sedmicu koju narudžba zaista isporučuje.
///
/// Napravljen u punom obliku odjednom (Task 5 koristi samo dio): da ga ne prepravljamo u
/// svakom od Taskova 6–8, što bi rizikovalo testove koji su već prošli. Metode specifične za
/// Taskove 6–8 (pakerska lista, upiti editora, izmjena) NISU ovdje — njihovi tipovi povratne
/// vrijednosti (PakerskaListaDto, MojaKorpicaDto, MojaKorpicaDetaljiDto) još ne postoje, pa bi
/// fajl pucao. Dodaju ih ti taskovi, uz polja i podatke koji su već spremni.
/// </summary>
internal sealed class Scenario : IDisposable
{
    private readonly TestDb _t;
    private readonly bool _prijavljen;
    private readonly Guid _zonaId;
    private readonly Guid _slotId;

    public AppDbContext Db => _t.Db;
    public KorpiceOpcije Opcije { get; }

    /// <summary>Guid.Empty kad prijavljen == false (gost nema nalog).</summary>
    public Guid KupacId { get; }

    public Guid KorpicaId { get; }
    public Guid KrompirId { get; }
    public Guid PaprikaId { get; }
    public Guid SirId { get; }
    public Guid SkupiArtikalId { get; }
    public Guid Jeftin264Id { get; }
    public Guid Skup301Id { get; }
    public Guid VanSezoneId { get; }

    public int Godina { get; }
    public int Sedmica { get; }

    private Scenario(
        TestDb t, bool prijavljen, Guid zonaId, Guid slotId, KorpiceOpcije opcije, Guid kupacId,
        Guid korpicaId, Guid krompirId, Guid paprikaId, Guid sirId, Guid skupiArtikalId,
        Guid jeftin264Id, Guid skup301Id, Guid vanSezoneId, int godina, int sedmica)
    {
        _t = t;
        _prijavljen = prijavljen;
        _zonaId = zonaId;
        _slotId = slotId;
        Opcije = opcije;
        KupacId = kupacId;
        KorpicaId = korpicaId;
        KrompirId = krompirId;
        PaprikaId = paprikaId;
        SirId = sirId;
        SkupiArtikalId = skupiArtikalId;
        Jeftin264Id = jeftin264Id;
        Skup301Id = skup301Id;
        VanSezoneId = vanSezoneId;
        Godina = godina;
        Sedmica = sedmica;
    }

    public static async Task<Scenario> NapraviAsync(
        bool objaviSadrzaj, bool prijavljen = false, decimal ciljnaVrijednost = 7m)
    {
        var t = new TestDb();

        var zona = new DeliveryZone
        {
            Name = "Centar", MinOrderAmount = 0m, DeliveryFee = 3m, IsActive = true,
        };
        var slot = new DeliverySlot
        {
            DayOfWeek = 4, TimeFrom = "16:00", TimeTo = "20:00", CutoffHours = 48,
        };
        t.Db.DeliveryZones.Add(zona);
        t.Db.DeliverySlots.Add(slot);

        var povrce = new Category { Name = "Povrće", Slug = "povrce" };
        var mlijecni = new Category { Name = "Mliječni", Slug = "mlijecni" };
        t.Db.Categories.AddRange(povrce, mlijecni);

        var proizvodjac = new Producer { Name = "Marko", Village = "Manjača" };
        t.Db.Producers.Add(proizvodjac);

        await t.Db.SaveChangesAsync();

        // Podrazumijevanih 5 bi obaralo svaki test, jer sadržaj scenarija ima dva artikla.
        var opcije = new KorpiceOpcije();
        opcije.MinimalanBrojArtikala[VelicinaKorpice.Mala] = 2;

        // Sedmica se nikad ne zakucava — test koji prolazi u oktobru a pada u decembru/
        // januaru je gori od testa koji ne postoji.
        var (dan, vrijemeOd, cutoff) = await SedmicaHelper.UcitajRokAsync(t.Db, opcije, default);
        var (godina, sedmica) = SedmicaHelper.SedmicaZaNarudzbu(dan, vrijemeOd, cutoff);

        var mjesecIsporuke = SedmicaHelper
            .DatumIsporuke(godina, sedmica, DayOfWeek.Monday, "00:00").Month;
        var vanSezoneMjesec = mjesecIsporuke == 1 ? 2 : 1;

        var krompir = new Product
        {
            Name = "Krompir", Slug = "krompir", Price = 2.00m, Unit = Unit.Kg,
            Stock = 100, IsAvailable = true, MozeUKorpicu = true,
            Vrsta = VrstaProizvoda.Obican, Mjeseci = [],
            Category = povrce, Producer = proizvodjac,
        };
        var paprika = new Product
        {
            Name = "Paprika", Slug = "paprika", Price = 3.00m, Unit = Unit.Kg,
            Stock = 100, IsAvailable = true, MozeUKorpicu = true,
            Vrsta = VrstaProizvoda.Obican, Mjeseci = [],
            Category = povrce, Producer = proizvodjac,
        };
        var sir = new Product
        {
            // Druga kategorija (Mliječni) → IzOveKorpice == false u Tasku 8, jer korpica
            // je vezana samo za Povrće preko KorpicaKategorija ispod.
            Name = "Sir", Slug = "sir", Price = 3.00m, Unit = Unit.Kom,
            Stock = 100, IsAvailable = true, MozeUKorpicu = true,
            Vrsta = VrstaProizvoda.Obican, Mjeseci = [],
            Category = mlijecni, Producer = proizvodjac,
        };
        var skupiArtikal = new Product
        {
            // Sam vrijedi tačno 100% budžeta (Task 8: [SkupiArtikal] = 7.00, pa pada na
            // premalo_artikala jer MinimalanBrojArtikala[Mala] == 2).
            Name = "Pečenica", Slug = "pecenica", Price = 7.00m, Unit = Unit.Kom,
            Stock = 100, IsAvailable = true, MozeUKorpicu = true,
            Vrsta = VrstaProizvoda.Obican, Mjeseci = [],
            Category = povrce, Producer = proizvodjac,
        };
        var jeftin264 = new Product
        {
            // Donja granica budžeta (Task 8): [Krompir, Jeftin264] = 6.64, ispod 6.65.
            // Unit.Veza ne postoji u domenskom enumu (Kg/Kom/L) — najbliže je Kom;
            // vidi task-5-report.md za objašnjenje ovog odstupanja od scenario-spec.md.
            Name = "Blitva", Slug = "blitva", Price = 2.64m, Unit = Unit.Kom,
            Stock = 100, IsAvailable = true, MozeUKorpicu = true,
            Vrsta = VrstaProizvoda.Obican, Mjeseci = [],
            Category = povrce, Producer = proizvodjac,
        };
        var skup301 = new Product
        {
            // Gornja granica budžeta (Task 8): [Krompir, Skup301] = 7.01, iznad 7.00.
            Name = "Kajmak", Slug = "kajmak", Price = 3.01m, Unit = Unit.Kom,
            Stock = 100, IsAvailable = true, MozeUKorpicu = true,
            Vrsta = VrstaProizvoda.Obican, Mjeseci = [],
            Category = mlijecni, Producer = proizvodjac,
        };
        var vanSezone = new Product
        {
            // Mjesec koji NIJE mjesec isporuke — izračunat, nikad zakucan.
            Name = "Kupus", Slug = "kupus", Price = 2.00m, Unit = Unit.Kg,
            Stock = 100, IsAvailable = true, MozeUKorpicu = true,
            Vrsta = VrstaProizvoda.Obican, Mjeseci = [vanSezoneMjesec],
            Category = povrce, Producer = proizvodjac,
        };

        var korpica = new Product
        {
            Name = "Mala korpica povrća", Slug = "mala-korpica-povrca", Price = 10m,
            Unit = Unit.Kom, Stock = 100, IsAvailable = true,
            Vrsta = VrstaProizvoda.Korpica, TipKorpice = TipKorpice.Povrce,
            Velicina = VelicinaKorpice.Mala, MozeUKorpicu = false,
            Category = povrce, Producer = proizvodjac,
        };

        t.Db.Products.AddRange(
            krompir, paprika, sir, skupiArtikal, jeftin264, skup301, vanSezone, korpica);
        await t.Db.SaveChangesAsync();

        // Korpica se puni samo iz Povrća — Sir i Kajmak (Mliječni) ostaju van nje.
        t.Db.KorpicaKategorije.Add(new KorpicaKategorija
        {
            TipKorpice = TipKorpice.Povrce, CategoryId = povrce.Id,
        });
        await t.Db.SaveChangesAsync();

        if (objaviSadrzaj)
        {
            var sadrzaj = new SedmicniSadrzaj
            {
                KorpicaId = korpica.Id, Godina = godina, Sedmica = sedmica,
                Status = StatusSadrzaja.Objavljen, CiljnaVrijednost = ciljnaVrijednost,
            };
            sadrzaj.Stavke.Add(new StavkaSadrzaja
            {
                ProductId = krompir.Id, Kolicina = 2m, JedinicnaCijena = 2m,
            });
            sadrzaj.Stavke.Add(new StavkaSadrzaja
            {
                ProductId = paprika.Id, Kolicina = 1m, JedinicnaCijena = 3m,
            });
            t.Db.SedmicniSadrzaji.Add(sadrzaj);
            await t.Db.SaveChangesAsync();
        }

        var kupacId = prijavljen ? Guid.NewGuid() : Guid.Empty;

        return new Scenario(
            t, prijavljen, zona.Id, slot.Id, opcije, kupacId,
            korpica.Id, krompir.Id, paprika.Id, sir.Id, skupiArtikal.Id,
            jeftin264.Id, skup301.Id, vanSezone.Id, godina, sedmica);
    }

    // ---------- Task 5 ----------

    public Task<OrderDto> PosaljiKorpicuAsync(decimal kolicina) =>
        Handler().Handle(Komanda(KorpicaId, kolicina), default);

    public Task<OrderDto> PosaljiObicanArtikalAsync() =>
        Handler().Handle(Komanda(KrompirId, 1m), default);

    private CreateOrderHandler Handler() => new(
        Db, new FakeCurrentUser(_prijavljen ? KupacId : null), new FakeEmail(), new FakeViber(),
        Options.Create(Opcije));

    private CreateOrderCommand Komanda(Guid productId, decimal kolicina) => new(
        new CustomerInput("Test Kupac", "test@example.com", "065 000 000"),
        _zonaId, _slotId, "Testna adresa 1", null,
        [new OrderItemInput(productId, kolicina)]);

    public void Dispose() => _t.Dispose();
}
