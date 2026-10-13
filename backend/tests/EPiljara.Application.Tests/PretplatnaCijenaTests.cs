using EPiljara.Application.Features.Pretplate;

namespace EPiljara.Application.Tests;

public class PretplatnaCijenaTests
{
    private static readonly PretplateOpcije Opcije = new();

    [Fact]
    public void Prva_isporuka_ide_po_punoj_cijeni()
    {
        Assert.Equal(25m, PretplatnaCijena.Izracunaj(25m, brojIsporuka: 0, Opcije));
    }

    /// <summary>
    /// Granica se lako pomjeri za jedan, a greška se vidi tek na računu: brojač 4 znači
    /// „četiri iza sebe, ovo je peta" — peta je posljednja po punoj cijeni.
    /// </summary>
    [Fact]
    public void Peta_isporuka_je_posljednja_po_punoj_cijeni()
    {
        Assert.Equal(25m, PretplatnaCijena.Izracunaj(25m, brojIsporuka: 4, Opcije));
    }

    [Fact]
    public void Sesta_isporuka_je_prva_po_nizoj_cijeni()
    {
        Assert.Equal(22.50m, PretplatnaCijena.Izracunaj(25m, brojIsporuka: 5, Opcije));
    }

    [Fact]
    public void Popust_ostaje_i_poslije_praga()
    {
        Assert.Equal(22.50m, PretplatnaCijena.Izracunaj(25m, brojIsporuka: 40, Opcije));
    }

    /// <summary>Brojač je izveden podatak; pokvaren red ne smije kupcu dati popust.</summary>
    [Fact]
    public void Negativan_brojac_se_cita_kao_nula()
    {
        Assert.Equal(25m, PretplatnaCijena.Izracunaj(25m, brojIsporuka: -3, Opcije));
    }

    [Fact]
    public void Zaokruzuje_se_na_dvije_decimale()
    {
        // 23.99 * 0.9 = 21.591
        Assert.Equal(21.59m, PretplatnaCijena.Izracunaj(23.99m, brojIsporuka: 5, Opcije));
    }

    [Fact]
    public void DoPopusta_broji_preostale_isporuke()
    {
        Assert.Equal(5, PretplatnaCijena.DoPopusta(0, Opcije));
        Assert.Equal(1, PretplatnaCijena.DoPopusta(4, Opcije));
        Assert.Equal(0, PretplatnaCijena.DoPopusta(5, Opcije));
        Assert.Equal(0, PretplatnaCijena.DoPopusta(9, Opcije));
    }
}
