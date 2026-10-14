using EPiljara.Domain.Common;
using EPiljara.Domain.Enums;

namespace EPiljara.Domain.Entities;

/// <summary>
/// Prijava domaćinstva koje želi da prodaje preko nas (spec P5, O9-O11). Upisuje je javni
/// obrazac bez prijave; admin je obrađuje. Nema mejla adminu — badge u navigaciji je dovoljan.
/// </summary>
public class PartnerPrijava : Entity
{
    /// <summary>Ime i prezime, 2-80.</summary>
    public string Ime { get; set; } = string.Empty;
    /// <summary>0-120.</summary>
    public string Gazdinstvo { get; set; } = string.Empty;
    /// <summary>2-80.</summary>
    public string Mjesto { get; set; } = string.Empty;
    /// <summary>6-30.</summary>
    public string Telefon { get; set; } = string.Empty;
    /// <summary>0-120; ako postoji, mora biti ispravan.</summary>
    public string Email { get; set; } = string.Empty;
    /// <summary>Šta proizvodi i koliko, 5-1000.</summary>
    public string Proizvodi { get; set; } = string.Empty;
    public StatusPrijave Status { get; set; } = StatusPrijave.Nova;
    /// <summary>0-1000.</summary>
    public string AdminNapomena { get; set; } = string.Empty;
    public DateTime Kreirana { get; set; } = DateTime.UtcNow;
}
