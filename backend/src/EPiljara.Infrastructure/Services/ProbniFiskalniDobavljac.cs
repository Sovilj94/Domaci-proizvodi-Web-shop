using System.Globalization;
using System.Text;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Entities;

namespace EPiljara.Infrastructure.Services;

/// <summary>
/// Probni dobavljač fiskalnih računa: ne kontaktira Poresku upravu i račun NEMA fiskalnu vrijednost.
/// Služi da cijeli tok (isporuka → račun → štampa → storno/refundacija) radi prije ugovora sa pravim
/// ESIR-om. Broj je izveden iz Id-a računa, pa je isti zahtjev poslat dvaput isti broj (idempotentno).
/// Žurnal liči na pravi račun, ali nosi jasnu oznaku da nije fiskalni.
/// </summary>
public sealed class ProbniFiskalniDobavljac : IFiskalniDobavljac
{
    public string Naziv => "Probni (bez fiskalne vrijednosti)";
    public bool Probni => true;

    private static readonly CultureInfo Kultura = CultureInfo.InvariantCulture;
    private const int Sirina = 32; // 58 mm termalni štampač, osnovni font

    public Task<FiskalniOdgovor> IzdajAsync(FiskalniZahtjev z, CancellationToken ct)
    {
        if (z.Stavke.Count == 0)
            return Task.FromResult(new FiskalniOdgovor(false, Greska: "Račun nema stavki.", Trajna: true));
        if (z.Stavke.Any(s => string.IsNullOrWhiteSpace(s.PoreskaOznaka)))
            return Task.FromResult(new FiskalniOdgovor(false, Greska: "Stavka nema poresku oznaku — podesi oznake u fiskalnim podešavanjima.", Trajna: true));

        var sada = DateTime.UtcNow;
        var oznaka = z.Vrsta switch
        {
            VrstaFiskalnogRacuna.Promet => "PP",
            VrstaFiskalnogRacuna.Storno => "PN", // promet, poništavanje
            _ => "PR",
        };
        var broj = $"PROBNI-{sada:yyyyMMdd}-{z.RacunId.ToString("N")[..8].ToUpperInvariant()}";
        var qr = string.Join(" | ", new[]
        {
            "PROBNI RAČUN — NEMA FISKALNU VRIJEDNOST", broj,
            z.Ukupno.ToString("0.00", Kultura) + " KM",
            z.ReferentniBroj is null ? null : $"ref {z.ReferentniBroj}",
        }.Where(d => d is not null));
        return Task.FromResult(new FiskalniOdgovor(true, Broj: broj, Brojac: oznaka, Vrijeme: sada, QrSadrzaj: qr,
            Zurnal: Zurnal(z, broj, sada)));
    }

    internal static string Zurnal(FiskalniZahtjev z, string broj, DateTime vrijeme)
    {
        var crta = new string('-', Sirina);
        var sb = new StringBuilder();
        sb.AppendLine(Sredina("PROBNI RACUN"));
        sb.AppendLine(Sredina("NEMA FISKALNU VRIJEDNOST"));
        sb.AppendLine(crta);
        sb.AppendLine(Sredina(z.Vrsta switch
        {
            VrstaFiskalnogRacuna.Promet => "PROMET - PRODAJA",
            VrstaFiskalnogRacuna.Storno => "PROMET - PONISTAVANJE",
            _ => "PROMET - REFUNDACIJA",
        }));
        sb.AppendLine(crta);
        foreach (var s in z.Stavke)
        {
            sb.AppendLine($"{s.Naziv} ({s.PoreskaOznaka})");
            Red(sb, $"  {s.Kolicina.ToString("0.###", Kultura)} x {s.JedinicnaCijena.ToString("0.00", Kultura)}",
                Iznos(z.Vrsta, s.Ukupno));
        }
        sb.AppendLine(crta);
        Red(sb, "UKUPNO", Iznos(z.Vrsta, z.Ukupno));
        Red(sb, z.NacinPlacanja, Iznos(z.Vrsta, z.Ukupno));
        sb.AppendLine(crta);
        if (z.ReferentniBroj is not null) sb.AppendLine($"Ref: {z.ReferentniBroj}");
        sb.AppendLine($"Broj: {broj}");
        sb.AppendLine($"Vrijeme: {vrijeme.ToLocalTime().ToString("dd.MM.yyyy. HH:mm:ss", Kultura)}");
        return sb.ToString();
    }

    private static string Iznos(VrstaFiskalnogRacuna v, decimal i) =>
        (v == VrstaFiskalnogRacuna.Promet ? "" : "-") + i.ToString("0.00", Kultura);

    private static void Red(StringBuilder sb, string lijevo, string desno)
    {
        var razmak = Math.Max(1, Sirina - lijevo.Length - desno.Length);
        sb.AppendLine(lijevo + new string(' ', razmak) + desno);
    }

    private static string Sredina(string t) => t.PadLeft((Sirina + t.Length) / 2);
}
