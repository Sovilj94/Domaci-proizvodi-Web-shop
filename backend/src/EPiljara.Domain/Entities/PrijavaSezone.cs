using EPiljara.Domain.Common;
using EPiljara.Domain.Enums;

namespace EPiljara.Domain.Entities;

/// <summary>
/// „Javi mi kad stigne voće" (spec 2026-11-26, odluka 5): e-mail ostavljen na stranici korpica van
/// sezone. Dobija jedan mejl kad se objavi prva korpica te vrste u novoj sezoni; tada se upisuje
/// <see cref="ObavijestenAt"/> i red ostaje kao evidencija. Za sljedeću sezonu kupac se prijavljuje ponovo.
/// </summary>
public class PrijavaSezone : Entity
{
    /// <summary>Normalizovan: bez razmaka, mala slova.</summary>
    public string Email { get; set; } = string.Empty;
    public TipKorpice TipKorpice { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ObavijestenAt { get; set; }
}
