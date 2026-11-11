using EPiljara.Application.Features.Naselja;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;

namespace EPiljara.Application.Tests;

public class OdredjivanjeNaseljaTests
{
    internal static DeliveryZone Naselje(string ime, double lon1, double lat1, double lon2, double lat2, bool aktivno = true) =>
        new()
        {
            Name = ime,
            IsActive = aktivno,
            Granica = GranicaNaseljaTests.Kvadrat(lon1, lat1, lon2, lat2),
            CentarLat = (lat1 + lat2) / 2,
            CentarLon = (lon1 + lon2) / 2,
        };

    private static async Task<TestDb> BazaAsync(string? telefon = "051 123 456", string? mejl = "info@epiljara.ba")
    {
        var t = new TestDb();
        t.Db.DeliveryZones.AddRange(
            Naselje("Borik", 17.20, 44.76, 17.21, 44.78),
            Naselje("Ada", 17.21, 44.76, 17.22, 44.78),
            Naselje("Motike", 17.10, 44.80, 17.12, 44.82, aktivno: false),
            new DeliveryZone { Name = "Stara zona", IsActive = true });
        if (telefon is not null || mejl is not null)
            t.Db.SadrzajSajta.Add(new SadrzajSajta { Telefon = telefon, Email = mejl });
        await t.Db.SaveChangesAsync();
        return t;
    }

    [Fact]
    public async Task Tacka_u_aktivnom_naselju()
    {
        using var t = await BazaAsync();

        var z = await OdredjivanjeNaselja.OdrediAsync(t.Db, 44.77, 17.205, default);

        Assert.Equal("Borik", z.Name);
    }

    [Fact]
    public async Task Tacka_u_neaktivnom_daje_gresku()
    {
        using var t = await BazaAsync();

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => OdredjivanjeNaselja.OdrediAsync(t.Db, 44.81, 17.11, default));

        Assert.Equal("van_podrucja_dostave", ex.Code);
    }

    [Fact]
    public async Task Tacka_van_svih_daje_gresku_sa_kontaktom()
    {
        using var t = await BazaAsync();

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => OdredjivanjeNaselja.OdrediAsync(t.Db, 44.60, 17.05, default));

        Assert.Equal("van_podrucja_dostave", ex.Code);
        Assert.Equal(
            "Na ovu adresu trenutno ne vršimo dostavu. Ako misliš da je greška, javi se administratoru: 051 123 456, info@epiljara.ba.",
            ex.Message);
    }

    [Fact]
    public async Task Bez_kontakta_poruka_bez_zareza()
    {
        using var t = await BazaAsync(telefon: null, mejl: null);

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => OdredjivanjeNaselja.OdrediAsync(t.Db, 44.60, 17.05, default));

        Assert.Equal(
            "Na ovu adresu trenutno ne vršimo dostavu. Ako misliš da je greška, javi se administratoru.",
            ex.Message);
    }

    [Fact]
    public async Task Samo_mejl_bez_telefona()
    {
        using var t = await BazaAsync(telefon: null);

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => OdredjivanjeNaselja.OdrediAsync(t.Db, 44.60, 17.05, default));

        Assert.EndsWith("administratoru: info@epiljara.ba.", ex.Message);
    }

    [Fact]
    public async Task Bez_tacke_daje_gresku()
    {
        using var t = await BazaAsync();

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => OdredjivanjeNaselja.OdrediAsync(t.Db, null, null, default));

        Assert.Equal("van_podrucja_dostave", ex.Code);
    }

    [Fact]
    public async Task Tacka_na_zajednickoj_ivici_daje_prvo_po_imenu()
    {
        using var t = await BazaAsync();

        // Borik i Ada dijele ivicu na 17.21 — „Ada" je prva po imenu.
        var z = await OdredjivanjeNaselja.OdrediAsync(t.Db, 44.77, 17.21, default);

        Assert.Equal("Ada", z.Name);
    }

    [Fact]
    public async Task Zona_bez_granice_se_ne_pogadja()
    {
        using var t = await BazaAsync();

        Assert.Null(await OdredjivanjeNaselja.NadjiAsync(t.Db, 0, 0, default));
    }
}
