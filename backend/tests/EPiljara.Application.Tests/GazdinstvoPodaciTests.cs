using EPiljara.Application.DTOs;
using EPiljara.Application.Features.Admin;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Tests;

/// <summary>
/// Gazdinstvo nosi podatke za otkupni list (nosilac, BPG, JMBG, adresa, račun, banka). JMBG i
/// račun su lični podaci — vide se samo u adminu, nikad u javnom ProducerDto.
/// </summary>
public class GazdinstvoPodaciTests
{
    private static ProducerInput Unos(string? jmbg = "0101990100001", string? bpg = "9000000001") => new(
        "Porodica Petković", "Nova Topola", null, null, null, null, "065 111 222",
        NosilacIme: "Petar Petković", Bpg: bpg, Jmbg: jmbg, Adresa: "Nova Topola bb",
        ZiroRacun: "5620000000000001", Banka: "Testna banka");

    [Fact]
    public async Task Cuva_podatke_za_otkupni_list()
    {
        using var t = new TestDb();

        var dto = await new CreateProducerHandler(t.Db).Handle(new CreateProducerCommand(Unos()), default);

        t.Db.ChangeTracker.Clear();
        var svi = await new GetAdminProducersHandler(t.Db).Handle(new GetAdminProducersQuery(), default);
        var p = svi.Single(x => x.Id == dto.Id);
        Assert.Equal("Petar Petković", p.NosilacIme);
        Assert.Equal("9000000001", p.Bpg);
        Assert.Equal("0101990100001", p.Jmbg);
        Assert.Equal("Nova Topola bb", p.Adresa);
        Assert.Equal("5620000000000001", p.ZiroRacun);
        Assert.Equal("Testna banka", p.Banka);
    }

    [Fact]
    public void Javni_ProducerDto_nema_jmbg_ni_racun()
    {
        var polja = typeof(ProducerDto).GetProperties().Select(p => p.Name).ToList();

        Assert.DoesNotContain("Jmbg", polja);
        Assert.DoesNotContain("ZiroRacun", polja);
        Assert.DoesNotContain("Banka", polja);
        Assert.DoesNotContain("Bpg", polja);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("01019901000AB")]
    public void Jmbg_mora_imati_13_cifara(string jmbg)
    {
        var r = new CreateProducerValidator().Validate(new CreateProducerCommand(Unos(jmbg: jmbg)));

        Assert.Contains(r.Errors, e => e.PropertyName == "Input.Jmbg");
    }

    [Fact]
    public void Bpg_mora_biti_cifre()
    {
        var r = new CreateProducerValidator().Validate(new CreateProducerCommand(Unos(bpg: "BPG-1")));

        Assert.Contains(r.Errors, e => e.PropertyName == "Input.Bpg");
    }

    [Fact]
    public async Task Prazna_polja_su_dozvoljena_i_cuvaju_se_kao_null()
    {
        using var t = new TestDb();
        var unos = Unos() with { NosilacIme = "  ", Bpg = "", Jmbg = null, Adresa = null, ZiroRacun = null, Banka = null };

        Assert.True(new CreateProducerValidator().Validate(new CreateProducerCommand(unos)).IsValid);
        var dto = await new CreateProducerHandler(t.Db).Handle(new CreateProducerCommand(unos), default);

        t.Db.ChangeTracker.Clear();
        var p = await t.Db.Producers.SingleAsync(x => x.Id == dto.Id);
        Assert.Null(p.NosilacIme);
        Assert.Null(p.Bpg);
    }

    [Fact]
    public async Task Gazdinstvo_sa_otkupnim_listom_se_ne_brise()
    {
        using var t = new TestDb();
        var p = new Producer { Name = "Jelena", Village = "Šargovac" };
        t.Db.Producers.Add(p);
        t.Db.OtkupniListovi.Add(new OtkupniList { Broj = "OL-2026-0001", ProducerId = p.Id, Godina = 2026, Sedmica = 48 });
        await t.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            new DeleteProducerHandler(t.Db).Handle(new DeleteProducerCommand(p.Id), default));

        Assert.Equal("producer_in_use", ex.Code);
    }
}
