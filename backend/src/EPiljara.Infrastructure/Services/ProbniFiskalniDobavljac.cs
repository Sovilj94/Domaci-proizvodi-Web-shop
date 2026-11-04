using System.Globalization;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Entities;

namespace EPiljara.Infrastructure.Services;

/// <summary>
/// Probni dobavljač fiskalnih računa: ne kontaktira Poresku upravu i račun NEMA fiskalnu vrijednost.
/// Služi da cijeli tok (isporuka → račun → pregled → refundacija) radi prije ugovora sa pravim ESIR-om.
/// Broj je izveden iz Id-a računa, pa je isti zahtjev poslat dvaput isti broj (idempotentno).
/// </summary>
public sealed class ProbniFiskalniDobavljac : IFiskalniDobavljac
{
    public string Naziv => "Probni (bez fiskalne vrijednosti)";
    public bool Probni => true;

    public Task<FiskalniOdgovor> IzdajAsync(FiskalniZahtjev z, CancellationToken ct)
    {
        if (z.Stavke.Count == 0)
            return Task.FromResult(new FiskalniOdgovor(false, Greska: "Račun nema stavki.", Trajna: true));
        if (z.Stavke.Any(s => string.IsNullOrWhiteSpace(s.PoreskaOznaka)))
            return Task.FromResult(new FiskalniOdgovor(false, Greska: "Stavka nema poresku oznaku — podesi oznake u fiskalnim podešavanjima.", Trajna: true));

        var sada = DateTime.UtcNow;
        var oznaka = z.Vrsta == VrstaFiskalnogRacuna.Promet ? "PP" : "PR"; // promet / refundacija, probno
        var broj = $"PROBNI-{sada:yyyyMMdd}-{z.RacunId.ToString("N")[..8].ToUpperInvariant()}";
        var qr = string.Join(" | ",
            "PROBNI RAČUN — NEMA FISKALNU VRIJEDNOST", broj,
            z.Ukupno.ToString("0.00", CultureInfo.InvariantCulture) + " KM",
            z.ReferentniBroj is null ? null : $"ref {z.ReferentniBroj}");
        return Task.FromResult(new FiskalniOdgovor(true, Broj: broj, Brojac: $"{oznaka}", Vrijeme: sada, QrSadrzaj: qr));
    }
}
