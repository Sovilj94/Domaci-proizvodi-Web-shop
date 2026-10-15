using EPiljara.Application.Features.Poeni;
using EPiljara.Application.Features.Korpice;
using EPiljara.Application.Features.Orders;
using EPiljara.Application.Features.Pretplate;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using EPiljara.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Unit = EPiljara.Domain.Enums.Unit;

namespace EPiljara.Application.Tests;

/// <summary>
/// Posao koji pravi pretplatne narudžbe je jedino mjesto u sistemu koje pravi narudžbu bez
/// kupca pred ekranom, pa se njegova greška ne vidi kao poruka nego kao kutija na stolu i
/// račun pouzećem. Zato prvi test nije sretan slučaj nego dupli prolaz.
/// </summary>
public class PretplatneNarudzbeTests
{
    /// <summary>
    /// Prozor „koliko unaprijed se pravi narudžba" je namjerno otvoren do kraja u većini
    /// testova: inače bi prolaznost zavisila od toga koji je danas dan. Zatvaranje prozora
    /// ima svoj test ispod.
    /// </summary>
    private static PretplateOpcije Opcije(int satiUnaprijed = 100_000) =>
        new() { SatiUnaprijed = satiUnaprijed };

    private sealed class Scena : IDisposable
    {
        private readonly TestDb _t;
        public AppDbContext Db => _t.Db;
        public Pretplata Pretplata { get; private set; } = null!;
        public Product Korpica { get; private set; } = null!;
        public int Godina { get; private set; }
        public int Sedmica { get; private set; }

        private Scena(TestDb t) => _t = t;

        public static async Task<Scena> SpremiAsync(
            StatusPretplate status = StatusPretplate.Aktivna,
            bool sadrzajObjavljen = true,
            int brojIsporuka = 0)
        {
            var s = new Scena(new TestDb());
            var db = s.Db;

            var zona = new DeliveryZone { Name = "Centar", MinOrderAmount = 0m, DeliveryFee = 3m, IsActive = true };
            var slot = new DeliverySlot { DayOfWeek = 4, TimeFrom = "16:00", TimeTo = "20:00", CutoffHours = 48 };
            var kat = new Category { Name = "Povrće", Slug = "povrce" };
            var proizvodjac = new Producer { Name = "Marko", Village = "Manjača" };
            var kupac = new User
            {
                Name = "Marko Marković", Email = "marko@test.local", Phone = "065 111 222",
                PasswordHash = "x", Role = UserRole.Customer,
            };
            var korpica = new Product
            {
                Name = "Mala domaćinska korpica", Slug = "mala-domacinska", Price = 25m,
                Unit = Unit.Kom, Stock = 100, IsAvailable = true, Category = kat, Producer = proizvodjac,
                Vrsta = VrstaProizvoda.Korpica, TipKorpice = TipKorpice.Domacinska,
                Velicina = VelicinaKorpice.Mala,
            };
            var artikal = new Product
            {
                Name = "Krompir", Slug = "krompir", Price = 2m, Unit = Unit.Kg,
                Stock = 100, IsAvailable = true, Category = kat, Producer = proizvodjac,
                MozeUKorpicu = true,
            };

            db.DeliveryZones.Add(zona);
            db.DeliverySlots.Add(slot);
            db.Users.Add(kupac);
            db.Products.AddRange(korpica, artikal);
            await db.SaveChangesAsync();

            var (dan, vrijeme, cutoff) = await SedmicaHelper.UcitajRokAsync(db, new KorpiceOpcije(), default);
            var (godina, sedmica) = SedmicaHelper.SedmicaZaNarudzbu(dan, vrijeme, cutoff);
            s.Godina = godina;
            s.Sedmica = sedmica;

            var sadrzaj = new SedmicniSadrzaj
            {
                KorpicaId = korpica.Id, Godina = godina, Sedmica = sedmica,
                Status = sadrzajObjavljen ? StatusSadrzaja.Objavljen : StatusSadrzaja.Nacrt,
                CiljnaVrijednost = 28m,
            };
            sadrzaj.Stavke.Add(new StavkaSadrzaja
            {
                ProductId = artikal.Id, Kolicina = 2m, JedinicnaCijena = 2m,
            });
            db.SedmicniSadrzaji.Add(sadrzaj);

            var pretplata = new Pretplata
            {
                UserId = kupac.Id,
                ProductId = korpica.Id,
                Status = status,
                DeliveryZoneId = zona.Id,
                DeliverySlotId = slot.Id,
                DeliveryAddress = "Kralja Petra 12, Banja Luka",
                BrojIsporuka = brojIsporuka,
            };
            db.Pretplate.Add(pretplata);
            await db.SaveChangesAsync();

            s.Pretplata = pretplata;
            s.Korpica = korpica;
            return s;
        }

        public NapraviPretplatneNarudzbeHandler Handler(
            PretplateOpcije? opcije = null, bool mediatorPuca = false, Guid? pucaZa = null) =>
            new(Db,
                new FakeMediator(Db, opcije ?? Opcije(), mediatorPuca, pucaZa),
                Options.Create(new KorpiceOpcije()),
                Options.Create(opcije ?? Opcije()),
                NullLogger<NapraviPretplatneNarudzbeHandler>.Instance);

        public Task<int> ProlazAsync(PretplateOpcije? opcije = null, bool mediatorPuca = false) =>
            Handler(opcije, mediatorPuca).Handle(new NapraviPretplatneNarudzbeCommand(), default);

        public Task<int> BrojNarudzbiAsync() =>
            Db.Orders.AsNoTracking().CountAsync(o => o.PretplataId == Pretplata.Id);

        public void Dispose() => _t.Dispose();
    }

    /// <summary>
    /// Mediator koji zna samo za <see cref="CreateOrderCommand"/> — jedinu komandu koju
    /// posao šalje. `puca` simulira pad usred posla, poslije nastanka narudžbe.
    /// </summary>
    /// <summary>
    /// `pucaZa` je PRETPLATA na kojoj se puca, ne redni broj poziva: upit nad pretplatama nema
    /// zadani poredak, pa bi „padni na prvoj" davalo test koji prolazi ili pada u zavisnosti
    /// od toga kojim redom baza vrati redove.
    /// </summary>
    private sealed class FakeMediator(
        AppDbContext db, PretplateOpcije opcije, bool puca, Guid? pucaZa = null) : IMediator
    {
        public async Task<TResponse> Send<TResponse>(
            IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is CreateOrderCommand cmd)
            {
                var dto = await new CreateOrderHandler(
                        db, new FakeCurrentUser(null), new FakeEmail(), new FakeViber(),
                        Options.Create(new KorpiceOpcije()), Options.Create(opcije), Options.Create(new PoeniOpcije()))
                    .Handle(cmd, cancellationToken);
                if (puca || (pucaZa is not null && cmd.PretplataId == pucaZa))
                    throw new InvalidOperationException("Pad poslije nastanka narudžbe.");
                return (TResponse)(object)dto;
            }
            throw new NotImplementedException(
                $"FakeMediator ne zna da posluži {request.GetType().Name}.");
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest => throw new NotImplementedException();

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
            where TNotification : INotification => throw new NotImplementedException();
    }

    [Fact]
    public async Task Prolaz_pravi_narudzbu_za_ciljnu_sedmicu()
    {
        using var s = await Scena.SpremiAsync();

        Assert.Equal(1, await s.ProlazAsync());

        s.Db.ChangeTracker.Clear();
        var order = await s.Db.Orders.AsNoTracking().FirstAsync(o => o.PretplataId == s.Pretplata.Id);
        Assert.Equal(s.Godina, order.Godina);
        Assert.Equal(s.Sedmica, order.Sedmica);
        Assert.Equal(s.Pretplata.UserId, order.CustomerId);
        Assert.Equal(s.Pretplata.DeliveryAddress, order.DeliveryAddress);
    }

    /// <summary>Posao se vrti svakih 15 minuta. Ovo je razlog zašto `Poslednja*` postoji.</summary>
    [Fact]
    public async Task Dva_prolaza_daju_jednu_narudzbu()
    {
        using var s = await Scena.SpremiAsync();

        Assert.Equal(1, await s.ProlazAsync());
        Assert.Equal(0, await s.ProlazAsync());

        Assert.Equal(1, await s.BrojNarudzbiAsync());
    }

    /// <summary>
    /// Pad između nastanka narudžbe i upisa `Poslednja*`: transakcija mora vratiti oboje,
    /// pa naredni prolaz pravi tačno jednu narudžbu — ne drugu.
    /// </summary>
    [Fact]
    public async Task Pad_usred_posla_ne_ostavlja_duplu_narudzbu()
    {
        using var s = await Scena.SpremiAsync();

        Assert.Equal(0, await s.ProlazAsync(mediatorPuca: true));
        Assert.Equal(0, await s.BrojNarudzbiAsync());

        s.Db.ChangeTracker.Clear();
        Assert.Equal(1, await s.ProlazAsync());
        Assert.Equal(1, await s.BrojNarudzbiAsync());
    }

    [Fact]
    public async Task Sadrzaj_koji_nije_objavljen_preskace_sedmicu_a_pretplata_ostaje_aktivna()
    {
        using var s = await Scena.SpremiAsync(sadrzajObjavljen: false);

        Assert.Equal(0, await s.ProlazAsync());
        Assert.Equal(0, await s.BrojNarudzbiAsync());

        s.Db.ChangeTracker.Clear();
        var p = await s.Db.Pretplate.AsNoTracking().FirstAsync();
        Assert.Equal(StatusPretplate.Aktivna, p.Status);
        Assert.Null(p.PoslednjaSedmica);
    }

    [Fact]
    public async Task Otkazana_pretplata_ne_pravi_nista()
    {
        using var s = await Scena.SpremiAsync(StatusPretplate.Otkazana);

        Assert.Equal(0, await s.ProlazAsync());
        Assert.Equal(0, await s.BrojNarudzbiAsync());
    }

    [Fact]
    public async Task Pauza_preskace_sedmicu()
    {
        using var s = await Scena.SpremiAsync(StatusPretplate.Pauzirana);
        var (g, sed) = SedmicaHelper.Pomjeri(s.Godina, s.Sedmica, 2);
        s.Pretplata.PauzaDoGodina = g;
        s.Pretplata.PauzaDoSedmica = sed;
        await s.Db.SaveChangesAsync();

        Assert.Equal(0, await s.ProlazAsync());
        Assert.Equal(0, await s.BrojNarudzbiAsync());
    }

    /// <summary>
    /// Pauza koja se ne vrati sama je tihi otkaz — kupac misli da je pretplaćen, a ništa ne
    /// stiže. Zato se status gleda POSLIJE pauze, ne prije.
    /// </summary>
    [Fact]
    public async Task Istekla_pauza_sama_vraca_pretplatu_u_rad()
    {
        using var s = await Scena.SpremiAsync(StatusPretplate.Pauzirana);
        s.Pretplata.PauzaDoGodina = s.Godina;
        s.Pretplata.PauzaDoSedmica = s.Sedmica;
        await s.Db.SaveChangesAsync();

        Assert.Equal(1, await s.ProlazAsync());

        s.Db.ChangeTracker.Clear();
        var p = await s.Db.Pretplate.AsNoTracking().FirstAsync();
        Assert.Equal(StatusPretplate.Aktivna, p.Status);
        Assert.Null(p.PauzaDoGodina);
        Assert.Null(p.PauzaDoSedmica);
    }

    [Fact]
    public async Task Prerano_za_ovu_sedmicu_ne_pravi_nista()
    {
        using var s = await Scena.SpremiAsync();

        Assert.Equal(0, await s.ProlazAsync(Opcije(satiUnaprijed: 0)));
        Assert.Equal(0, await s.BrojNarudzbiAsync());
    }

    [Fact]
    public async Task Prvih_pet_isporuka_ide_po_punoj_cijeni()
    {
        using var s = await Scena.SpremiAsync(brojIsporuka: 4);

        await s.ProlazAsync();

        s.Db.ChangeTracker.Clear();
        var order = await s.Db.Orders.AsNoTracking().Include(o => o.Items)
            .FirstAsync(o => o.PretplataId == s.Pretplata.Id);
        Assert.Equal(25m, order.Items.Single().UnitPrice);
    }

    [Fact]
    public async Task Sesta_isporuka_nosi_pretplatnicku_cijenu()
    {
        using var s = await Scena.SpremiAsync(brojIsporuka: 5);

        await s.ProlazAsync();

        s.Db.ChangeTracker.Clear();
        var order = await s.Db.Orders.AsNoTracking().Include(o => o.Items)
            .FirstAsync(o => o.PretplataId == s.Pretplata.Id);
        Assert.Equal(22.50m, order.Items.Single().UnitPrice);
        // Ukupno mora pratiti umanjenu cijenu, ne punu — inače popust postoji samo na stavci.
        Assert.Equal(22.50m + 3m, order.TotalAmount);
    }

    /// <summary>
    /// Dvije pretplate u istom prolazu, prva puca. Druga mora proći — i, važnije, pucanje
    /// prve ne smije joj ostaviti upisanu sedmicu za koju narudžba ne postoji: ta sedmica bi
    /// joj bila trajno preskočena, a kupac ne bi imao šta da vidi ni kome da se požali.
    /// </summary>
    [Fact]
    public async Task Pad_jedne_pretplate_ne_kvari_drugu_u_istom_prolazu()
    {
        using var s = await Scena.SpremiAsync();
        var drugiKupac = new User
        {
            Name = "Dragan Hasić", Email = "dragan@test.local", Phone = "065 333 444",
            PasswordHash = "x", Role = UserRole.Customer,
        };
        s.Db.Users.Add(drugiKupac);
        await s.Db.SaveChangesAsync();

        // Druga pretplata na istu korpicu, ali drugog kupca — filtrirani indeks to dozvoljava.
        var druga = new Pretplata
        {
            UserId = drugiKupac.Id,
            ProductId = s.Pretplata.ProductId,
            Status = StatusPretplate.Aktivna,
            DeliveryZoneId = s.Pretplata.DeliveryZoneId,
            DeliverySlotId = s.Pretplata.DeliverySlotId,
            DeliveryAddress = "Gundulićeva 5, Banja Luka",
        };
        s.Db.Pretplate.Add(druga);
        await s.Db.SaveChangesAsync();

        // Puca tačno na prvoj pretplati; druga mora proći bez obzira na redoslijed obrade.
        var napravljeno = await s.Handler(Opcije(), pucaZa: s.Pretplata.Id)
            .Handle(new NapraviPretplatneNarudzbeCommand(), default);

        Assert.Equal(1, napravljeno);

        s.Db.ChangeTracker.Clear();
        var sve = await s.Db.Pretplate.AsNoTracking().ToListAsync();
        var pala = sve.Single(x => x.Id == s.Pretplata.Id);
        var prosla = sve.Single(x => x.Id == druga.Id);

        Assert.Null(pala.PoslednjaSedmica);
        Assert.Equal(s.Sedmica, prosla.PoslednjaSedmica);
        Assert.Equal(0, await s.Db.Orders.CountAsync(o => o.PretplataId == pala.Id));
        Assert.Equal(1, await s.Db.Orders.CountAsync(o => o.PretplataId == prosla.Id));
    }

    /// <summary>
    /// Narudžba postoji a `Poslednja*` ne zna za nju (upis je negdje pukao): drugi prolaz
    /// mora popraviti brojač, a ne napraviti drugu korpicu.
    /// </summary>
    [Fact]
    public async Task Postojeca_narudzba_popravlja_brojac_umjesto_da_pravi_novu()
    {
        using var s = await Scena.SpremiAsync();
        await s.ProlazAsync();

        s.Db.ChangeTracker.Clear();
        var p = await s.Db.Pretplate.FirstAsync();
        p.PoslednjaGodina = null;
        p.PoslednjaSedmica = null;
        await s.Db.SaveChangesAsync();

        Assert.Equal(0, await s.ProlazAsync());
        Assert.Equal(1, await s.BrojNarudzbiAsync());

        s.Db.ChangeTracker.Clear();
        var poslije = await s.Db.Pretplate.AsNoTracking().FirstAsync();
        Assert.Equal(s.Sedmica, poslije.PoslednjaSedmica);
    }
}
