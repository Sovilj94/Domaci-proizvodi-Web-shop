using EPiljara.Application.Features.Admin;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using FluentAssertions;

namespace EPiljara.Application.Tests;

/// <summary>Poslovna pravila su jedan red u bazi; bez njega ništa ne smije tiho postati nula.</summary>
public class PodesavanjaTests
{
    private static Podesavanja Red() => new()
    {
        Id = Podesavanja.JediniId,
        KoeficijentVrijednosti = 1.2m, MinimalnaMarza = 0.3m, BrojZamjena = 3,
        MinArtikalaMala = 5, MinArtikalaSrednja = 6, MinArtikalaVelika = 7,
        RokSatiRezerva = 24, DanIsporukeRezerva = DayOfWeek.Thursday, VrijemeIsporukeRezerva = "15:00",
        PopustNedjeljna = 0.05m, PopustMjesecna = 0.1m, PopustTriMjeseca = 0.15m,
        ObavezaMjesecna = 4, ObavezaTriMjeseca = 12, SatiUnaprijed = 48, AlarmSatiPrijeRoka = 24, MaksPauzaSedmica = 6,
        PoenaPoKM = 2, VrijednostPoenaKM = 0.1m, NagradaPreporuka = 50, PragPriKraju = 5m,
    };

    [Fact]
    public void Red_iz_baze_puni_sve_opcije()
    {
        var p = Red();

        var k = PodesavanjaMapa.Korpice(p);
        k.MinimalnaMarza.Should().Be(0.3m);
        k.RokSati.Should().Be(24);
        k.MinimalanBrojArtikala[VelicinaKorpice.Velika].Should().Be(7);
        PodesavanjaMapa.Pretplate(p).PopustTriMjeseca.Should().Be(0.15m);
        PodesavanjaMapa.Poeni(p).NagradaPreporuka.Should().Be(50);
        PodesavanjaMapa.Prodavnica(p).PragPriKraju.Should().Be(5m);
    }

    [Fact]
    public void Bez_reda_u_bazi_citanje_baca()
    {
        using var t = new TestDb();
        FluentActions.Invoking(() => new IzvorPodesavanja(t.Db).Red).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task Admin_mijenja_red_i_izvor_cita_novu_vrijednost()
    {
        using var t = new TestDb();
        t.Db.Podesavanja.Add(Red());
        await t.Db.SaveChangesAsync();

        var dto = PodesavanjaMapa.UDto(Red()) with { MinimalnaMarza = 0.4m, DanIsporukeRezerva = 5 };
        await new SacuvajPodesavanjaHandler(t.Db).Handle(new SacuvajPodesavanjaCommand(dto), default);
        t.Db.ChangeTracker.Clear();

        var red = new IzvorPodesavanja(t.Db).Red;
        red.MinimalnaMarza.Should().Be(0.4m);
        red.DanIsporukeRezerva.Should().Be(DayOfWeek.Friday);
    }

    [Fact]
    public void Validator_odbija_vrijednosti_van_opsega()
    {
        var v = new SacuvajPodesavanjaValidator();
        v.Validate(new SacuvajPodesavanjaCommand(PodesavanjaMapa.UDto(Red()))).IsValid.Should().BeTrue();
        v.Validate(new SacuvajPodesavanjaCommand(PodesavanjaMapa.UDto(Red()) with { MinimalnaMarza = 1.5m })).IsValid.Should().BeFalse();
        v.Validate(new SacuvajPodesavanjaCommand(PodesavanjaMapa.UDto(Red()) with { VrijemeIsporukeRezerva = "25:00" })).IsValid.Should().BeFalse();
    }
}
