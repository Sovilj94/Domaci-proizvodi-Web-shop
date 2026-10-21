using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.Features.Admin;
using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Enums;
using EPiljara.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Tests;

public class AdminZadaciTests
{
    private sealed class LazniHasher : IPasswordHasher
    {
        public string Hash(string password) => password;
        public bool Verify(string password, string hash) => password == hash;
    }

    private static async Task<TestDb> Seedovana()
    {
        var t = new TestDb();
        await DbSeeder.SeedAsync(t.Db, new LazniHasher());
        return t;
    }

    private static Task<EPiljara.Application.DTOs.AdminZadaciDto> Zadaci(TestDb t) =>
        new GetAdminZadaciHandler(t.Db, Options.Create(new KorpiceOpcije())).Handle(new GetAdminZadaciQuery(), default);

    [Fact]
    public async Task Seedovana_baza_ima_narudzbu_na_cekanju_i_neobjavljene_korpice()
    {
        using var t = await Seedovana();

        var r = await Zadaci(t);

        r.Zadaci.Should().Contain(z => z.Kljuc == "narudzbe_na_cekanju" && z.Broj == 1 && z.Nivo == "hitno");
        r.Zadaci.Should().Contain(z => z.Kljuc == "sadrzaj_korpica" && z.Broj == 6 && z.Akcija == "Generiši");
        r.Zadaci.Single(z => z.Kljuc == "narudzbe_na_cekanju").Naslov.Should().Be("1 narudžba čeka potvrdu");
        r.Meni["narudzbe"].Should().Be(1);
        r.Meni["korpice"].Should().Be(6);
        r.Zadaci.Should().NotContain(z => z.Kljuc == "bez_nabavne", "seed daje nabavnu svim artiklima");
    }

    [Fact]
    public async Task Objavljen_sadrzaj_i_potvrdjena_narudzba_skidaju_zadatke()
    {
        using var t = await Seedovana();
        var r0 = await Zadaci(t);
        await new GenerisiSedmicniSadrzajHandler(t.Db, Options.Create(new KorpiceOpcije()))
            .Handle(new GenerisiSedmicniSadrzajCommand(r0.Godina, r0.Sedmica, null), default);

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
        using var t = await Seedovana();
        var krompir = await t.Db.Products.SingleAsync(p => p.Name == "Domaći krompir");
        krompir.Stock = 0;
        krompir.NabavnaCijena = null;
        await t.Db.SaveChangesAsync();

        var r = await Zadaci(t);

        r.Zadaci.Single(z => z.Kljuc == "bez_zalihe").Opis.Should().Contain("Domaći krompir");
        r.Zadaci.Single(z => z.Kljuc == "bez_nabavne").Broj.Should().Be(1);
        r.Zadaci.Single(z => z.Kljuc == "bez_nabavne").Naslov.Should().StartWith("1 artikal za korpice nema");
    }
}
