using EPiljara.Application.Features.Admin;
using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Tests;

/// <summary>
/// Scenario: jedna korpica u ponudi, osam artikala sa zalihom. Nabavne cijene se upisuju svima,
/// da zadatak „bez nabavne" ne zakloni ono što test provjerava.
/// </summary>
public class AdminZadaciTests
{
    private static async Task<Scenario> NapraviAsync(bool objaviSadrzaj)
    {
        var t = await Scenario.NapraviAsync(objaviSadrzaj);
        await t.Db.Products.ExecuteUpdateAsync(s => s.SetProperty(p => p.NabavnaCijena, 1m));
        t.Db.ChangeTracker.Clear();
        return t;
    }

    private static Task<EPiljara.Application.DTOs.AdminZadaciDto> Zadaci(Scenario t) =>
        new GetAdminZadaciHandler(t.Db, Options.Create(TestOpcije.Korpice())).Handle(new GetAdminZadaciQuery(), default);

    [Fact]
    public async Task Narudzba_na_cekanju_i_korpica_bez_sadrzaja_su_zadaci()
    {
        using var t = await NapraviAsync(objaviSadrzaj: false);
        await t.PosaljiObicanArtikalAsync();

        var r = await Zadaci(t);

        r.Zadaci.Should().Contain(z => z.Kljuc == "narudzbe_na_cekanju" && z.Broj == 1 && z.Nivo == "hitno");
        r.Zadaci.Should().Contain(z => z.Kljuc == "sadrzaj_korpica" && z.Broj == 1 && z.Akcija == "Generiši");
        r.Zadaci.Single(z => z.Kljuc == "narudzbe_na_cekanju").Naslov.Should().Be("1 narudžba čeka potvrdu");
        r.Meni["narudzbe"].Should().Be(1);
        r.Meni["korpice"].Should().Be(1);
        r.Zadaci.Should().NotContain(z => z.Kljuc == "bez_nabavne");
    }

    [Fact]
    public async Task Objavljen_sadrzaj_i_potvrdjena_narudzba_skidaju_zadatke()
    {
        using var t = await NapraviAsync(objaviSadrzaj: true);
        await t.PosaljiObicanArtikalAsync();
        await t.Db.SedmicniSadrzaji.ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, StatusSadrzaja.Nacrt));

        (await Zadaci(t)).Zadaci.Single(z => z.Kljuc == "sadrzaj_korpica").Akcija.Should().Be("Pregledaj i objavi");

        await t.Db.SedmicniSadrzaji.ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, StatusSadrzaja.Objavljen));
        await t.Db.Orders.Where(o => o.Status == OrderStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, OrderStatus.Confirmed));

        var r = await Zadaci(t);
        r.Zadaci.Should().NotContain(z => z.Kljuc == "sadrzaj_korpica" || z.Kljuc == "narudzbe_na_cekanju");
        r.Meni["korpice"].Should().Be(0);
    }

    [Fact]
    public async Task Artikal_u_sezoni_bez_zalihe_i_bez_nabavne_se_javlja()
    {
        using var t = await NapraviAsync(objaviSadrzaj: true);
        var krompir = await t.Db.Products.SingleAsync(p => p.Id == t.KrompirId);
        krompir.Stock = 0;
        krompir.NabavnaCijena = null;
        await t.Db.SaveChangesAsync();

        var r = await Zadaci(t);

        r.Zadaci.Single(z => z.Kljuc == "bez_zalihe").Opis.Should().Contain("Krompir");
        r.Zadaci.Single(z => z.Kljuc == "bez_nabavne").Broj.Should().Be(1);
        r.Zadaci.Single(z => z.Kljuc == "bez_nabavne").Naslov.Should().StartWith("1 artikal za korpice nema");
    }
}
