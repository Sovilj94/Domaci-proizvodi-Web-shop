using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.Features.Admin;
using EPiljara.Application.Features.Auth;
using EPiljara.Application.Features.Mejlovi;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using EPiljara.Infrastructure.Services;
using EPiljara.Infrastructure.Services.Mejlovi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Tests;

/// <summary>Red mejlova, obavještenja, zaboravljena lozinka, ručno slanje i odjava (spec 2026-11-04-mejlovi-design.md).</summary>
public class MejloviTests
{
    private sealed class Transport(Func<EmailZaSlanje, EmailRezultat> odgovor) : IEmailTransport
    {
        public List<EmailZaSlanje> Poslato { get; } = [];
        public string Naziv => "test";

        public Task<EmailRezultat> PosaljiAsync(EmailZaSlanje p, CancellationToken ct)
        {
            Poslato.Add(p);
            return Task.FromResult(odgovor(p));
        }
    }

    private sealed class Jwt : IJwtTokenService
    {
        public string CreateToken(User user) => "token";
    }

    private static readonly DateTime Sada = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    private static EmailPoruka Mejl(string za = "kupac@example.com") =>
        new() { Primalac = za, Naslov = "Naslov", Html = "<p>x</p>", Vrsta = "probni", Kreirana = Sada.AddMinutes(-5) };

    /* ---------- red ---------- */

    [Fact]
    public async Task Uspjesno_poslat_mejl_je_poslat_i_ne_salje_se_ponovo()
    {
        using var t = new TestDb();
        t.Db.EmailPoruke.Add(Mejl());
        await t.Db.SaveChangesAsync();
        var tr = new Transport(_ => new EmailRezultat(true, "msg-1"));

        Assert.Equal(1, await new PosaljiEmailoveHandler(t.Db, tr).Handle(new PosaljiEmailoveCommand(Sada), default));
        Assert.Equal(0, await new PosaljiEmailoveHandler(t.Db, tr).Handle(new PosaljiEmailoveCommand(Sada), default));

        var m = await t.Db.EmailPoruke.SingleAsync();
        Assert.Equal(StatusEmaila.Poslato, m.Status);
        Assert.Equal("msg-1", m.DobavljacId);
        Assert.Single(tr.Poslato);
    }

    [Fact]
    public async Task Prolazna_greska_zakazuje_ponovni_pokusaj_a_trajna_zaustavlja()
    {
        using var t = new TestDb();
        var prolazna = Mejl("a@example.com");
        var trajna = Mejl("losa-adresa");
        t.Db.EmailPoruke.AddRange(prolazna, trajna);
        await t.Db.SaveChangesAsync();
        var tr = new Transport(p => p.Primalac.Contains('@')
            ? new EmailRezultat(false, Greska: "503 zauzet")
            : new EmailRezultat(false, Greska: "neispravna adresa", Trajna: true));

        await new PosaljiEmailoveHandler(t.Db, tr).Handle(new PosaljiEmailoveCommand(Sada), default);
        // Prije zakazanog vremena se ne pokušava.
        await new PosaljiEmailoveHandler(t.Db, tr).Handle(new PosaljiEmailoveCommand(Sada.AddSeconds(30)), default);

        t.Db.ChangeTracker.Clear();
        var p = await t.Db.EmailPoruke.SingleAsync(x => x.Id == prolazna.Id);
        Assert.Equal(StatusEmaila.NaCekanju, p.Status);
        Assert.Equal(1, p.Pokusaja);
        Assert.Equal(Sada.AddMinutes(1), p.SljedeciPokusaj);
        Assert.Equal("503 zauzet", p.ZadnjaGreska);
        var tj = await t.Db.EmailPoruke.SingleAsync(x => x.Id == trajna.Id);
        Assert.Equal(StatusEmaila.Greska, tj.Status);
        Assert.Equal(2, tr.Poslato.Count);
    }

    [Fact]
    public async Task Poslije_maksimalnog_broja_pokusaja_mejl_prelazi_u_gresku()
    {
        using var t = new TestDb();
        var m = Mejl();
        m.Pokusaja = EmailPravila.MaksPokusaja - 1;
        t.Db.EmailPoruke.Add(m);
        await t.Db.SaveChangesAsync();

        await new PosaljiEmailoveHandler(t.Db, new Transport(_ => new EmailRezultat(false, Greska: "timeout")))
            .Handle(new PosaljiEmailoveCommand(Sada), default);

        Assert.Equal(StatusEmaila.Greska, (await t.Db.EmailPoruke.SingleAsync()).Status);
    }

    [Fact]
    public async Task Ponovi_vraca_mejl_sa_greskom_u_red()
    {
        using var t = new TestDb();
        var m = Mejl();
        m.Status = StatusEmaila.Greska;
        m.Pokusaja = 10;
        t.Db.EmailPoruke.Add(m);
        await t.Db.SaveChangesAsync();
        var signal = new EmailSignal();

        await new PonoviEmailHandler(t.Db, signal).Handle(new PonoviEmailCommand(m.Id), default);

        var x = await t.Db.EmailPoruke.SingleAsync();
        Assert.Equal(StatusEmaila.NaCekanju, x.Status);
        Assert.Equal(0, x.Pokusaja);
    }

    /* ---------- RedEmailService: upis u red ---------- */

    private static RedEmailService Servis(TestDb t, IOdjavaPotpis? potpis = null)
    {
        var sc = new ServiceCollection();
        sc.AddSingleton<IAppDbContext>(t.Db);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["App:BaseUrl"] = "https://epiljara.test", ["Jwt:SigningKey"] = "tajna-za-test-0123456789",
        }).Build();
        return new RedEmailService(sc.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), config,
            new EmailSignal(), potpis ?? new OdjavaPotpis(config), NullLogger<RedEmailService>.Instance);
    }

    [Fact]
    public async Task Status_narudzbe_ide_u_red_jednom_po_dogadjaju()
    {
        using var t = new TestDb();
        var order = new Order
        {
            Code = "EP-2611-0001", PublicToken = "tok", CustomerName = "Jelena Marić", CustomerEmail = "jelena@example.com",
            TotalAmount = 39m, Status = OrderStatus.Delivered,
        };
        var s = Servis(t);

        await s.SendStatusNarudzbeAsync(order, OrderStatus.Delivered);
        await s.SendStatusNarudzbeAsync(order, OrderStatus.Delivered);

        var m = await t.Db.EmailPoruke.SingleAsync();
        Assert.Equal("jelena@example.com", m.Primalac);
        Assert.Equal("status_delivered", m.Vrsta);
        Assert.Contains("isporučena", m.Naslov);
        Assert.Contains("https://epiljara.test/narudzba/tok", m.Html);
    }

    [Fact]
    public async Task Kampanja_daje_link_za_odjavu_samo_informativnim_primaocima()
    {
        using var t = new TestDb();
        var s = Servis(t);
        var k = new EmailKampanja { Naslov = "Kasnimo sa dostavom", Tekst = "Zdravo,\n\nkasnimo sat vremena.", Grupa = "kupci", GrupaOpis = "x" };

        await s.SendKampanjaAsync(k, [new PrimalacKampanje("a@example.com", "Ana", true), new PrimalacKampanje("b@example.com", null, false)]);

        var poruke = await t.Db.EmailPoruke.OrderBy(m => m.Primalac).ToListAsync();
        Assert.Contains("/odjava?e=a%40example.com", poruke[0].Html);
        Assert.DoesNotContain("/odjava", poruke[1].Html);
        Assert.Contains("kasnimo sat vremena.", poruke[0].Html);
        Assert.All(poruke, m => Assert.Equal(k.Id, m.KampanjaId));
    }

    /* ---------- obavještenja na tokovima ---------- */

    private static async Task<(TestDb T, Order Order)> NarudzbaAsync(OrderStatus status)
    {
        var t = new TestDb();
        var slot = new DeliverySlot { DayOfWeek = 4, TimeFrom = "16:00", TimeTo = "20:00", CutoffHours = 48 };
        var zona = new DeliveryZone { Name = "Centar", DeliveryFee = 3m };
        var order = new Order
        {
            Code = "EP-2611-0002", PublicToken = Guid.NewGuid().ToString("N"), CustomerName = "Marko Petrović",
            CustomerEmail = "marko@example.com", Status = status, DeliveryZoneId = zona.Id, DeliverySlotId = slot.Id,
            Godina = 2026, Sedmica = 46, TotalAmount = 25m,
        };
        t.Db.AddRange(slot, zona, order);
        t.Db.Users.Add(new User { Name = "Admin", Email = "admin@example.com", PasswordHash = "x", Role = UserRole.Admin });
        await t.Db.SaveChangesAsync();
        t.Db.ChangeTracker.Clear();
        return (t, order);
    }

    [Fact]
    public async Task Admin_potvrda_narudzbe_obavjestava_kupca()
    {
        var (t, order) = await NarudzbaAsync(OrderStatus.Pending);
        using var _ = t;
        var email = new FakeEmail();

        await new UpdateOrderStatusHandler(t.Db, Options.Create(new Features.Poeni.PoeniOpcije()), email)
            .Handle(new UpdateOrderStatusCommand(order.Id, "Confirmed"), default);

        var (o, status, automatski) = Assert.Single(email.Statusi);
        Assert.Equal(order.Id, o.Id);
        Assert.Equal(OrderStatus.Confirmed, status);
        Assert.False(automatski);
    }

    [Fact]
    public async Task Iskljuceno_obavjestenje_se_ne_salje()
    {
        var (t, order) = await NarudzbaAsync(OrderStatus.Pending);
        using var _ = t;
        t.Db.Podesavanja.Add(new Podesavanja { Id = Podesavanja.JediniId, ObavKupacPotvrdjena = false, VrijemeIsporukeRezerva = "16:00" });
        await t.Db.SaveChangesAsync();
        var email = new FakeEmail();

        await new UpdateOrderStatusHandler(t.Db, Options.Create(new Features.Poeni.PoeniOpcije()), email)
            .Handle(new UpdateOrderStatusCommand(order.Id, "Confirmed"), default);

        Assert.Empty(email.Statusi);
    }

    [Fact]
    public async Task Radnici_su_lista_iz_podesavanja_a_bez_nje_admini()
    {
        var (t, _) = await NarudzbaAsync(OrderStatus.Pending);
        using var _t = t;

        Assert.Equal(["admin@example.com"], await ObavjestenjaPravila.RadniciAsync(t.Db, null, default));
        Assert.Equal(["mladen@example.com", "pakovanje@example.com"], await ObavjestenjaPravila.RadniciAsync(t.Db,
            new Podesavanja { ObavRadniciPrimaoci = " Mladen@example.com; pakovanje@example.com,mladen@example.com" }, default));
    }

    [Fact]
    public async Task Nova_narudzba_javlja_radnicima()
    {
        var (t, order) = await NarudzbaAsync(OrderStatus.Pending);
        using var _ = t;
        var email = new FakeEmail();

        await ObavjestenjaPravila.NovaNarudzbaAsync(t.Db, email, order, default);

        var o = Assert.Single(email.Obavjestenja);
        Assert.Equal(["admin@example.com"], o.Primaoci);
        Assert.Equal("Nova narudžba EP-2611-0002", o.Naslov);
        Assert.Contains(o.Redovi, r => r.Contains("četvrtak 16:00–20:00"));
    }

    /* ---------- zaboravljena lozinka ---------- */

    private static async Task<(TestDb T, User U)> KorisnikAsync()
    {
        var t = new TestDb();
        var u = new User { Name = "Ivana Lukić", Email = "ivana@example.com", PasswordHash = new PasswordHasher().Hash("stara1"), Role = UserRole.Customer };
        t.Db.Users.Add(u);
        await t.Db.SaveChangesAsync();
        return (t, u);
    }

    [Fact]
    public async Task Link_za_novu_lozinku_postavlja_lozinku_jednom()
    {
        var (t, u) = await KorisnikAsync();
        using var _ = t;
        var email = new FakeEmail();
        var hasher = new PasswordHasher();

        await new ZaboravljenaLozinkaHandler(t.Db, email).Handle(new ZaboravljenaLozinkaCommand(" IVANA@example.com "), default);
        var token = Assert.Single(email.ResetLozinke).Token;
        Assert.NotEqual(token, u.ResetLozinkeHash); // u bazi je samo heš

        await new NovaLozinkaHandler(t.Db, hasher, new Jwt()).Handle(new NovaLozinkaCommand(token, "nova123"), default);

        Assert.True(hasher.Verify("nova123", u.PasswordHash));
        Assert.True(u.EmailConfirmed);
        var ponovo = await Assert.ThrowsAsync<DomainException>(() =>
            new NovaLozinkaHandler(t.Db, hasher, new Jwt()).Handle(new NovaLozinkaCommand(token, "treca123"), default));
        Assert.Equal("reset_istekao", ponovo.Code);
    }

    [Fact]
    public async Task Istekao_link_ne_radi_a_nepoznata_adresa_ne_dobija_mejl()
    {
        var (t, u) = await KorisnikAsync();
        using var _ = t;
        var email = new FakeEmail();
        await new ZaboravljenaLozinkaHandler(t.Db, email).Handle(new ZaboravljenaLozinkaCommand("niko@example.com"), default);
        Assert.Empty(email.ResetLozinke);

        await new ZaboravljenaLozinkaHandler(t.Db, email).Handle(new ZaboravljenaLozinkaCommand(u.Email), default);
        // Drugi zahtjev odmah poslije prvog ne šalje novi mejl (zaštita sandučeta).
        await new ZaboravljenaLozinkaHandler(t.Db, email).Handle(new ZaboravljenaLozinkaCommand(u.Email), default);
        Assert.Single(email.ResetLozinke);

        u.ResetLozinkeIstice = DateTime.UtcNow.AddMinutes(-1);
        await t.Db.SaveChangesAsync();
        var e = await Assert.ThrowsAsync<DomainException>(() => new NovaLozinkaHandler(t.Db, new PasswordHasher(), new Jwt())
            .Handle(new NovaLozinkaCommand(email.ResetLozinke[0].Token, "nova123"), default));
        Assert.Equal("reset_istekao", e.Code);
    }

    /* ---------- grupe i odjava ---------- */

    [Fact]
    public async Task Grupa_kupci_preskace_odjavljene_i_nepotvrdjene_a_odjava_radi_samo_sa_potpisom()
    {
        using var t = new TestDb();
        t.Db.Users.AddRange(
            new User { Name = "Ana", Email = "ana@example.com", PasswordHash = "x", EmailConfirmed = true },
            new User { Name = "Bojan", Email = "bojan@example.com", PasswordHash = "x", EmailConfirmed = true, OdjavljenOdObavjestenja = true },
            new User { Name = "Ceca", Email = "ceca@example.com", PasswordHash = "x", EmailConfirmed = false });
        t.Db.PrijaveNovosti.Add(new PrijavaNovosti { Email = "ana@example.com" });
        await t.Db.SaveChangesAsync();

        var (kupci, _) = await KampanjaGrupe.UcitajAsync(t.Db, "kupci", null, null, default);
        var p = Assert.Single(kupci);
        Assert.Equal("ana@example.com", p.Email);
        Assert.True(p.Odjava);

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:SigningKey"] = "k" }).Build();
        var potpis = new OdjavaPotpis(config);
        await Assert.ThrowsAsync<DomainException>(() =>
            new OdjavaSaObavjestenjaHandler(t.Db, potpis).Handle(new OdjavaSaObavjestenjaCommand("ana@example.com", "lazni"), default));
        await new OdjavaSaObavjestenjaHandler(t.Db, potpis)
            .Handle(new OdjavaSaObavjestenjaCommand("Ana@example.com", potpis.Potpisi("ana@example.com")), default);

        Assert.Empty((await KampanjaGrupe.UcitajAsync(t.Db, "kupci", null, null, default)).Primaoci);
        Assert.Empty(await t.Db.PrijaveNovosti.ToListAsync());
    }

    /* ---------- plan vozača ---------- */

    [Fact]
    public async Task Plan_za_sutra_ide_od_podesenog_sata_jednom_po_vozacu()
    {
        using var t = new TestDb();
        t.Db.Database.ExecuteSqlRaw(
            $"INSERT INTO \"Magacin\" (\"Id\", \"Naziv\", \"Latitude\", \"Longitude\") VALUES ('{Magacin.JediniId}', 'Magacin', 44.57, 17.00)");
        var slot = new DeliverySlot { DayOfWeek = 4, TimeFrom = "16:00", TimeTo = "20:00", CutoffHours = 48 };
        var zona = new DeliveryZone { Name = "Borik", DeliveryFee = 3m };
        var korisnik = new User { Name = "Saša", Email = "sasa@example.com", PasswordHash = "x", Role = UserRole.Driver };
        var vozac = new Driver { Name = "Saša (kombi 2)", Phone = "065", UserId = korisnik.Id };
        var sutra = new DateOnly(2026, 11, 5);
        var ruta = new DeliveryRoute { Date = sutra, DriverId = vozac.Id, Naziv = "Borik", DeliveryZoneId = zona.Id };
        var order = new Order
        {
            Code = "EP-1", PublicToken = "p", CustomerName = "K", Status = OrderStatus.Confirmed, DeliveryZoneId = zona.Id,
            DeliverySlotId = slot.Id, Godina = 2026, Sedmica = 45, Latitude = 44.78, Longitude = 17.21,
        };
        t.Db.AddRange(slot, zona, korisnik, vozac, ruta, order);
        t.Db.RouteStops.Add(new RouteStop { RouteId = ruta.Id, OrderId = order.Id, Sequence = 1 });
        await t.Db.SaveChangesAsync();
        var email = new FakeEmail();
        var opt = new FiksniOptimizer(80_000);
        // 4.11. u 15:00 UTC = 16:00 lokalno (prije 17 h), pa u 17:00 UTC = 18:00 lokalno.
        var prije = new DateTime(2026, 11, 4, 15, 0, 0, DateTimeKind.Utc);
        var poslije = new DateTime(2026, 11, 4, 17, 0, 0, DateTimeKind.Utc);

        Assert.Equal(0, await new PosaljiPlanoveVozacaHandler(t.Db, opt, email).Handle(new PosaljiPlanoveVozacaCommand(prije), default));
        Assert.Equal(1, await new PosaljiPlanoveVozacaHandler(t.Db, opt, email).Handle(new PosaljiPlanoveVozacaCommand(poslije), default));

        var o = Assert.Single(email.Obavjestenja);
        Assert.Equal(["sasa@example.com"], o.Primaoci);
        Assert.Equal("Plan za sutra — četvrtak 5.11.", o.Naslov);
        Assert.Contains(o.Redovi, r => r.StartsWith("1 stanica: Borik (1)"));
        Assert.Contains(o.Redovi, r => r.Contains("80 km sa povratkom u magacin"));
        Assert.Equal($"plan:{vozac.Id:N}:20261105", o.Kljuc);
    }

    private sealed class FiksniOptimizer(double metara) : IRouteOptimizer
    {
        public Task<RouteOptimizationResult> OptimizeAsync(double startLat, double startLon,
            IReadOnlyList<RouteStopInput> stops, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RouteOptimizationResult(stops.Select(s => s.OrderId).ToList(), metara, 3600, null));
    }
}
