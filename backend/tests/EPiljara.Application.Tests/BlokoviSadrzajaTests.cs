using EPiljara.Application.Features.Admin;
using EPiljara.Application.Features.Catalog;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using FluentAssertions;

namespace EPiljara.Application.Tests;

/// <summary>Sadržaj sajta je u bazi: admin čuva cijelu sekciju, sajt čita samo objavljeno.</summary>
public class BlokoviSadrzajaTests
{
    private static BlokUnos Blok(string naslov, bool objavljen = true, string? naslovEn = null) =>
        new(null, naslov, null, null, null, null, null, null, naslovEn, null, null, objavljen);

    [Fact]
    public async Task Sekcija_se_cuva_cijela_a_redoslijed_je_redoslijed_liste()
    {
        using var t = new TestDb();
        var h = new SacuvajSekcijuHandler(t.Db);
        await h.Handle(new SacuvajSekcijuCommand(SekcijeSadrzaja.SpajzZasto, [Blok("A"), Blok("B"), Blok("C")]), default);

        var rez = await h.Handle(new SacuvajSekcijuCommand(SekcijeSadrzaja.SpajzZasto, [Blok("C"), Blok("A")]), default);

        rez.Select(b => (b.Naslov, b.Redoslijed)).Should().Equal(("C", 0), ("A", 1));
        (await new GetAdminBlokoviHandler(t.Db).Handle(new GetAdminBlokoviQuery(SekcijeSadrzaja.SpajzZasto), default))
            .Select(b => b.Naslov).Should().Equal("C", "A");
    }

    [Fact]
    public async Task Sajt_vidi_samo_objavljene_blokove_i_ne_mijesa_sekcije()
    {
        using var t = new TestDb();
        var h = new SacuvajSekcijuHandler(t.Db);
        await h.Handle(new SacuvajSekcijuCommand(SekcijeSadrzaja.SpajzZasto, [Blok("Vidljiv"), Blok("Skriven", objavljen: false)]), default);
        await h.Handle(new SacuvajSekcijuCommand(SekcijeSadrzaja.PartnerPonuda, [Blok("Ponuda")]), default);

        var javno = await new GetBlokoviSadrzajaHandler(t.Db).Handle(new GetBlokoviSadrzajaQuery(), default);

        javno.Select(b => (b.Sekcija, b.Naslov)).Should().BeEquivalentTo(new[]
        {
            (SekcijeSadrzaja.PartnerPonuda, "Ponuda"), (SekcijeSadrzaja.SpajzZasto, "Vidljiv"),
        });
    }

    [Fact]
    public async Task Prazna_baza_nema_nijedan_blok()
    {
        using var t = new TestDb();
        (await new GetBlokoviSadrzajaHandler(t.Db).Handle(new GetBlokoviSadrzajaQuery(), default)).Should().BeEmpty();
    }

    [Fact]
    public void Nepoznata_sekcija_i_blok_bez_naslova_i_slike_se_odbijaju()
    {
        var v = new SacuvajSekcijuValidator();
        v.Validate(new SacuvajSekcijuCommand("izmisljena", [Blok("A")])).IsValid.Should().BeFalse();
        v.Validate(new SacuvajSekcijuCommand(SekcijeSadrzaja.SpajzZasto, [Blok(" ")])).IsValid.Should().BeFalse();
        v.Validate(new SacuvajSekcijuCommand(SekcijeSadrzaja.SpajzZasto,
            [Blok("A") with { Boja = "zelena" }])).IsValid.Should().BeFalse();
        v.Validate(new SacuvajSekcijuCommand(SekcijeSadrzaja.SpajzZasto, [Blok("A")])).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Admin_lista_nepoznate_sekcije_je_404()
    {
        using var t = new TestDb();
        await FluentActions.Invoking(() => new GetAdminBlokoviHandler(t.Db).Handle(new GetAdminBlokoviQuery("x"), default))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Engleski_naslov_bloka_ide_u_prevode_sajta()
    {
        using var t = new TestDb();
        await new SacuvajSekcijuHandler(t.Db).Handle(new SacuvajSekcijuCommand(SekcijeSadrzaja.SpajzZasto,
            [Blok("Ista dostava", naslovEn: "Same delivery"), Blok("Skriveno", objavljen: false, naslovEn: "Hidden")]), default);

        var prevodi = await new GetPrevodiHandler(t.Db).Handle(new GetPrevodiQuery(), default);

        prevodi["Ista dostava"].Should().Be("Same delivery");
        prevodi.Should().NotContainKey("Skriveno");
    }
}
