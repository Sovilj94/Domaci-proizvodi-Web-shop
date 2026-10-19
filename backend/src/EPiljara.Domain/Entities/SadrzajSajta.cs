using EPiljara.Domain.Common;

namespace EPiljara.Domain.Entities;

/// <summary>
/// Tekstovi sajta koje admin uređuje (kontakt, česta pitanja). Jedan red za cijelu piljaru.
/// Polje koje je null znači „podrazumijevano" — sajt tada prikazuje svoj ugrađeni tekst.
/// </summary>
public class SadrzajSajta : Entity
{
    public string? Telefon { get; set; }
    public string? Email { get; set; }
    public string? Adresa { get; set; }
    public string? Podrucje { get; set; }

    /// <summary>JSON niz {pitanje, odgovor}; odgovor smije nositi {rok}, {isporuka}, {popustNedjeljna}, {popustTriMjeseca}.</summary>
    public string? PitanjaJson { get; set; }

    public DateTime Izmijenjeno { get; set; } = DateTime.UtcNow;
}
