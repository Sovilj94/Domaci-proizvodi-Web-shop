using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;

namespace EPiljara.Application.Tests;

public class PretplataModelTests
{
    private static Pretplata Nova(StatusPretplate status = StatusPretplate.Aktivna) =>
        new() { Status = status, DeliveryAddress = "Kralja Petra 1, Banja Luka" };

    [Theory]
    [InlineData(StatusPretplate.Aktivna, StatusPretplate.Pauzirana)]
    [InlineData(StatusPretplate.Aktivna, StatusPretplate.Otkazana)]
    [InlineData(StatusPretplate.Pauzirana, StatusPretplate.Aktivna)]
    [InlineData(StatusPretplate.Pauzirana, StatusPretplate.Otkazana)]
    public void Dozvoljeni_prelazi_prolaze(StatusPretplate iz, StatusPretplate u)
    {
        var p = Nova(iz);
        p.Predji(u);
        Assert.Equal(u, p.Status);
    }

    /// <summary>Otkazana je završna: istorija narudžbi visi o njoj i ne smije se oživjeti.</summary>
    [Theory]
    [InlineData(StatusPretplate.Aktivna)]
    [InlineData(StatusPretplate.Pauzirana)]
    public void Iz_otkazane_se_ne_izlazi(StatusPretplate u)
    {
        var p = Nova(StatusPretplate.Otkazana);
        var ex = Assert.Throws<ConflictException>(() => p.Predji(u));
        Assert.Equal("nedozvoljen_prelaz_pretplate", ex.Code);
        Assert.Equal(StatusPretplate.Otkazana, p.Status);
    }

    [Fact]
    public void Otkaz_upisuje_vrijeme_i_brise_pauzu()
    {
        var p = Nova(StatusPretplate.Pauzirana);
        p.PauzaDoGodina = 2026;
        p.PauzaDoSedmica = 44;

        p.Predji(StatusPretplate.Otkazana);

        Assert.NotNull(p.OtkazanaU);
        Assert.Null(p.PauzaDoGodina);
        Assert.Null(p.PauzaDoSedmica);
    }

    /// <summary>
    /// Nastavak mora obrisati pauzu, inače bi posao i dalje preskakao sedmice iako je
    /// pretplata ponovo aktivna — tiho otkazana pretplata koja izgleda aktivno.
    /// </summary>
    [Fact]
    public void Nastavak_brise_pauzu()
    {
        var p = Nova(StatusPretplate.Pauzirana);
        p.PauzaDoGodina = 2026;
        p.PauzaDoSedmica = 44;

        p.Predji(StatusPretplate.Aktivna);

        Assert.Null(p.PauzaDoGodina);
        Assert.Null(p.PauzaDoSedmica);
    }

    [Fact]
    public void Prelaz_u_isti_status_nije_dozvoljen()
    {
        var p = Nova();
        Assert.Throws<ConflictException>(() => p.Predji(StatusPretplate.Aktivna));
    }
}
