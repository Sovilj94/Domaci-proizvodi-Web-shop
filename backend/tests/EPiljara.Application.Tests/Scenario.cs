using EPiljara.Application.DTOs;
using EPiljara.Application.Features.Korpice;
using EPiljara.Application.Features.Orders;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using EPiljara.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Unit = EPiljara.Domain.Enums.Unit;

namespace EPiljara.Application.Tests;

/// <summary>
/// Zajednički pomoćnik za P2b testove (Taskovi 5–8). Pravi zonu, termin, opcije korpica,
/// dvije kategorije, proizvođača, puni skup artikala iz scenario-spec.md i korpicu — a kad
/// se traži, i objavljen sedmični sadržaj za sedmicu koju narudžba zaista isporučuje.
///
/// Napravljen u punom obliku odjednom (Task 5 koristio je samo dio): da ga ne prepravljamo u
/// svakom od Taskova 6–8, što bi rizikovalo testove koji su već prošli. Sve je sada ovdje —
/// naručivanje (Task 5), pakerska lista (Task 6), upiti editora (Task 7), izmjena sa
/// graničnim vrijednostima budžeta (Task 8) i putanja gosta (ispravke finalnog review-a).
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

    /// <summary>
    /// Luk, 2.65 KM. Dodat uz ispravke finalnog review-a: nijedna kombinacija postojećih
    /// artikala nije mogla sastaviti tačno donju granicu budžeta. [Krompir, Granica265] je
    /// 2×2.00 + 1×2.65 = 6.65, a to je tačno <c>Math.Round(7.00m * 0.95m, 2)</c>.
    /// </summary>
    public Guid Granica265Id { get; }

    public int Godina { get; }
    public int Sedmica { get; }

    /// <summary>
    /// Mjesec koji NIJE mjesec isporuke — isti koji nosi <see cref="VanSezoneId"/>. Izložen je
    /// da test može poslati postojeći artikal van sezone bez zakucanog mjeseca.
    /// </summary>
    public int VanSezoneMjesec { get; }

    private Scenario(
        TestDb t, bool prijavljen, Guid zonaId, Guid slotId, KorpiceOpcije opcije, Guid kupacId,
        Guid korpicaId, Guid krompirId, Guid paprikaId, Guid sirId, Guid skupiArtikalId,
        Guid jeftin264Id, Guid skup301Id, Guid vanSezoneId, Guid granica265Id,
        int godina, int sedmica, int vanSezoneMjesec)
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
        Granica265Id = granica265Id;
        Godina = godina;
        Sedmica = sedmica;
        VanSezoneMjesec = vanSezoneMjesec;
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
        var granica265 = new Product
        {
            // Donja granica budžeta, tačno na njoj: [Krompir, Granica265] = 4.00 + 2.65 = 6.65,
            // a Math.Round(7.00m * 0.95m, 2) je 6.65. Bez ovog artikla nijedna kombinacija ne
            // pada na samu granicu — 6.64 i 7.01 je okružuju, ali je ne dotiču.
            Name = "Luk", Slug = "luk", Price = 2.65m, Unit = Unit.Kom,
            Stock = 100, IsAvailable = true, MozeUKorpicu = true,
            Vrsta = VrstaProizvoda.Obican, Mjeseci = [],
            Category = povrce, Producer = proizvodjac,
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
            krompir, paprika, sir, skupiArtikal, jeftin264, skup301, granica265, vanSezone,
            korpica);
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
            jeftin264.Id, skup301.Id, vanSezone.Id, granica265.Id,
            godina, sedmica, vanSezoneMjesec);
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

    // ---------- Task 6 ----------

    public Task<PakerskaListaDto> PakerskaListaAsync() =>
        new GetPakerskaListaHandler(Db).Handle(new GetPakerskaListaQuery(Godina, Sedmica), default);

    // ---------- Task 7 ----------

    public async Task<Guid> OrderItemIdAsync(Guid orderId) =>
        (await Db.OrderItems.FirstAsync(i => i.OrderId == orderId)).Id;

    public Task<IReadOnlyList<MojaKorpicaDto>> MojeKorpiceAsync(Guid? kao = null) =>
        new GetMojeKorpiceHandler(Db, KorisnikZa(kao), Options.Create(Opcije))
            .Handle(new GetMojeKorpiceQuery(), default);

    public Task<MojaKorpicaDetaljiDto> MojaKorpicaAsync(Guid orderItemId, Guid? kao = null) =>
        new GetMojaKorpicaHandler(Db, KorisnikZa(kao), Options.Create(Opcije))
            .Handle(new GetMojaKorpicaQuery(orderItemId), default);

    private FakeCurrentUser KorisnikZa(Guid? kao) => new(kao ?? KupacId);

    // ---------- Task 8 ----------

    public Task<MojaKorpicaDetaljiDto> IzmijeniAsync(
        Guid orderItemId, Guid[] productIds, Guid? kao = null)
    {
        var korisnik = KorisnikZa(kao);
        var mediator = new FakeMediator(Db, korisnik, Options.Create(Opcije));
        return new IzmijeniMojuKorpicuHandler(Db, korisnik, Options.Create(Opcije), mediator)
            .Handle(new IzmijeniMojuKorpicuCommand(orderItemId, productIds), default);
    }

    // ---------- Ispravke finalnog review-a ----------

    /// <summary>
    /// Zahtjev gosta za korpicu, pa token iz upisanog reda. Gost zahtjev ne nosi sedmicu ni
    /// sadržaj — oboje nastaje na potvrdu, vidi <see cref="PotvrdiGostaAsync"/>.
    /// Zahtijeva <c>prijavljen: false</c> (podrazumijevano), jer
    /// CreateGuestOrderRequestHandler odbija prijavljenog kupca.
    /// </summary>
    public async Task<string> ZahtjevGostaAsync(Guid productId, decimal kolicina = 1m)
    {
        await new CreateGuestOrderRequestHandler(Db, new FakeCurrentUser(null), new FakeEmail())
            .Handle(KomandaGosta(productId, kolicina), default);
        return (await Db.GuestOrderRequests.OrderBy(r => r.CreatedAt).LastAsync()).Token;
    }

    public Task<OrderDto> PotvrdiGostaAsync(string token) =>
        new ConfirmGuestOrderHandler(
                Db, new FakeEmail(), new FakeViber(), Options.Create(Opcije))
            .Handle(new ConfirmGuestOrderCommand(token), default);

    private CreateGuestOrderRequestCommand KomandaGosta(Guid productId, decimal kolicina) => new(
        new CustomerInput("Test Gost", "gost@example.com", "065 000 001"),
        _zonaId, _slotId, "Testna adresa 1", null,
        [new OrderItemInput(productId, kolicina)]);

    /// <summary>
    /// Pomjera sedmicu narudžbe u prošlost, da rok izmjene padne iza nas. Vrijeme se NE čeka
    /// (nikakav Sleep) — rok se izvodi iz <c>Order.Godina/Sedmica</c>, pa se pomjera taj ulaz.
    /// Prelaz preko granice godine ide kroz ISOWeek, ne ručnim odbijanjem.
    /// </summary>
    public async Task PomjeriSedmicuUProslostAsync(Guid orderId, int sedmicaNazad = 2)
    {
        var ponedjeljak = System.Globalization.ISOWeek
            .ToDateTime(Godina, Sedmica, DayOfWeek.Monday)
            .AddDays(-7 * sedmicaNazad);

        var o = await Db.Orders.FirstAsync(x => x.Id == orderId);
        o.Godina = System.Globalization.ISOWeek.GetYear(ponedjeljak);
        o.Sedmica = System.Globalization.ISOWeek.GetWeekOfYear(ponedjeljak);
        await Db.SaveChangesAsync();
    }

    /// <summary>
    /// `94.9` i `100.1` su preslikani direktno na korpe iz tabele u scenario-spec.md:
    /// [Krompir, Jeftin264] = 6.64 (ispod donje granice 6.65) i [Krompir, Skup301] = 7.01
    /// (iznad gornje granice 7.00). Ovo NIJE računica — svaki drugi broj bi zahtijevao
    /// pogađanje koje artikle sastaviti, a test bi onda mogao proći iz pogrešnog razloga.
    ///
    /// `95.0` je dodat uz ispravke finalnog review-a: [Krompir, Granica265] = 4.00 + 2.65 =
    /// 6.65, tačno na inkluzivnoj donjoj granici. Granica je dotad bila dokazana s obje
    /// strane (6.64 pada, 7.00 prolazi), ali ne na sebi samoj.
    /// </summary>
    public Task<MojaKorpicaDetaljiDto> IzmijeniNaProcenatAsync(Guid orderItemId, double procenat)
    {
        Guid[] productIds = procenat switch
        {
            94.9 => [KrompirId, Jeftin264Id],
            95.0 => [KrompirId, Granica265Id],
            100.1 => [KrompirId, Skup301Id],
            _ => throw new ArgumentOutOfRangeException(
                nameof(procenat), procenat, "Nema pripremljene korpe za ovaj procenat."),
        };
        return IzmijeniAsync(orderItemId, productIds);
    }

    /// <summary>
    /// Mediator koji hendler Taska 8 koristi SAMO za <see cref="GetMojaKorpicaQuery"/>, da
    /// vrati svježe stanje poslije izmjene bez drugog round-trip-a u testu. Ostali članovi
    /// IMediator-a se u ovom scenariju ne zovu.
    /// </summary>
    private sealed class FakeMediator(
        AppDbContext db, FakeCurrentUser current, IOptions<KorpiceOpcije> opcije) : IMediator
    {
        public Task<TResponse> Send<TResponse>(
            IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is GetMojaKorpicaQuery q)
                return (Task<TResponse>)(object)new GetMojaKorpicaHandler(db, current, opcije)
                    .Handle(q, cancellationToken);
            throw new NotImplementedException(
                $"FakeMediator ne zna da posluži {request.GetType().Name}.");
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task Send<TRequest>(
            TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest =>
            throw new NotImplementedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public IAsyncEnumerable<object?> CreateStream(
            object request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task Publish<TNotification>(
            TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification =>
            throw new NotImplementedException();
    }

    public void Dispose() => _t.Dispose();
}
