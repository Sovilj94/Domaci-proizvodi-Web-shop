using EPiljara.Application.Features.Admin.Poslovanje;

namespace EPiljara.Application.Tests;

/// <summary>
/// Plan poslovanja: prosječna narudžba (po današnjim cijenama i nabavnim) × broj narudžbi, minus
/// varijabilni (nabavno, ambalaža, gorivo po narudžbi) i fiksni troškovi mjeseca.
/// Prosječna narudžba: korpice 30 KM (nabavno 19, kutija 1), Špajz 10 KM (nabavno 6, ambalaža 0,50),
/// dostava 3 KM, popusti 1 KM → neto 42; varijabilno 19 + 1 + 6 + 0,5 + gorivo 2 = 28,50 → doprinos 13,50.
/// Fiksni 1.350 KM → prag 100 narudžbi.
/// </summary>
public class PlanPoslovanjaTests
{
    private static readonly PlanNarudzba Prosjek = new(
        Korpice: 30m, NabavnoKorpica: 19m, KutijeKorpica: 1m,
        Spajz: 10m, NabavnoSpajz: 6m, AmbalazaSpajz: 0.5m,
        Dostava: 3m, Popusti: 1m, Gorivo: 2m);

    [Fact]
    public void Doprinos_po_narudzbi_je_neto_manje_varijabilni_troskovi()
    {
        Assert.Equal(42m, Prosjek.Neto);
        Assert.Equal(28.5m, Prosjek.Varijabilno);
        Assert.Equal(13.5m, Prosjek.Doprinos);
    }

    [Fact]
    public void Mjesec_sa_200_narudzbi_i_prag_isplativosti()
    {
        var s = PlanRacun.Scenarij(Prosjek, 200, fiksni: 1350m);

        Assert.Equal(8400m, s.Neto);
        Assert.Equal(5700m, s.Varijabilno);
        Assert.Equal(1350m, s.Fiksni);
        Assert.Equal(1350m, s.Dobit);           // 200 × 13,50 − 1.350
        Assert.Equal(0.1607m, s.MarzaDobiti);   // 1.350 / 8.400
        Assert.Equal(100, PlanRacun.Prag(Prosjek, 1350m));
    }

    [Fact]
    public void Bez_doprinosa_nema_praga()
    {
        var gubitas = Prosjek with { Gorivo = 20m };
        Assert.Null(PlanRacun.Prag(gubitas, 1350m));
    }
}
