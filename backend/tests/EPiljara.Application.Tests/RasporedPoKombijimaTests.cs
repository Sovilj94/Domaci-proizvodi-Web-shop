using EPiljara.Application.Features.Admin;

namespace EPiljara.Application.Tests;

/// <summary>
/// Raspodjela naselja po kombijima: susjedna naselja (po uglu oko magacina) idu istom vozaču,
/// koristi se najmanje kombija koliko treba, nijedan ne prelazi svoj slobodan kapacitet, a u
/// kombiju naselja idu od najbližeg magacinu.
/// </summary>
public class RasporedPoKombijimaTests
{
    private static readonly (double Lat, double Lon) Magacin = (44.77, 17.19);

    private static RasporedPoKombijima.Naselje N(string naziv, double dLat, double dLon, int narudzbi) =>
        new(Guid.NewGuid(), naziv, Magacin.Lat + dLat, Magacin.Lon + dLon,
            Enumerable.Range(0, narudzbi).Select(_ => Guid.NewGuid()).ToList());

    private static RasporedPoKombijima.Vozac V(string naziv, int slobodno = 30) => new(Guid.NewGuid(), naziv, slobodno);

    [Fact]
    public void Susjedna_naselja_idu_istom_kombiju()
    {
        var istok1 = N("Istok 1", 0.00, 0.05, 12);
        var istok2 = N("Istok 2", 0.01, 0.06, 12);
        var zapad1 = N("Zapad 1", 0.00, -0.05, 12);
        var zapad2 = N("Zapad 2", -0.01, -0.06, 12);

        var r = RasporedPoKombijima.Rasporedi(Magacin, [istok1, zapad1, istok2, zapad2], [V("Ana"), V("Bojan")]);

        Assert.Equal(2, r.Kombiji.Count);
        Assert.Contains(r.Kombiji, k => k.Naselja.Select(n => n.Naziv).Order().SequenceEqual(["Istok 1", "Istok 2"]));
        Assert.Contains(r.Kombiji, k => k.Naselja.Select(n => n.Naziv).Order().SequenceEqual(["Zapad 1", "Zapad 2"]));
        Assert.Empty(r.Nerasporedjeno);
    }

    [Fact]
    public void Koristi_najmanje_kombija_koliko_treba()
    {
        var r = RasporedPoKombijima.Rasporedi(Magacin,
            [N("A", 0.01, 0.01, 10), N("B", -0.01, -0.01, 10)], [V("Ana"), V("Bojan"), V("Ceca")]);

        Assert.Single(r.Kombiji);
        Assert.Equal(20, r.Kombiji[0].Ukupno);
    }

    [Fact]
    public void Nijedan_kombi_ne_prelazi_slobodan_kapacitet()
    {
        var naselja = Enumerable.Range(0, 8).Select(i => N($"N{i}", Math.Sin(i) * 0.05, Math.Cos(i) * 0.05, 9)).ToList();
        var vozaci = new[] { V("Ana", 30), V("Bojan", 20), V("Ceca", 30) };

        var r = RasporedPoKombijima.Rasporedi(Magacin, naselja, vozaci);

        foreach (var k in r.Kombiji)
            Assert.True(k.Ukupno <= vozaci.Single(v => v.Id == k.VozacId).Slobodno, $"{k.Naziv}: {k.Ukupno}");
        Assert.Equal(72, r.Kombiji.Sum(k => k.Ukupno) + r.Nerasporedjeno.Sum(n => n.OrderIds.Count));
    }

    [Fact]
    public void Naselje_vece_od_svakog_kapaciteta_ostaje_nerasporedjeno()
    {
        var veliko = N("Veliko", 0.01, 0.01, 40);

        var r = RasporedPoKombijima.Rasporedi(Magacin, [veliko, N("Malo", 0.02, 0.02, 5)], [V("Ana")]);

        Assert.Equal(["Veliko"], r.Nerasporedjeno.Select(n => n.Naziv));
        Assert.Equal(["Malo"], r.Kombiji.Single().Naselja.Select(n => n.Naziv));
    }

    [Fact]
    public void U_kombiju_naselja_idu_od_najblizeg_magacinu()
    {
        var r = RasporedPoKombijima.Rasporedi(Magacin,
            [N("Daleko", 0.05, 0.05, 3), N("Blizu", 0.005, 0.005, 3), N("Srednje", 0.02, 0.02, 3)], [V("Ana")]);

        Assert.Equal(["Blizu", "Srednje", "Daleko"], r.Kombiji.Single().Naselja.Select(n => n.Naziv));
    }

    [Fact]
    public void Bez_vozaca_sve_ostaje_nerasporedjeno()
    {
        var r = RasporedPoKombijima.Rasporedi(Magacin, [N("A", 0.01, 0.01, 3)], [V("Ana", 0)]);

        Assert.Empty(r.Kombiji);
        Assert.Single(r.Nerasporedjeno);
    }
}
