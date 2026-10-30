using EPiljara.Application.Features.Pretplate;
using EPiljara.Domain.Enums;

namespace EPiljara.Application.Tests;

/// <summary>
/// R2: pretplatna cijena važi od prve isporuke i zavisi samo od plana (po uzoru na farmit) —
/// zamijenila je P3 pravilo „od šeste isporuke".
/// </summary>
public class PretplatnaCijenaTests
{
    private static readonly PretplateOpcije Opcije = TestOpcije.Pretplate();

    [Theory]
    [InlineData(PlanPretplate.Nedjeljna, 22.50)]
    [InlineData(PlanPretplate.Mjesecna, 21.25)]
    [InlineData(PlanPretplate.TriMjeseca, 20.00)]
    public void Popust_zavisi_od_plana(PlanPretplate plan, decimal ocekivano) =>
        Assert.Equal(ocekivano, PretplatnaCijena.Izracunaj(25m, plan, Opcije));

    [Fact]
    public void Zaokruzuje_se_na_dvije_decimale()
    {
        // 23.99 * 0.9 = 21.591
        Assert.Equal(21.59m, PretplatnaCijena.Izracunaj(23.99m, PlanPretplate.Nedjeljna, Opcije));
    }

    [Theory]
    [InlineData(PlanPretplate.Nedjeljna, 0)]
    [InlineData(PlanPretplate.Mjesecna, 4)]
    [InlineData(PlanPretplate.TriMjeseca, 12)]
    public void Obaveza_po_planu(PlanPretplate plan, int obaveza) =>
        Assert.Equal(obaveza, PretplatnaCijena.Obaveza(plan, Opcije));

    [Fact]
    public void Preostalo_obaveze_nikad_nije_negativno()
    {
        Assert.Equal(4, PretplatnaCijena.PreostaloObaveze(obaveza: 4, brojIsporuka: 0));
        Assert.Equal(1, PretplatnaCijena.PreostaloObaveze(obaveza: 4, brojIsporuka: 3));
        Assert.Equal(0, PretplatnaCijena.PreostaloObaveze(obaveza: 4, brojIsporuka: 4));
        Assert.Equal(0, PretplatnaCijena.PreostaloObaveze(obaveza: 4, brojIsporuka: 9));
        Assert.Equal(4, PretplatnaCijena.PreostaloObaveze(obaveza: 4, brojIsporuka: -2));
    }
}
