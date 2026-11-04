using EPiljara.Domain.Entities;

namespace EPiljara.Application.Common.Interfaces;

/// <summary>Jedna stavka fiskalnog računa. <see cref="Ukupno"/> = round(Kolicina × JedinicnaCijena, 2) − Popust.</summary>
public sealed record FiskalnaStavka(
    string Naziv, decimal Kolicina, string Jedinica, decimal JedinicnaCijena, decimal Popust, decimal Ukupno, string PoreskaOznaka);

/// <param name="RacunId">Ključ idempotentnosti: isti račun poslat dvaput ne smije dati dva fiskalna računa.</param>
/// <param name="ReferentniBroj">Za refundaciju: broj prometnog računa koji se poništava.</param>
public sealed record FiskalniZahtjev(
    Guid RacunId,
    VrstaFiskalnogRacuna Vrsta,
    string NacinPlacanja,
    IReadOnlyList<FiskalnaStavka> Stavke,
    decimal Ukupno,
    string? ReferentniBroj,
    DateTime? ReferentnoVrijeme);

/// <param name="Trajna">Greška zbog sadržaja zahtjeva — ponovno slanje istog ne pomaže.</param>
public sealed record FiskalniOdgovor(
    bool Uspjeh,
    string? Broj = null,
    string? Brojac = null,
    DateTime? Vrijeme = null,
    string? QrSadrzaj = null,
    string? VerifikacijaUrl = null,
    string? Greska = null,
    bool Trajna = false);

/// <summary>
/// Certifikovani izdavalac fiskalnih računa (ESIR + PFR). Aplikacija sama ne fiskalizuje — samo predaje
/// podatke. Implementacija se bira konfiguracijom <c>Fiskalizacija:Dobavljac</c>.
/// </summary>
public interface IFiskalniDobavljac
{
    /// <summary>Naziv za admin ekran i za polje Dobavljac na računu.</summary>
    string Naziv { get; }
    /// <summary>True kad računi NEMAJU fiskalnu vrijednost (probni dobavljač).</summary>
    bool Probni { get; }
    Task<FiskalniOdgovor> IzdajAsync(FiskalniZahtjev zahtjev, CancellationToken ct);
}
