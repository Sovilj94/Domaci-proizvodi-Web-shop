using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.Features.Admin;
using EPiljara.Application.Features.Fiskalizacija;
using EPiljara.Application.Features.Poeni;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Unit = EPiljara.Domain.Enums.Unit;

namespace EPiljara.Application.Tests;

/// <summary>Spec: docs/superpowers/specs/2026-11-04-fiskalizacija-design.md.</summary>
public class FiskalizacijaTests
{
    private sealed class TestDobavljac : IFiskalniDobavljac
    {
        public string Naziv => "test";
        public bool Probni => true;
        public Func<FiskalniZahtjev, FiskalniOdgovor> Odgovor { get; set; } =
            z => new FiskalniOdgovor(true, Broj: $"T-{z.RacunId:N}"[..10], Brojac: "1/1PP", Vrijeme: new DateTime(2026, 11, 5, 15, 0, 0, DateTimeKind.Utc), QrSadrzaj: "qr");
        public List<FiskalniZahtjev> Primljeni { get; } = [];
        public Task<FiskalniOdgovor> IzdajAsync(FiskalniZahtjev zahtjev, CancellationToken ct)
        {
            Primljeni.Add(zahtjev);
            return Task.FromResult(Odgovor(zahtjev));
        }
    }

    private sealed record Osnova(TestDb T, Order Narudzba, TestDobavljac Dobavljac);

    /// <summary>Narudžba na putu: 2 kg sira po 10 KM + korpica 25 KM, dostava 3 KM, promo 4,50 i poeni 1,00.</summary>
    private static Osnova Napravi(OrderStatus status = OrderStatus.OutForDelivery)
    {
        var t = new TestDb();
        var slot = new DeliverySlot { DayOfWeek = 4, TimeFrom = "16:00", TimeTo = "20:00", CutoffHours = 48 };
        var zona = new DeliveryZone { Name = "Centar", DeliveryFee = 3m };
        var kat = new Category { Name = "Mliječno", Slug = "mlijecno" };
        var proiz = new Producer { Name = "Jelena Petrović", Village = "Šargovac" };
        var sir = new Product { Name = "Sir", Slug = "sir", Price = 10m, Unit = Unit.Kg, CategoryId = kat.Id, ProducerId = proiz.Id };
        var o = new Order
        {
            Code = "EP-2611-0001", PublicToken = "t", CustomerName = "Marko Marković", Status = status,
            DeliveryZoneId = zona.Id, DeliverySlotId = slot.Id, Godina = 2026, Sedmica = 45,
            Subtotal = 45m, DeliveryFee = 3m, Popust = 4.50m, PoeniPopust = 1m, TotalAmount = 42.50m,
        };
        o.Items.Add(new OrderItem { OrderId = o.Id, ProductId = sir.Id, ProductName = "Sir", Unit = Unit.Kg, Quantity = 2m, UnitPrice = 10m });
        o.Items.Add(new OrderItem { OrderId = o.Id, ProductId = sir.Id, ProductName = "Srednja korpica povrća", Unit = Unit.Kom, Quantity = 1m, UnitPrice = 25m });
        t.Db.AddRange(slot, zona, kat, proiz, sir);
        t.Db.Orders.Add(o);
        t.Db.SaveChanges();
        t.Db.ChangeTracker.Clear();
        return new Osnova(t, o, new TestDobavljac());
    }

    private static Task IsporuciAsync(Osnova o, string status = "Delivered") =>
        new UpdateOrderStatusHandler(o.T.Db, Options.Create(new PoeniOpcije())).Handle(new UpdateOrderStatusCommand(o.Narudzba.Id, status), default);

    private static Task<int> ObradiAsync(Osnova o, DateTime sada) =>
        new ObradiFiskalneRacuneHandler(o.T.Db, o.Dobavljac).Handle(new ObradiFiskalneRacuneCommand(sada), default);

    private static async Task<FiskalniRacun> RacunAsync(Osnova o, VrstaFiskalnogRacuna vrsta = VrstaFiskalnogRacuna.Promet)
    {
        o.T.Db.ChangeTracker.Clear();
        return await o.T.Db.FiskalniRacuni.AsNoTracking().SingleAsync(r => r.OrderId == o.Narudzba.Id && r.Vrsta == vrsta);
    }

    [Fact]
    public void Stavke_su_artikli_i_dostava_a_popust_je_rasporedjen_do_tacnog_iznosa()
    {
        var o = Napravi();
        using var _ = o.T;
        var n = o.T.Db.Orders.Include(x => x.Items).Single();

        var stavke = FiskalniSastav.Stavke(n, "A");

        Assert.Equal(["Dostava", "Sir", "Srednja korpica povrća"], stavke.Select(s => s.Naziv).Order());
        Assert.Equal("Dostava", stavke[^1].Naziv);
        Assert.All(stavke, s => Assert.Equal("A", s.PoreskaOznaka));
        Assert.Equal(5.50m, stavke.Sum(s => s.Popust));
        Assert.Equal(42.50m, stavke.Sum(s => s.Ukupno));
        Assert.All(stavke, s => Assert.Equal(Math.Round(s.Kolicina * s.JedinicnaCijena, 2) - s.Popust, s.Ukupno));
    }

    [Fact]
    public async Task Isporuka_upisuje_prometni_racun_na_cekanju_jednom()
    {
        var o = Napravi();
        using var _ = o.T;

        await IsporuciAsync(o);
        await IsporuciAsync(o); // isti status ponovo — bez drugog računa

        var r = await RacunAsync(o);
        Assert.Equal(StatusFiskalnogRacuna.NaCekanju, r.Status);
        Assert.Equal(42.50m, r.Iznos);
        Assert.Contains("\"Dostava\"", r.StavkeJson);
        Assert.Equal(1, await o.T.Db.FiskalniRacuni.CountAsync());
    }

    [Fact]
    public async Task Neuspjela_isporuka_ne_pravi_racun()
    {
        var o = Napravi();
        using var _ = o.T;

        await IsporuciAsync(o, "FailedDelivery");

        Assert.False(await o.T.Db.FiskalniRacuni.AnyAsync());
    }

    [Fact]
    public async Task Obveznik_pdv_a_dobija_oznaku_pdv_a()
    {
        var o = Napravi();
        using var _ = o.T;
        o.T.Db.Podesavanja.Add(new Podesavanja
        {
            Id = Podesavanja.JediniId, VrijemeIsporukeRezerva = "16:00",
            FiskalPdvObveznik = true, FiskalStopaPdv = 17m, FiskalOznakaPdv = "Ђ", FiskalOznakaBezPdv = "A",
        });
        await o.T.Db.SaveChangesAsync();

        await IsporuciAsync(o);

        Assert.Contains("\"PoreskaOznaka\":\"Ђ\"", (await RacunAsync(o)).StavkeJson);
    }

    [Fact]
    public async Task Obrada_salje_dobavljacu_i_upisuje_broj()
    {
        var o = Napravi();
        using var _ = o.T;
        await IsporuciAsync(o);

        Assert.Equal(1, await ObradiAsync(o, DateTime.UtcNow));

        var r = await RacunAsync(o);
        Assert.Equal(StatusFiskalnogRacuna.Izdat, r.Status);
        Assert.StartsWith("T-", r.Broj);
        Assert.Equal("test", r.Dobavljac);
        var z = Assert.Single(o.Dobavljac.Primljeni);
        Assert.Equal(r.Id, z.RacunId);
        Assert.Equal(42.50m, z.Ukupno);
        Assert.Equal(0, await ObradiAsync(o, DateTime.UtcNow)); // izdat se ne šalje opet
    }

    [Fact]
    public async Task Greska_dobavljaca_se_ponavlja_tek_poslije_razmaka()
    {
        var o = Napravi();
        using var _ = o.T;
        await IsporuciAsync(o);
        o.Dobavljac.Odgovor = _ => new FiskalniOdgovor(false, Greska: "PFR nedostupan");
        var sada = DateTime.UtcNow;

        await ObradiAsync(o, sada);
        var r = await RacunAsync(o);
        Assert.Equal(StatusFiskalnogRacuna.Greska, r.Status);
        Assert.Equal(1, r.Pokusaja);
        Assert.Equal("PFR nedostupan", r.ZadnjaGreska);
        Assert.True(r.SljedeciPokusaj > sada);

        Assert.Equal(0, await ObradiAsync(o, sada.AddSeconds(10)));
        o.Dobavljac.Odgovor = z => new FiskalniOdgovor(true, Broj: "T-2", Vrijeme: sada);
        Assert.Equal(1, await ObradiAsync(o, sada.AddMinutes(2)));
        Assert.Equal(StatusFiskalnogRacuna.Izdat, (await RacunAsync(o)).Status);
    }

    [Fact]
    public async Task Trajna_greska_ceka_admina()
    {
        var o = Napravi();
        using var _ = o.T;
        await IsporuciAsync(o);
        o.Dobavljac.Odgovor = _ => new FiskalniOdgovor(false, Greska: "Neispravna oznaka", Trajna: true);
        await ObradiAsync(o, DateTime.UtcNow);

        Assert.Equal(0, await ObradiAsync(o, DateTime.UtcNow.AddDays(1)));

        var r = await RacunAsync(o);
        await new PonoviFiskalniRacunHandler(o.T.Db).Handle(new PonoviFiskalniRacunCommand(r.Id), default);
        o.Dobavljac.Odgovor = _ => new FiskalniOdgovor(true, Broj: "T-3", Vrijeme: DateTime.UtcNow);
        Assert.Equal(1, await ObradiAsync(o, DateTime.UtcNow.AddDays(1)));
    }

    [Fact]
    public async Task Probni_dobavljac_daje_isti_broj_za_isti_racun_i_odbija_stavku_bez_oznake()
    {
        var d = new EPiljara.Infrastructure.Services.ProbniFiskalniDobavljac();
        var id = Guid.NewGuid();
        var stavka = new FiskalnaStavka("Sir", 1m, "kg", 10m, 0m, 10m, "A");
        var z = new FiskalniZahtjev(id, VrstaFiskalnogRacuna.Promet, "Gotovina", [stavka], 10m, null, null);

        var a = await d.IzdajAsync(z, default);
        var b = await d.IzdajAsync(z, default);
        Assert.True(a.Uspjeh);
        Assert.Equal(a.Broj, b.Broj);
        Assert.Contains("NEMA FISKALNU VRIJEDNOST", a.QrSadrzaj);

        var bez = await d.IzdajAsync(z with { Stavke = [stavka with { PoreskaOznaka = "" }] }, default);
        Assert.False(bez.Uspjeh);
        Assert.True(bez.Trajna);
    }

    [Fact]
    public async Task Pregled_pokazuje_racune_na_cekanju_i_van_perioda()
    {
        var o = Napravi();
        using var _ = o.T;
        await IsporuciAsync(o);

        var p = await new GetFiskalniRacuniHandler(o.T.Db, o.Dobavljac)
            .Handle(new GetFiskalniRacuniQuery(new DateOnly(2020, 1, 1), new DateOnly(2020, 1, 31)), default);

        Assert.Equal(1, p.NaCekanju);
        Assert.Equal("EP-2611-0001", Assert.Single(p.Racuni).KodNarudzbe);
        Assert.Equal(0, p.Izdato);
    }

    [Fact]
    public async Task Refundacija_trazi_izdat_prometni_i_referise_ga()
    {
        var o = Napravi();
        using var _ = o.T;
        await IsporuciAsync(o);
        var refundiraj = new RefundirajFiskalniRacunHandler(o.T.Db);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => refundiraj.Handle(new RefundirajFiskalniRacunCommand(o.Narudzba.Id), default));
        Assert.Equal("promet_nije_izdat", ex.Code);

        await ObradiAsync(o, DateTime.UtcNow);
        await refundiraj.Handle(new RefundirajFiskalniRacunCommand(o.Narudzba.Id), default);
        await ObradiAsync(o, DateTime.UtcNow);

        var promet = await RacunAsync(o);
        var refund = await RacunAsync(o, VrstaFiskalnogRacuna.Refundacija);
        Assert.Equal(StatusFiskalnogRacuna.Izdat, refund.Status);
        Assert.Equal(promet.Id, refund.RefundiraniRacunId);
        Assert.Equal(promet.Broj, o.Dobavljac.Primljeni.Last().ReferentniBroj);

        var dvaput = await Assert.ThrowsAsync<ConflictException>(() => refundiraj.Handle(new RefundirajFiskalniRacunCommand(o.Narudzba.Id), default));
        Assert.Equal("vec_refundirano", dvaput.Code);
    }
}
