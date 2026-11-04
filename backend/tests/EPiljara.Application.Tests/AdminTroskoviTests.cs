using EPiljara.Application.Features.Admin.Troskovi;
using EPiljara.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Tests;

public class AdminTroskoviTests
{
    private static Task<Guid> VrstaAsync(TestDb t, string naziv, bool gorivo = false) =>
        new SacuvajVrstuTroskaHandler(t.Db).Handle(new SacuvajVrstuTroskaCommand(null, naziv, gorivo, 1), default);

    [Fact]
    public async Task Dvije_vrste_istog_naziva_nisu_dozvoljene()
    {
        using var t = new TestDb();
        await VrstaAsync(t, "Gorivo");

        var ex = await Assert.ThrowsAsync<ConflictException>(() => VrstaAsync(t, "  gorivo "));
        Assert.Equal("vrsta_postoji", ex.Code);
    }

    [Fact]
    public async Task Vrsta_sa_troskovima_se_ne_brise()
    {
        using var t = new TestDb();
        var id = await VrstaAsync(t, "Zakup");
        await new SacuvajTrosakHandler(t.Db).Handle(new SacuvajTrosakCommand(null, new DateOnly(2026, 10, 1), id, 200m, "Magacin", null), default);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            new ObrisiVrstuTroskaHandler(t.Db).Handle(new ObrisiVrstuTroskaCommand(id), default));
        Assert.Equal("vrsta_u_upotrebi", ex.Code);
    }

    [Fact]
    public async Task Trosak_mora_imati_pozitivan_iznos()
    {
        using var t = new TestDb();
        var id = await VrstaAsync(t, "Ostalo");

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            new SacuvajTrosakHandler(t.Db).Handle(new SacuvajTrosakCommand(null, new DateOnly(2026, 10, 1), id, 0m, null, null), default));
        Assert.Equal("iznos_neispravan", ex.Code);
    }

    [Fact]
    public async Task Zaustavljen_ponavljajuci_vazi_do_mjeseca_prije()
    {
        using var t = new TestDb();
        var v = await VrstaAsync(t, "Internet");
        var id = await new DodajPonavljajuciTrosakHandler(t.Db).Handle(new DodajPonavljajuciTrosakCommand(v, 40m, "Telekom", 2025, 3), default);

        await new ZaustaviPonavljajuciTrosakHandler(t.Db).Handle(new ZaustaviPonavljajuciTrosakCommand(id, 2026, 1), default);

        var p = await t.Db.PonavljajuciTroskovi.AsNoTracking().SingleAsync();
        Assert.Equal((2025, 12), (p.DoGodina, p.DoMjesec));
        Assert.True(p.VaziU(2025, 12));
        Assert.False(p.VaziU(2026, 1));
    }

    [Fact]
    public async Task Zaustavljen_prije_pocetka_ponavljajuci_se_brise()
    {
        using var t = new TestDb();
        var v = await VrstaAsync(t, "Internet");
        var id = await new DodajPonavljajuciTrosakHandler(t.Db).Handle(new DodajPonavljajuciTrosakCommand(v, 40m, null, 2026, 5), default);

        await new ZaustaviPonavljajuciTrosakHandler(t.Db).Handle(new ZaustaviPonavljajuciTrosakCommand(id, 2026, 5), default);

        Assert.False(await t.Db.PonavljajuciTroskovi.AnyAsync());
    }

    [Fact]
    public async Task Cijena_goriva_za_isti_datum_se_azurira()
    {
        using var t = new TestDb();
        var h = new DodajCijenuGorivaHandler(t.Db);
        await h.Handle(new DodajCijenuGorivaCommand(new DateOnly(2026, 1, 1), 2.50m, 8m), default);
        await h.Handle(new DodajCijenuGorivaCommand(new DateOnly(2026, 1, 1), 2.70m, 8.5m), default);

        var c = await t.Db.CijeneGoriva.AsNoTracking().SingleAsync();
        Assert.Equal(2.70m, c.CijenaPoLitru);
        Assert.Equal(8.5m, c.PotrosnjaL100km);
    }

    [Fact]
    public async Task Pregled_mjeseca_vraca_samo_troskove_tog_mjeseca()
    {
        using var t = new TestDb();
        var v = await VrstaAsync(t, "Marketing");
        var h = new SacuvajTrosakHandler(t.Db);
        await h.Handle(new SacuvajTrosakCommand(null, new DateOnly(2026, 10, 31), v, 10m, "Okt", null), default);
        await h.Handle(new SacuvajTrosakCommand(null, new DateOnly(2026, 11, 1), v, 20m, "Nov", null), default);

        var d = await new GetTroskoviHandler(t.Db).Handle(new GetTroskoviQuery(2026, 10), default);

        Assert.Equal("Okt", Assert.Single(d.Troskovi).Opis);
        Assert.True(Assert.Single(d.Vrste).UUpotrebi);
    }
}
