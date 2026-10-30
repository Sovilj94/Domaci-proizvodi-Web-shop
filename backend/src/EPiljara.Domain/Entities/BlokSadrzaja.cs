using EPiljara.Domain.Common;

namespace EPiljara.Domain.Entities;

/// <summary>
/// Jedan komad sadržaja sajta — slajd, kartica, pločica ili slika — koji admin uređuje. Sajt ne
/// nosi nijedan svoj tekst za ove sekcije: šta nije u bazi, ne prikazuje se.
///
/// Sekcija određuje gdje se blok prikazuje (<see cref="SekcijeSadrzaja"/>), a koja polja ima
/// smisla popuniti zavisi od sekcije (slajd ima sliku i dugme, kartica ikonu i tekst).
/// </summary>
public class BlokSadrzaja : Entity
{
    public string Sekcija { get; set; } = string.Empty;
    /// <summary>Manji ide prvi.</summary>
    public int Redoslijed { get; set; }

    /// <summary>Emoji ili naziv ikone interfejsa (npr. „Sprout").</summary>
    public string? Ikona { get; set; }
    public string? Naslov { get; set; }
    public string? Tekst { get; set; }
    public string? Dugme { get; set; }
    public string? Link { get; set; }
    public string? SlikaUrl { get; set; }
    /// <summary>Boja pozadine i teksta pločice, #RRGGBB.</summary>
    public string? Boja { get; set; }
    public string? BojaTeksta { get; set; }

    public string? NaslovEn { get; set; }
    public string? TekstEn { get; set; }
    public string? DugmeEn { get; set; }

    public bool Objavljen { get; set; } = true;
}

/// <summary>Mjesta na sajtu koja se pune iz <see cref="BlokSadrzaja"/>.</summary>
public static class SekcijeSadrzaja
{
    public const string PocetnaSlajdovi = "pocetna-slajdovi";
    public const string PocetnaKoraci = "pocetna-koraci";
    public const string PocetnaPlocice = "pocetna-plocice";
    public const string PocetnaDomacini = "pocetna-domacini";
    public const string PocetnaKorpica = "pocetna-korpica";
    public const string PocetnaUtisci = "pocetna-utisci";
    public const string SpajzRazlozi = "spajz-razlozi";
    public const string SpajzZasto = "spajz-zasto";
    public const string KorpiceMogucnosti = "korpice-mogucnosti";
    public const string PartnerPonuda = "partner-ponuda";

    public static readonly IReadOnlyList<string> Sve =
    [
        PocetnaSlajdovi, PocetnaKoraci, PocetnaPlocice, PocetnaDomacini, PocetnaKorpica, PocetnaUtisci,
        SpajzRazlozi, SpajzZasto, KorpiceMogucnosti, PartnerPonuda,
    ];
}
