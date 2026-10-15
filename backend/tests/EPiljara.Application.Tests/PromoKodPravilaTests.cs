using EPiljara.Application.Features.PromoKodovi;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;

namespace EPiljara.Application.Tests;

/// <summary>Promo kod (P6b, Task 1): računanje popusta i sva pravila primjene.</summary>
public class PromoKodPravilaTests
{
    private static readonly DateOnly Danas = new(2026, 10, 15);
    private static readonly KupacZaPromo Niko = new(null, null);

    private sealed class Okruzenje : IDisposable
    {
        public TestDb T { get; } = new();
        public DeliveryZone Zona { get; } = new() { Name = "Centar", MinOrderAmount = 0m, DeliveryFee = 3m, IsActive = true };
        public DeliverySlot Slot { get; } = new() { DayOfWeek = 4, TimeFrom = "16:00", TimeTo = "20:00", CutoffHours = 48 };
        private int _brojac;

        public async Task<Okruzenje> PripremiAsync()
        {
            T.Db.DeliveryZones.Add(Zona);
            T.Db.DeliverySlots.Add(Slot);
            await T.Db.SaveChangesAsync();
            return this;
        }

        public async Task<PromoKod> KodAsync(Action<PromoKod>? podesi = null, string kod = "JESEN5")
        {
            var k = new PromoKod { Kod = kod, Opis = "Test", Tip = TipPopusta.Iznos, Vrijednost = 5m };
            podesi?.Invoke(k);
            T.Db.PromoKodovi.Add(k);
            await T.Db.SaveChangesAsync();
            return k;
        }

        public async Task NarudzbaAsync(
            Guid? customerId = null, string? email = null,
            OrderStatus status = OrderStatus.Pending, Guid? promoKodId = null)
        {
            _brojac++;
            T.Db.Orders.Add(new Order
            {
                Code = $"EP-{_brojac:0000}", PublicToken = Guid.NewGuid().ToString("N"),
                CustomerId = customerId, CustomerEmail = email, CustomerName = "Test",
                Status = status, PromoKodId = promoKodId, DeliveryAddress = "Testna 1",
                DeliverySlotId = Slot.Id, DeliveryZoneId = Zona.Id,
            });
            await T.Db.SaveChangesAsync();
        }

        public Task<PromoKod> ProvjeriAsync(string unos, decimal subtotal = 50m, KupacZaPromo? kupac = null) =>
            PromoKodPravila.ProvjeriAsync(T.Db, unos, subtotal, kupac ?? Niko, Danas, default);

        public void Dispose() => T.Dispose();
    }

    private static Task<Okruzenje> NovoAsync() => new Okruzenje().PripremiAsync();

    private static async Task AssertPorukaAsync(Task<PromoKod> poziv, string poruka)
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() => poziv);
        Assert.Equal(poruka, ex.Message);
        Assert.Equal("promo_kod_nevazeci", ex.Code);
    }

    [Fact]
    public void Procenat_se_zaokruzuje_od_nule_navise()
    {
        var kod = new PromoKod { Tip = TipPopusta.Procenat, Vrijednost = 10m };
        Assert.Equal(3.33m, PromoKodPravila.Popust(kod, 33.25m)); // 3.325; bankarsko bi dalo 3.32
    }

    [Fact]
    public void Fiksni_iznos_se_odbija_u_cjelini()
    {
        var kod = new PromoKod { Tip = TipPopusta.Iznos, Vrijednost = 5m };
        Assert.Equal(5m, PromoKodPravila.Popust(kod, 20m));
    }

    [Fact]
    public void Fiksni_iznos_veci_od_robe_ne_prelazi_robu()
    {
        var kod = new PromoKod { Tip = TipPopusta.Iznos, Vrijednost = 5m };
        Assert.Equal(4m, PromoKodPravila.Popust(kod, 4m));
    }

    [Fact]
    public void Normalizuj_trimuje_i_pretvara_u_velika_slova()
    {
        Assert.Equal("DOBRODOSLI10", PromoKodPravila.Normalizuj(" dobrodosli10 "));
    }

    [Fact]
    public async Task Kod_se_nalazi_bez_obzira_na_razmake_i_velika_slova()
    {
        using var o = await NovoAsync();
        var kod = await o.KodAsync();

        var nadjen = await o.ProvjeriAsync(" jesen5 ");

        Assert.Equal(kod.Id, nadjen.Id);
    }

    [Fact]
    public async Task Nepostojeci_kod_je_nevazeci()
    {
        using var o = await NovoAsync();
        await AssertPorukaAsync(o.ProvjeriAsync("NEMA"), "Promo kod ne postoji ili nije aktivan.");
    }

    [Fact]
    public async Task Ugasen_kod_daje_istu_poruku_kao_nepostojeci()
    {
        using var o = await NovoAsync();
        await o.KodAsync(k => k.Aktivan = false);
        await AssertPorukaAsync(o.ProvjeriAsync("JESEN5"), "Promo kod ne postoji ili nije aktivan.");
    }

    [Fact]
    public async Task Kod_prije_pocetka_vazenja_jos_ne_vazi()
    {
        using var o = await NovoAsync();
        await o.KodAsync(k => k.VaziOd = Danas.AddDays(1));
        await AssertPorukaAsync(o.ProvjeriAsync("JESEN5"), "Promo kod još ne važi.");
    }

    [Fact]
    public async Task Kod_poslije_roka_je_istekao()
    {
        using var o = await NovoAsync();
        await o.KodAsync(k => k.VaziDo = Danas.AddDays(-1));
        await AssertPorukaAsync(o.ProvjeriAsync("JESEN5"), "Promo kod je istekao.");
    }

    [Fact]
    public async Task Posljednji_dan_roka_je_ukljucen()
    {
        using var o = await NovoAsync();
        await o.KodAsync(k => k.VaziDo = Danas);
        Assert.NotNull(await o.ProvjeriAsync("JESEN5"));
    }

    [Fact]
    public async Task Prvi_dan_vazenja_je_ukljucen()
    {
        using var o = await NovoAsync();
        await o.KodAsync(k => k.VaziOd = Danas);
        Assert.NotNull(await o.ProvjeriAsync("JESEN5"));
    }

    [Fact]
    public async Task Iskoristen_limit_koristenja_odbija_kod()
    {
        using var o = await NovoAsync();
        var kod = await o.KodAsync(k => k.MaxKoristenja = 1);
        await o.NarudzbaAsync(promoKodId: kod.Id);
        await AssertPorukaAsync(o.ProvjeriAsync("JESEN5"), "Promo kod je iskorišten.");
    }

    [Fact]
    public async Task Otkazana_narudzba_ne_trosi_limit()
    {
        using var o = await NovoAsync();
        var kod = await o.KodAsync(k => k.MaxKoristenja = 1);
        await o.NarudzbaAsync(status: OrderStatus.Cancelled, promoKodId: kod.Id);
        Assert.NotNull(await o.ProvjeriAsync("JESEN5"));
    }

    [Fact]
    public async Task Jednom_po_kupcu_odbija_registrovanog_koji_je_vec_koristio()
    {
        using var o = await NovoAsync();
        var kod = await o.KodAsync(k => k.JednomPoKupcu = true);
        var kupac = Guid.NewGuid();
        await o.NarudzbaAsync(customerId: kupac, promoKodId: kod.Id);

        await AssertPorukaAsync(
            o.ProvjeriAsync("JESEN5", kupac: new KupacZaPromo(kupac, null)),
            "Promo kod ste već iskoristili.");
    }

    [Fact]
    public async Task Jednom_po_kupcu_prepoznaje_gosta_po_emailu_bez_obzira_na_velika_slova()
    {
        using var o = await NovoAsync();
        var kod = await o.KodAsync(k => k.JednomPoKupcu = true);
        await o.NarudzbaAsync(email: "Ana@X.ba", promoKodId: kod.Id);

        await AssertPorukaAsync(
            o.ProvjeriAsync("JESEN5", kupac: new KupacZaPromo(null, "ana@x.ba")),
            "Promo kod ste već iskoristili.");
    }

    [Fact]
    public async Task Jednom_po_kupcu_otkazana_narudzba_ne_racuna_se()
    {
        using var o = await NovoAsync();
        var kod = await o.KodAsync(k => k.JednomPoKupcu = true);
        var kupac = Guid.NewGuid();
        await o.NarudzbaAsync(customerId: kupac, status: OrderStatus.Cancelled, promoKodId: kod.Id);

        Assert.NotNull(await o.ProvjeriAsync("JESEN5", kupac: new KupacZaPromo(kupac, null)));
    }

    [Fact]
    public async Task Jednom_po_kupcu_narudzba_drugog_kupca_ne_smeta()
    {
        using var o = await NovoAsync();
        var kod = await o.KodAsync(k => k.JednomPoKupcu = true);
        await o.NarudzbaAsync(customerId: Guid.NewGuid(), promoKodId: kod.Id);

        Assert.NotNull(await o.ProvjeriAsync("JESEN5", kupac: new KupacZaPromo(Guid.NewGuid(), null)));
    }

    [Fact]
    public async Task Samo_novi_kupci_odbija_kupca_sa_ranijom_narudzbom_bez_koda()
    {
        using var o = await NovoAsync();
        await o.KodAsync(k => k.SamoNoviKupci = true);
        var kupac = Guid.NewGuid();
        await o.NarudzbaAsync(customerId: kupac);

        await AssertPorukaAsync(
            o.ProvjeriAsync("JESEN5", kupac: new KupacZaPromo(kupac, null)),
            "Promo kod važi samo za prvu narudžbu.");
    }

    [Fact]
    public async Task Samo_novi_kupci_propusta_kupca_sa_samo_otkazanom_narudzbom()
    {
        using var o = await NovoAsync();
        await o.KodAsync(k => k.SamoNoviKupci = true);
        var kupac = Guid.NewGuid();
        await o.NarudzbaAsync(customerId: kupac, status: OrderStatus.Cancelled);

        Assert.NotNull(await o.ProvjeriAsync("JESEN5", kupac: new KupacZaPromo(kupac, null)));
    }

    [Fact]
    public async Task Samo_novi_kupci_propusta_gosta_bez_emaila()
    {
        using var o = await NovoAsync();
        await o.KodAsync(k => k.SamoNoviKupci = true);
        await o.NarudzbaAsync(customerId: Guid.NewGuid());

        Assert.NotNull(await o.ProvjeriAsync("JESEN5", kupac: new KupacZaPromo(null, null)));
    }

    [Fact]
    public async Task Ispod_minimalnog_iznosa_kod_ne_vazi()
    {
        using var o = await NovoAsync();
        await o.KodAsync(k => k.MinIznos = 30m);
        await AssertPorukaAsync(o.ProvjeriAsync("JESEN5", 29.99m), "Promo kod važi za narudžbe od 30 KM.");
    }

    [Fact]
    public async Task Tacno_minimalni_iznos_prolazi()
    {
        using var o = await NovoAsync();
        await o.KodAsync(k => k.MinIznos = 30m);
        Assert.NotNull(await o.ProvjeriAsync("JESEN5", 30m));
    }
}
