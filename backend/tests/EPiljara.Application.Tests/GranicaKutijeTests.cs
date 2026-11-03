using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Common;

namespace EPiljara.Application.Tests;

/// <summary>
/// Admin ne smije gurnuti korpicu preko cilja: dodaje se samo ono što se oslobodi vađenjem.
/// Sadržaj scenarija: 2 kg krompira (2,00) + 1 kg paprike (3,00) = 7,00 KM = cilj.
/// </summary>
public class GranicaKutijeTests
{
    [Fact]
    public async Task Admin_ne_moze_samo_dodati_artikal_u_punu_korpicu_kupca()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true);
        var o = await t.PosaljiKorpicuAsync(kolicina: 1m);
        var stavka = await t.OrderItemIdAsync(o.Id);

        var ex = await Assert.ThrowsAsync<DomainException>(() => new IzmijeniKorpicuNarudzbeHandler(t.Db).Handle(
            new IzmijeniKorpicuNarudzbeCommand(stavka,
                [new StavkaUnos(t.KrompirId, 2m), new StavkaUnos(t.PaprikaId, 1m), new StavkaUnos(t.Jeftin264Id, 1m)]), default));

        Assert.Equal("korpica_preko_cilja", ex.Code);
    }

    [Fact]
    public async Task Admin_moze_dodati_kad_prvo_izvadi_nesto_drugo()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true);
        var o = await t.PosaljiKorpicuAsync(kolicina: 1m);
        var stavka = await t.OrderItemIdAsync(o.Id);

        // Paprika (3,00) van, artikal od 2,64 unutra: 4,00 + 2,64 = 6,64 ≤ 7,00.
        var dto = await new IzmijeniKorpicuNarudzbeHandler(t.Db).Handle(
            new IzmijeniKorpicuNarudzbeCommand(stavka, [new StavkaUnos(t.KrompirId, 2m), new StavkaUnos(t.Jeftin264Id, 1m)]), default);

        Assert.Equal(6.64m, dto.Vrijednost);
    }

    [Fact]
    public async Task Admin_ne_moze_povecati_kolicinu_preko_cilja()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true);
        var o = await t.PosaljiKorpicuAsync(kolicina: 1m);
        var stavka = await t.OrderItemIdAsync(o.Id);

        var ex = await Assert.ThrowsAsync<DomainException>(() => new IzmijeniKorpicuNarudzbeHandler(t.Db).Handle(
            new IzmijeniKorpicuNarudzbeCommand(stavka, [new StavkaUnos(t.KrompirId, 3m), new StavkaUnos(t.PaprikaId, 1m)]), default));

        Assert.Equal("korpica_preko_cilja", ex.Code);
    }

    [Theory]
    [InlineData(7.00, 7.00, 7.00)]   // tačno cilj
    [InlineData(7.00, 6.00, 7.00)]   // smanjenje
    [InlineData(8.00, 7.50, 7.00)]   // već preko cilja, ali se smanjuje
    [InlineData(8.00, 8.00, 7.00)]   // već preko cilja, ostaje isto
    [InlineData(5.00, 9.00, 0.00)]   // cilj nepoznat — bez granice
    public void Dozvoljeno(double stara, double nova, double cilj) =>
        GranicaKutije.Provjeri((decimal)stara, (decimal)nova, (decimal)cilj);

    [Theory]
    [InlineData(7.00, 7.10, 7.00)]
    [InlineData(8.00, 8.10, 7.00)]   // preko cilja ne smije ni još malo da poraste
    public void Odbijeno(double stara, double nova, double cilj) =>
        Assert.Equal("korpica_preko_cilja",
            Assert.Throws<DomainException>(() => GranicaKutije.Provjeri((decimal)stara, (decimal)nova, (decimal)cilj)).Code);
}
