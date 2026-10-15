using EPiljara.Application.Features.Korpice;
using EPiljara.Application.Features.Pretplate;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using EPiljara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Unit = EPiljara.Domain.Enums.Unit;

namespace EPiljara.Application.Tests;

public class MojePretplateTests
{
    private sealed class Scena : IDisposable
    {
        private readonly TestDb _t;
        public AppDbContext Db => _t.Db;
        public User Kupac { get; private set; } = null!;
        public User Drugi { get; private set; } = null!;
        public Product Korpica { get; private set; } = null!;
        public Product DrugaKorpica { get; private set; } = null!;
        public Product ObicanArtikal { get; private set; } = null!;
        public DeliveryZone Zona { get; private set; } = null!;
        public DeliverySlot Slot { get; private set; } = null!;

        private Scena(TestDb t) => _t = t;

        public static async Task<Scena> SpremiAsync()
        {
            var s = new Scena(new TestDb());
            var db = s.Db;

            var zona = new DeliveryZone { Name = "Centar", MinOrderAmount = 0m, DeliveryFee = 3m, IsActive = true };
            var slot = new DeliverySlot { DayOfWeek = 4, TimeFrom = "16:00", TimeTo = "20:00", CutoffHours = 48, Label = "Četvrtak 16–20" };
            var kat = new Category { Name = "Mješovito", Slug = "mjesovito" };
            var proiz = new Producer { Name = "Marko", Village = "Manjača" };
            var kupac = new User { Name = "Marko", Email = "marko@test.local", PasswordHash = "x", Role = UserRole.Customer };
            var drugi = new User { Name = "Dragan", Email = "dragan@test.local", PasswordHash = "x", Role = UserRole.Customer };

            Product Korp(string naziv, string slug, VelicinaKorpice v) => new()
            {
                Name = naziv, Slug = slug, Price = 25m, Unit = Unit.Kom, Stock = 50,
                IsAvailable = true, Category = kat, Producer = proiz,
                Vrsta = VrstaProizvoda.Korpica, TipKorpice = TipKorpice.Domacinska, Velicina = v,
            };

            var korpica = Korp("Mala domaćinska", "mala-domacinska", VelicinaKorpice.Mala);
            var druga = Korp("Srednja domaćinska", "srednja-domacinska", VelicinaKorpice.Srednja);
            var obican = new Product
            {
                Name = "Krompir", Slug = "krompir", Price = 2m, Unit = Unit.Kg, Stock = 100,
                IsAvailable = true, Category = kat, Producer = proiz,
            };

            db.AddRange(zona, slot, kupac, drugi, korpica, druga, obican);
            await db.SaveChangesAsync();

            s.Kupac = kupac; s.Drugi = drugi; s.Korpica = korpica;
            s.DrugaKorpica = druga; s.ObicanArtikal = obican; s.Zona = zona; s.Slot = slot;
            return s;
        }

        private static readonly IOptions<KorpiceOpcije> Ko = Options.Create(new KorpiceOpcije());
        private static readonly IOptions<PretplateOpcije> Po = Options.Create(new PretplateOpcije());

        public NapraviPretplatuHandler Novi(Guid? korisnik = null) =>
            new(Db, new FakeCurrentUser(korisnik ?? Kupac.Id), Ko, Po);

        public IzmijeniPretplatuHandler Izmjena(Guid? korisnik = null) =>
            new(Db, new FakeCurrentUser(korisnik ?? Kupac.Id), Ko, Po);

        public PauzirajPretplatuHandler Pauza(Guid? korisnik = null) =>
            new(Db, new FakeCurrentUser(korisnik ?? Kupac.Id), Ko, Po);

        public NastaviPretplatuHandler Nastavak(Guid? korisnik = null) =>
            new(Db, new FakeCurrentUser(korisnik ?? Kupac.Id), Ko, Po);

        public OtkaziPretplatuHandler Otkaz(Guid? korisnik = null) =>
            new(Db, new FakeCurrentUser(korisnik ?? Kupac.Id));

        public GetMojePretplateHandler Spisak(Guid? korisnik = null) =>
            new(Db, new FakeCurrentUser(korisnik ?? Kupac.Id), Ko, Po);

        public NapraviPretplatuCommand Komanda(Guid? productId = null) =>
            new(productId ?? Korpica.Id, Zona.Id, Slot.Id, "Kralja Petra 12", 44.77, 17.19, null);

        public void Dispose() => _t.Dispose();
    }

    [Fact]
    public async Task Nova_pretplata_pocinje_aktivna_bez_isporuka()
    {
        using var s = await Scena.SpremiAsync();

        var dto = await s.Novi().Handle(s.Komanda(), default);

        Assert.Equal("Aktivna", dto.Status);
        Assert.Equal(0, dto.BrojIsporuka);
        Assert.Equal(25m, dto.PunaCijena);
        Assert.Equal(25m, dto.TrenutnaCijena);
        Assert.False(dto.PopustAktivan);
        Assert.Equal(5, dto.DoPopusta);
        Assert.NotNull(dto.SljedecaIsporuka);
        Assert.Equal("Četvrtak 16–20", dto.DeliverySlotLabel);
    }

    [Fact]
    public async Task Druga_pretplata_na_istu_korpicu_se_odbija()
    {
        using var s = await Scena.SpremiAsync();
        await s.Novi().Handle(s.Komanda(), default);

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => s.Novi().Handle(s.Komanda(), default));
        Assert.Equal("pretplata_vec_postoji", ex.Code);
    }

    /// <summary>
    /// Otkazane se ne broje u jedinstvenosti — kupac koji se predomisli mora moći nazad na
    /// isti paket. Zato je indeks u bazi filtriran.
    /// </summary>
    [Fact]
    public async Task Poslije_otkaza_se_moze_pretplatiti_ponovo_na_istu_korpicu()
    {
        using var s = await Scena.SpremiAsync();
        var prva = await s.Novi().Handle(s.Komanda(), default);
        await s.Otkaz().Handle(new OtkaziPretplatuCommand(prva.Id), default);

        var druga = await s.Novi().Handle(s.Komanda(), default);

        Assert.NotEqual(prva.Id, druga.Id);
        Assert.Equal("Aktivna", druga.Status);
    }

    [Fact]
    public async Task Pretplata_na_obican_artikal_se_odbija()
    {
        using var s = await Scena.SpremiAsync();

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => s.Novi().Handle(s.Komanda(s.ObicanArtikal.Id), default));
        Assert.Equal("nije_korpica", ex.Code);
    }

    /// <summary>
    /// Pretplata koja ne bi prošla checkout mora pasti ODMAH. Inače bi posao svake sedmice
    /// tiho ćutao, a kupac bi mislio da je pretplaćen.
    /// </summary>
    [Fact]
    public async Task Zona_sa_minimumom_iznad_cijene_korpice_se_odbija_odmah()
    {
        using var s = await Scena.SpremiAsync();
        s.Zona.MinOrderAmount = 40m;
        await s.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => s.Novi().Handle(s.Komanda(), default));
        Assert.Equal("below_minimum", ex.Code);
    }

    [Fact]
    public async Task Tudja_pretplata_se_ne_moze_dirati()
    {
        using var s = await Scena.SpremiAsync();
        var moja = await s.Novi().Handle(s.Komanda(), default);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => s.Otkaz(s.Drugi.Id).Handle(new OtkaziPretplatuCommand(moja.Id), default));
    }

    [Fact]
    public async Task Pauza_upisuje_sedmicu_do_koje_se_preskace()
    {
        using var s = await Scena.SpremiAsync();
        var p = await s.Novi().Handle(s.Komanda(), default);

        var dto = await s.Pauza().Handle(new PauzirajPretplatuCommand(p.Id, 3), default);

        Assert.Equal("Pauzirana", dto.Status);

        var (dan, vrijeme, cutoff) = await SedmicaHelper.UcitajRokAsync(s.Db, new KorpiceOpcije(), default);
        var (g, sed) = SedmicaHelper.SedmicaZaNarudzbu(dan, vrijeme, cutoff);
        var (og, osed) = SedmicaHelper.Pomjeri(g, sed, 3);
        Assert.Equal(og, dto.PauzaDoGodina);
        Assert.Equal(osed, dto.PauzaDoSedmica);
        // Sljedeća isporuka je prva POSLIJE pauze, ne ona koju bi dobio da nije pauzirao.
        Assert.Equal(osed, dto.SljedecaSedmica);
    }

    [Fact]
    public async Task Pauza_izvan_opsega_se_odbija()
    {
        using var s = await Scena.SpremiAsync();
        var p = await s.Novi().Handle(s.Komanda(), default);

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => s.Pauza().Handle(new PauzirajPretplatuCommand(p.Id, 99), default));
        Assert.Equal("neispravna_pauza", ex.Code);
    }

    [Fact]
    public async Task Nastavak_gasi_pauzu()
    {
        using var s = await Scena.SpremiAsync();
        var p = await s.Novi().Handle(s.Komanda(), default);
        await s.Pauza().Handle(new PauzirajPretplatuCommand(p.Id, 2), default);

        var dto = await s.Nastavak().Handle(new NastaviPretplatuCommand(p.Id), default);

        Assert.Equal("Aktivna", dto.Status);
        Assert.Null(dto.PauzaDoGodina);
        Assert.Null(dto.PauzaDoSedmica);
    }

    /// <summary>
    /// Promjena paketa ne nulira zarađene isporuke: popust visi o vjernosti pretplati, ne o
    /// konkretnoj korpici.
    /// </summary>
    [Fact]
    public async Task Promjena_paketa_cuva_zaradjeni_popust()
    {
        using var s = await Scena.SpremiAsync();
        var p = await s.Novi().Handle(s.Komanda(), default);

        // Poslije Clear() su `s.DrugaKorpica` i ostali objekti iz pripreme ODVOJENI od
        // konteksta, pa izmjena na njima ne bi otišla u bazu — oba reda se zato dohvataju
        // iznova.
        s.Db.ChangeTracker.Clear();
        var red = await s.Db.Pretplate.FirstAsync(x => x.Id == p.Id);
        red.BrojIsporuka = 6;
        var druga = await s.Db.Products.FirstAsync(x => x.Id == s.DrugaKorpica.Id);
        druga.Price = 40m;
        await s.Db.SaveChangesAsync();

        var dto = await s.Izmjena().Handle(new IzmijeniPretplatuCommand(
            p.Id, s.DrugaKorpica.Id, s.Zona.Id, s.Slot.Id, "Nova 5", null, null, "Zvoni dvaput"),
            default);

        Assert.Equal(s.DrugaKorpica.Id, dto.ProductId);
        Assert.Equal(6, dto.BrojIsporuka);
        Assert.Equal(40m, dto.PunaCijena);
        Assert.Equal(36m, dto.TrenutnaCijena);
        Assert.True(dto.PopustAktivan);
        Assert.Equal("Zvoni dvaput", dto.Napomena);
    }

    [Fact]
    public async Task Otkazana_pretplata_se_ne_moze_mijenjati_ni_nastaviti()
    {
        using var s = await Scena.SpremiAsync();
        var p = await s.Novi().Handle(s.Komanda(), default);
        await s.Otkaz().Handle(new OtkaziPretplatuCommand(p.Id), default);

        var ex1 = await Assert.ThrowsAsync<ConflictException>(
            () => s.Izmjena().Handle(new IzmijeniPretplatuCommand(
                p.Id, s.Korpica.Id, s.Zona.Id, s.Slot.Id, "Nova 5", null, null, null), default));
        Assert.Equal("pretplata_otkazana", ex1.Code);

        var ex2 = await Assert.ThrowsAsync<ConflictException>(
            () => s.Nastavak().Handle(new NastaviPretplatuCommand(p.Id), default));
        Assert.Equal("pretplata_otkazana", ex2.Code);
    }

    [Fact]
    public async Task Spisak_vraca_samo_moje_i_aktivne_prve()
    {
        using var s = await Scena.SpremiAsync();
        var prva = await s.Novi().Handle(s.Komanda(), default);
        await s.Novi().Handle(s.Komanda(s.DrugaKorpica.Id), default);
        await s.Otkaz().Handle(new OtkaziPretplatuCommand(prva.Id), default);
        await s.Novi(s.Drugi.Id).Handle(s.Komanda(), default);

        var spisak = await s.Spisak().Handle(new GetMojePretplateQuery(), default);

        Assert.Equal(2, spisak.Count);
        Assert.Equal("Aktivna", spisak[0].Status);
        Assert.Equal("Otkazana", spisak[1].Status);
        // Otkazana nema sljedeću isporuku — ekran bi inače obećavao korpicu koja ne dolazi.
        Assert.Null(spisak[1].SljedecaIsporuka);
    }
}
