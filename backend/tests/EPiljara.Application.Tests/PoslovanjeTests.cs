using EPiljara.Application.DTOs;
using EPiljara.Application.Features.Admin.Poslovanje;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Unit = EPiljara.Domain.Enums.Unit;

namespace EPiljara.Application.Tests;

/// <summary>
/// Podaci za testove analitike, upisani direktno (bez checkout-a), sa fiksnim sedmicama 2026:
/// isporuka četvrtkom, oktobar = sedmice 40–44, septembar = 36–39.
/// Krompir: 2 KM/kg, nabavna 1 (Marko). Jaja: 0,50 KM/kom, nabavna 0,30 (Ana, kategorija Jaja).
/// Korpica povrća (mala): 10 KM, sadržaj 2 kg krompira.
/// </summary>
internal sealed class PoslovanjePodaciTest : IDisposable
{
    public TestDb T { get; } = new();
    public DeliveryZone Centar { get; private set; } = null!;
    public DeliveryZone Laus { get; private set; } = null!;
    public DeliverySlot Slot { get; private set; } = null!;
    public Product Krompir { get; private set; } = null!;
    public Product Jaja { get; private set; } = null!;
    public Product Korpica { get; private set; } = null!;
    private int _brojac;

    public static async Task<PoslovanjePodaciTest> NapraviAsync()
    {
        var p = new PoslovanjePodaciTest();
        var db = p.T.Db;
        p.Centar = new DeliveryZone { Name = "Centar", MinOrderAmount = 0m, DeliveryFee = 3m, IsActive = true, Granica = TestNaselje.Granica };
        p.Laus = new DeliveryZone { Name = "Lauš", MinOrderAmount = 0m, DeliveryFee = 3m, IsActive = true };
        p.Slot = new DeliverySlot { DayOfWeek = (int)DayOfWeek.Thursday, TimeFrom = "16:00", TimeTo = "20:00", CutoffHours = 48 };
        var povrce = new Category { Name = "Povrće", Slug = "povrce" };
        var jaja = new Category { Name = "Jaja", Slug = "jaja" };
        var marko = new Producer { Name = "Marko", Village = "Manjača" };
        var ana = new Producer { Name = "Ana", Village = "Kola" };
        p.Krompir = new Product
        {
            Name = "Krompir", Slug = "krompir", Price = 2m, NabavnaCijena = 1m, Unit = Unit.Kg, Stock = 100,
            Category = povrce, Producer = marko,
        };
        p.Jaja = new Product
        {
            Name = "Jaja", Slug = "jaja", Price = 0.5m, NabavnaCijena = 0.3m, Unit = Unit.Kom, Stock = 100,
            Category = jaja, Producer = ana,
        };
        p.Korpica = new Product
        {
            Name = "Mala korpica povrća", Slug = "mala-korpica-povrca", Price = 10m, Unit = Unit.Kom,
            Vrsta = VrstaProizvoda.Korpica, TipKorpice = TipKorpice.Povrce, Velicina = VelicinaKorpice.Mala,
            Category = povrce, Producer = marko,
        };
        db.AddRange(p.Centar, p.Laus, p.Slot, povrce, jaja, marko, ana, p.Krompir, p.Jaja, p.Korpica);
        await db.SaveChangesAsync();
        return p;
    }

    /// <summary>
    /// Upisuje narudžbu za sedmicu. <paramref name="bezSnimka"/> briše snimak nabavne poslije
    /// upisa — kao narudžba starija od snimanja.
    /// </summary>
    public async Task<Order> NarudzbaAsync(
        int sedmica, OrderStatus status, decimal krompirKg = 0, int jaja = 0, int korpica = 0,
        Guid? kupacId = null, string kupac = "Marko Marković", string? email = null, string? telefon = null,
        decimal dostava = 0, decimal popust = 0, decimal poeniPopust = 0, DeliveryZone? zona = null,
        Guid? pretplataId = null, bool bezSnimka = false, int godina = 2026)
    {
        var n = ++_brojac;
        var o = new Order
        {
            Code = $"EP-{n:0000}", PublicToken = Guid.NewGuid().ToString("N"),
            CustomerId = kupacId, CustomerName = kupac, CustomerEmail = email, CustomerPhone = telefon,
            Status = status, DeliveryAddress = "Testna 1", DeliverySlotId = Slot.Id,
            DeliveryZoneId = (zona ?? Centar).Id, Godina = godina, Sedmica = sedmica,
            DeliveryFee = dostava, Popust = popust, PoeniPopust = poeniPopust, PretplataId = pretplataId,
        };
        if (krompirKg > 0)
            o.Items.Add(new OrderItem { ProductId = Krompir.Id, ProductName = "Krompir", Unit = Unit.Kg, Quantity = krompirKg, UnitPrice = 2m });
        if (jaja > 0)
            o.Items.Add(new OrderItem { ProductId = Jaja.Id, ProductName = "Jaja", Unit = Unit.Kom, Quantity = jaja, UnitPrice = 0.5m });
        if (korpica > 0)
        {
            var k = new OrderItem { ProductId = Korpica.Id, ProductName = Korpica.Name, Unit = Unit.Kom, Quantity = korpica, UnitPrice = 10m };
            k.KorpicaStavke.Add(new StavkaKorpiceNarudzbe
            {
                ProductId = Krompir.Id, ProductName = "Krompir", Unit = Unit.Kg, Kolicina = 2m, JedinicnaCijena = 2m,
            });
            o.Items.Add(k);
        }
        o.Subtotal = o.Items.Sum(i => i.Quantity * i.UnitPrice);
        o.TotalAmount = o.Subtotal + dostava - popust - poeniPopust;
        T.Db.Orders.Add(o);
        await T.Db.SaveChangesAsync();

        if (bezSnimka)
        {
            foreach (var i in o.Items)
            {
                i.NabavnaCijena = null;
                foreach (var s in i.KorpicaStavke) s.NabavnaCijena = null;
            }
            await T.Db.SaveChangesAsync();
        }
        T.Db.ChangeTracker.Clear();
        return o;
    }

    public async Task<User> KupacAsync(string ime, Guid? preporucio = null)
    {
        var u = new User { Name = ime, Email = $"{ime.ToLowerInvariant()}@test.ba", PasswordHash = "x", PreporucioId = preporucio };
        T.Db.Users.Add(u);
        await T.Db.SaveChangesAsync();
        return u;
    }

    public Task<PoslovanjeMjesecaDto> MjesecAsync(int mjesec, int godina = 2026) =>
        new GetPoslovanjeHandler(T.Db, Options.Create(TestOpcije.Korpice()))
            .Handle(new GetPoslovanjeQuery(godina, mjesec), default);

    public void Dispose() => T.Dispose();
}

public class PoslovanjePrihodTests
{
    /// <summary>
    /// Oktobar: A (sedm. 41, isporučena) = 3 kg krompira + 1 korpica + 3 KM dostave;
    /// B (sedm. 40 — isporuka 1.10) = 10 jaja. Otkazana i neuspjela ne ulaze u prihod.
    /// Promet 16 + 5 = 21; nabavno 3 + 2 (korpica: 2 kg × 1) + 3 = 8; zarada 13; neto 24.
    /// </summary>
    [Fact]
    public async Task Prihod_broji_samo_isporucene_u_mjesecu_isporuke()
    {
        using var p = await PoslovanjePodaciTest.NapraviAsync();
        await p.NarudzbaAsync(41, OrderStatus.Delivered, krompirKg: 3, korpica: 1, dostava: 3);
        await p.NarudzbaAsync(40, OrderStatus.Delivered, jaja: 10);
        await p.NarudzbaAsync(42, OrderStatus.Cancelled, krompirKg: 5);
        await p.NarudzbaAsync(43, OrderStatus.FailedDelivery, krompirKg: 1);
        await p.NarudzbaAsync(39, OrderStatus.Delivered, krompirKg: 1); // 24.9 — septembar

        var d = await p.MjesecAsync(10);

        Assert.Equal(21m, d.Prihod.PrometRobe);
        Assert.Equal(3m, d.Prihod.Dostava);
        Assert.Equal(24m, d.Prihod.Neto);
        Assert.Equal(8m, d.Prihod.Nabavno);
        Assert.Equal(13m, d.Prihod.Zarada);
        Assert.Equal(0.619m, d.Prihod.Marza);
        Assert.Equal(2, d.Prihod.BrojNarudzbi);
        Assert.Equal(12m, d.Prihod.ProsjecnaNarudzba);
        Assert.Equal(0.4762m, d.Prihod.UdioKorpica); // 10 od 21
        Assert.False(d.Prihod.Procjena);
        Assert.Equal(2m, d.Prethodni.PrometRobe);   // septembar: samo sedmica 39
    }

    [Fact]
    public async Task Popusti_umanjuju_neto_ali_ne_i_marzu()
    {
        using var p = await PoslovanjePodaciTest.NapraviAsync();
        await p.NarudzbaAsync(41, OrderStatus.Delivered, krompirKg: 10, popust: 2, poeniPopust: 1);

        var d = await p.MjesecAsync(10);

        Assert.Equal(2m, d.Prihod.PromoPopust);
        Assert.Equal(1m, d.Prihod.PoeniPopust);
        Assert.Equal(17m, d.Prihod.Neto);
        Assert.Equal(0.5m, d.Prihod.Marza); // (20 − 10) / 20
    }

    [Fact]
    public async Task Bez_snimka_racuna_po_danasnjoj_nabavnoj_i_oznacava_procjenu()
    {
        using var p = await PoslovanjePodaciTest.NapraviAsync();
        await p.NarudzbaAsync(41, OrderStatus.Delivered, krompirKg: 4, bezSnimka: true);

        var d = await p.MjesecAsync(10);

        Assert.True(d.Prihod.Procjena);
        Assert.Equal(4m, d.Prihod.Nabavno);
    }

    [Fact]
    public async Task Artikal_bez_ikakve_nabavne_se_broji_posebno()
    {
        using var p = await PoslovanjePodaciTest.NapraviAsync();
        var k = await p.T.Db.Products.FirstAsync(x => x.Id == p.Krompir.Id);
        k.NabavnaCijena = null;
        await p.T.Db.SaveChangesAsync();
        p.T.Db.ChangeTracker.Clear();
        await p.NarudzbaAsync(41, OrderStatus.Delivered, krompirKg: 4, jaja: 10);

        var d = await p.MjesecAsync(10);

        Assert.Equal(1, d.Prihod.BezNabavneCijene);
        Assert.Equal(3m, d.Prihod.Nabavno); // samo jaja
    }

    /// <summary>
    /// Korpica bez redova sadržaja (npr. narudžba od prije P2b) nema trošak koji bi se mogao
    /// izračunati — ne smije tiho dati 100% marže, nego se broji kao stavka bez nabavne cijene.
    /// </summary>
    [Fact]
    public async Task Korpica_bez_sadrzaja_se_broji_kao_stavka_bez_nabavne()
    {
        using var p = await PoslovanjePodaciTest.NapraviAsync();
        var o = await p.NarudzbaAsync(41, OrderStatus.Delivered, korpica: 1);
        p.T.Db.StavkeKorpiceNarudzbe.RemoveRange(p.T.Db.StavkeKorpiceNarudzbe);
        await p.T.Db.SaveChangesAsync();
        p.T.Db.ChangeTracker.Clear();

        var d = await p.MjesecAsync(10);

        Assert.Equal(1, d.Prihod.BezNabavneCijene);
        Assert.Null(Assert.Single(d.Prodaja.Korpice).Marza);
    }

    [Fact]
    public async Task Prazan_mjesec_daje_nule_bez_greske()
    {
        using var p = await PoslovanjePodaciTest.NapraviAsync();

        var d = await p.MjesecAsync(8);

        Assert.Equal(0m, d.Prihod.Neto);
        Assert.Null(d.Prihod.Marza);
        Assert.Equal(0m, d.Prihod.ProsjecnaNarudzba);
        Assert.Equal(0, d.Kupci.Aktivni);
        Assert.Null(d.Kupci.Zadrzavanje);
        Assert.Null(d.Dostava.Uspjesnost);
        Assert.Empty(d.Gazdinstva);
    }

    [Fact]
    public async Task Neispravan_mjesec_je_greska_domena()
    {
        using var p = await PoslovanjePodaciTest.NapraviAsync();
        var ex = await Assert.ThrowsAsync<EPiljara.Domain.Common.DomainException>(() => p.MjesecAsync(13));
        Assert.Equal("mjesec_neispravan", ex.Code);
    }
}

public class PoslovanjeKupciTests
{
    [Fact]
    public async Task Novi_povratni_i_zadrzavanje()
    {
        using var p = await PoslovanjePodaciTest.NapraviAsync();
        var marko = await p.KupacAsync("Marko");
        var jovan = await p.KupacAsync("Jovan");
        await p.NarudzbaAsync(38, OrderStatus.Delivered, krompirKg: 1, kupacId: marko.Id);   // septembar
        await p.NarudzbaAsync(39, OrderStatus.Delivered, krompirKg: 1, kupacId: jovan.Id);   // septembar
        await p.NarudzbaAsync(41, OrderStatus.Delivered, krompirKg: 2, kupacId: marko.Id);
        await p.NarudzbaAsync(42, OrderStatus.Delivered, krompirKg: 3, kupacId: marko.Id);
        await p.NarudzbaAsync(41, OrderStatus.Delivered, jaja: 10, kupac: "Ana", email: " Ana@Mail.com ");

        var k = (await p.MjesecAsync(10)).Kupci;

        Assert.Equal(2, k.Aktivni);               // Marko, Ana
        Assert.Equal(2, k.AktivniPrethodni);      // Marko, Jovan
        Assert.Equal(1, k.Novi);                  // Ana
        Assert.Equal(1, k.Povratni);              // Marko
        Assert.Equal(0.5m, k.Zadrzavanje);        // od Marka i Jovana vratio se Marko
        Assert.Equal(1, k.Gosti);
        Assert.Equal(1, k.Registrovani);
        Assert.Equal(1.5m, k.NarudzbiPoKupcu);    // 3 narudžbe / 2 kupca
        Assert.Equal(7.5m, k.PotrosnjaPoKupcu);   // (4 + 6 + 5) / 2
        Assert.Equal("Marko Marković", k.TopKupci[0].Ime);
        Assert.Equal(2, k.TopKupci[0].Narudzbi);
    }

    [Fact]
    public async Task Isti_gost_po_emailu_se_ne_broji_dvaput_a_gosti_bez_kontakta_se_ne_spajaju()
    {
        using var p = await PoslovanjePodaciTest.NapraviAsync();
        await p.NarudzbaAsync(41, OrderStatus.Delivered, jaja: 2, kupac: "Ana", email: "ana@mail.com");
        await p.NarudzbaAsync(42, OrderStatus.Delivered, jaja: 2, kupac: "Ana", email: "ANA@mail.com ");
        await p.NarudzbaAsync(41, OrderStatus.Delivered, jaja: 2, kupac: "Gost 1");
        await p.NarudzbaAsync(42, OrderStatus.Delivered, jaja: 2, kupac: "Gost 2");

        var k = (await p.MjesecAsync(10)).Kupci;

        Assert.Equal(3, k.Aktivni);
        Assert.Equal(3, k.Gosti);
    }

    [Fact]
    public async Task Novi_preko_preporuke()
    {
        using var p = await PoslovanjePodaciTest.NapraviAsync();
        var marko = await p.KupacAsync("Marko");
        var jovan = await p.KupacAsync("Jovan", preporucio: marko.Id);
        await p.NarudzbaAsync(41, OrderStatus.Delivered, krompirKg: 1, kupacId: jovan.Id);

        Assert.Equal(1, (await p.MjesecAsync(10)).Kupci.NoviPrekoPreporuke);
    }
}

public class PoslovanjeProdajaTests
{
    [Fact]
    public async Task Korpice_spajz_po_kategoriji_i_top_artikli()
    {
        using var p = await PoslovanjePodaciTest.NapraviAsync();
        await p.NarudzbaAsync(41, OrderStatus.Delivered, krompirKg: 3, korpica: 2, jaja: 10);

        var d = (await p.MjesecAsync(10)).Prodaja;

        var k = Assert.Single(d.Korpice);
        Assert.Equal("Mala korpica povrća", k.Naziv);
        Assert.Equal("povrce", k.Tip);
        Assert.Equal("Mala", k.Velicina);
        Assert.Equal(2m, k.Komada);
        Assert.Equal(20m, k.Prihod);
        Assert.Equal(4m, k.Nabavno);              // 2 kutije × 2 kg × 1 KM
        Assert.Equal(0.8m, k.Marza);
        Assert.Equal(["Jaja", "Povrće"], d.Spajz.Select(s => s.Kategorija).OrderBy(x => x).ToArray());
        var krompir = d.TopArtikli.Single(a => a.Naziv == "Krompir");
        Assert.Equal(7m, krompir.Kolicina);       // 3 Špajz + 4 iz korpica
        Assert.Equal(14m, krompir.Prihod);
        Assert.Equal(7m, krompir.Zarada);
    }

    [Fact]
    public async Task Gazdinstva_sabiraju_nabavno_po_proizvodjacu()
    {
        using var p = await PoslovanjePodaciTest.NapraviAsync();
        await p.NarudzbaAsync(41, OrderStatus.Delivered, krompirKg: 3, korpica: 1, jaja: 10);

        var g = (await p.MjesecAsync(10)).Gazdinstva;

        Assert.Equal(["Marko", "Ana"], g.Select(x => x.Naziv).ToArray()); // po nabavnom, opadajuće
        Assert.Equal(5m, g[0].Nabavno);           // 3 + 2 kg krompira
        Assert.Equal(5m, Assert.Single(g[0].Artikli).Kolicina);
        Assert.Equal(3m, g[1].Nabavno);
    }

    [Fact]
    public async Task Obrisan_artikal_se_i_dalje_broji_kod_svog_gazdinstva()
    {
        using var p = await PoslovanjePodaciTest.NapraviAsync();
        await p.NarudzbaAsync(41, OrderStatus.Delivered, krompirKg: 2);
        var k = await p.T.Db.Products.IgnoreQueryFilters().FirstAsync(x => x.Id == p.Krompir.Id);
        k.IsDeleted = true;
        await p.T.Db.SaveChangesAsync();
        p.T.Db.ChangeTracker.Clear();

        var d = await p.MjesecAsync(10);

        Assert.Equal("Krompir", Assert.Single(d.Prodaja.TopArtikli).Naziv);
        Assert.Equal("Marko", Assert.Single(d.Gazdinstva).Naziv);
        Assert.Equal(2m, d.Gazdinstva[0].Nabavno);
    }

    [Fact]
    public async Task Dostava_po_zoni_uspjesnost_i_otkazane()
    {
        using var p = await PoslovanjePodaciTest.NapraviAsync();
        await p.NarudzbaAsync(41, OrderStatus.Delivered, krompirKg: 2);
        await p.NarudzbaAsync(41, OrderStatus.Delivered, krompirKg: 1, zona: p.Laus);
        await p.NarudzbaAsync(42, OrderStatus.Delivered, krompirKg: 1, zona: p.Laus);
        await p.NarudzbaAsync(42, OrderStatus.FailedDelivery, krompirKg: 1);
        await p.NarudzbaAsync(43, OrderStatus.Cancelled, krompirKg: 5);

        var d = (await p.MjesecAsync(10)).Dostava;

        Assert.Equal(3, d.Isporucene);
        Assert.Equal(1, d.Neuspjele);
        Assert.Equal(0.75m, d.Uspjesnost);
        Assert.Equal(1, d.Otkazane);
        Assert.Equal(10m, d.OtkazaneVrijednost);
        Assert.Equal(0.2m, d.OtkazaneUdio);       // 1 od 5 narudžbi
        Assert.Equal("Lauš", d.PoZoni[0].Zona);   // 2 narudžbe, 4 KM
        Assert.Equal(2, d.PoZoni[0].Narudzbi);
    }
}

public class PoslovanjePretplateTests
{
    private static async Task<Pretplata> PretplataAsync(PoslovanjePodaciTest p, User u, DateTime kreirana,
        DateTime? otkazana = null, PlanPretplate plan = PlanPretplate.Mjesecna)
    {
        var x = new Pretplata
        {
            UserId = u.Id, ProductId = p.Korpica.Id, DeliveryZoneId = p.Centar.Id, DeliverySlotId = p.Slot.Id,
            DeliveryAddress = "Testna 1", Plan = plan, CreatedAt = kreirana, OtkazanaU = otkazana,
            Status = otkazana is null ? StatusPretplate.Aktivna : StatusPretplate.Otkazana,
        };
        p.T.Db.Pretplate.Add(x);
        await p.T.Db.SaveChangesAsync();
        return x;
    }

    /// <summary>
    /// Oktobar 2025 (prošlost, pa nikad „tekući" mjesec — Pauzirane mora biti null).
    /// Prva: od avgusta, aktivna. Druga: od septembra, otkazana 10.10. Treća: nova 5.10.
    /// Sedmica 41/2025 = isporuka u četvrtak 9.10.2025.
    /// </summary>
    [Fact]
    public async Task Aktivne_nove_otkazane_i_stopa()
    {
        using var p = await PoslovanjePodaciTest.NapraviAsync();
        // Kupac smije imati samo jednu pretplatu na istu korpicu — svaka pretplata svoj kupac.
        var u = await p.KupacAsync("Marko");
        await PretplataAsync(p, u, new DateTime(2025, 8, 20, 10, 0, 0, DateTimeKind.Utc));
        await PretplataAsync(p, await p.KupacAsync("Jovan"), new DateTime(2025, 9, 15, 10, 0, 0, DateTimeKind.Utc),
            otkazana: new DateTime(2025, 10, 10, 10, 0, 0, DateTimeKind.Utc));
        var treca = await PretplataAsync(p, await p.KupacAsync("Ana"), new DateTime(2025, 10, 5, 10, 0, 0, DateTimeKind.Utc), plan: PlanPretplate.Nedjeljna);
        await p.NarudzbaAsync(41, OrderStatus.Delivered, korpica: 1, kupacId: u.Id, pretplataId: treca.Id, godina: 2025);
        await p.NarudzbaAsync(41, OrderStatus.Delivered, krompirKg: 5, kupacId: u.Id, godina: 2025);

        var d = (await p.MjesecAsync(10, godina: 2025)).Pretplate;

        Assert.Equal(2, d.AktivneNaKraju);        // prva i treća
        Assert.Equal(1, d.Nove);
        Assert.Equal(1, d.Otkazane);
        Assert.Equal(0.5m, d.StopaOtkazivanja);   // 1 od 2 aktivne na početku oktobra
        Assert.Null(d.Pauzirane);                 // samo za tekući mjesec
        Assert.Equal(0.5m, d.UdioPrihoda);        // 10 od 20 KM
        Assert.Contains(d.PoPlanu, x => x.Plan == "Nedjeljna" && x.Aktivne == 1);
    }
}

public class PoslovanjeGodinaTests
{
    [Fact]
    public async Task Dvanaest_mjeseci_i_sezona_po_kategoriji()
    {
        using var p = await PoslovanjePodaciTest.NapraviAsync();
        await p.NarudzbaAsync(41, OrderStatus.Delivered, krompirKg: 3);           // oktobar
        await p.NarudzbaAsync(39, OrderStatus.Delivered, jaja: 10);               // septembar
        await p.NarudzbaAsync(42, OrderStatus.Cancelled, krompirKg: 50);          // ne broji se

        var g = await new GetPoslovanjeGodinaHandler(p.T.Db, Options.Create(TestOpcije.Korpice()))
            .Handle(new GetPoslovanjeGodinaQuery(2026), default);

        Assert.Equal(12, g.Mjeseci.Count);
        Assert.Equal(6m, g.Mjeseci[9].Neto);      // oktobar
        Assert.Equal(5m, g.Mjeseci[8].Neto);      // septembar
        Assert.Equal(0, g.Mjeseci[0].Narudzbi);
        var povrce = g.Sezona.Single(s => s.Kategorija == "Povrće");
        Assert.Equal(6m, povrce.Mjeseci[9]);
        Assert.Equal(0m, povrce.Mjeseci[8]);
    }

    /// <summary>Mjesec koji još nije počeo nema broj pretplata — inače bi pokazao današnji broj kao budući.</summary>
    [Fact]
    public async Task Buduci_mjeseci_nemaju_broj_pretplata()
    {
        using var p = await PoslovanjePodaciTest.NapraviAsync();
        var handler = new GetPoslovanjeGodinaHandler(p.T.Db, Options.Create(TestOpcije.Korpice()));

        var buduca = await handler.Handle(new GetPoslovanjeGodinaQuery(2099), default);
        var prosla = await handler.Handle(new GetPoslovanjeGodinaQuery(2025), default);

        Assert.All(buduca.Mjeseci, m => Assert.Null(m.AktivnePretplate));
        Assert.All(prosla.Mjeseci, m => Assert.Equal(0, m.AktivnePretplate));
    }
}

public class PoslovanjeKalendarTests
{
    [Fact]
    public void Sedmica_na_prelazu_ide_u_mjesec_isporuke()
    {
        // Sedmica 40/2026 počinje u ponedjeljak 28.9, a isporuka je u četvrtak 1.10.
        Assert.Equal((2026, 10), PoslovanjeKalendar.MjesecIsporuke(2026, 40, DayOfWeek.Thursday));
        Assert.Equal((2026, 9), PoslovanjeKalendar.MjesecIsporuke(2026, 40, DayOfWeek.Monday));
    }

    [Fact]
    public void Oktobar_2026_ima_pet_cetvrtaka()
    {
        Assert.Equal(
            [(2026, 40), (2026, 41), (2026, 42), (2026, 43), (2026, 44)],
            PoslovanjeKalendar.SedmiceMjeseca(2026, 10, DayOfWeek.Thursday));
    }

    [Fact]
    public void Januar_uzima_sedmicu_prethodne_iso_godine_i_prethodni_je_decembar()
    {
        // 1.1.2027 je petak i pripada ISO sedmici 53/2026.
        Assert.Equal((2026, 53), PoslovanjeKalendar.SedmiceMjeseca(2027, 1, DayOfWeek.Friday)[0]);
        Assert.Equal((2026, 12), PoslovanjeKalendar.Prethodni(2027, 1));
        Assert.Equal((2026, 10), PoslovanjeKalendar.Prethodni(2026, 11));
    }

    [Fact]
    public void Pocetak_mjeseca_je_lokalna_ponoc()
    {
        // 1.10.2026 00:00 u Sarajevu (ljetno vrijeme, UTC+2) = 30.9 22:00 UTC.
        Assert.Equal(new DateTime(2026, 9, 30, 22, 0, 0, DateTimeKind.Utc), PoslovanjeKalendar.PocetakUtc(2026, 10));
    }
}
