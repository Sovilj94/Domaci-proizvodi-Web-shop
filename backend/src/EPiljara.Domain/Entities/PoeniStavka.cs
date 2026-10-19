using EPiljara.Domain.Common;
using EPiljara.Domain.Enums;

namespace EPiljara.Domain.Entities;

/// <summary>
/// Jedan red knjige poena. Stanje kupca je ZBIR <see cref="Iznos"/> svih njegovih stavki —
/// nema kolone sa stanjem koja bi se mogla razići od istorije. Zato se stavka nikad ne
/// mijenja ni briše: ispravka je nova stavka (<see cref="VrstaPoena.Korekcija"/>).
/// Iznos je negativan kad se poeni troše.
/// </summary>
public class PoeniStavka : Entity
{
    public Guid UserId { get; set; }
    public int Iznos { get; set; }
    public VrstaPoena Vrsta { get; set; }
    public Guid? OrderId { get; set; }

    /// <summary>
    /// Ko je došao preko preporuke. Samo podatak, bez FK — brisanje preporučenog
    /// ne smije izbrisati tuđu već zarađenu nagradu.
    /// </summary>
    public Guid? PreporuceniId { get; set; }

    /// <summary>Razlog ručne korekcije (B4) — vidi ga i kupac u istoriji. Samo za <see cref="VrstaPoena.Korekcija"/>.</summary>
    public string? Razlog { get; set; }

    /// <summary>Admin koji je unio korekciju (B4). Samo podatak, bez FK — kao <see cref="PreporuceniId"/>.</summary>
    public Guid? UnioId { get; set; }

    public DateTime Kreirana { get; set; } = DateTime.UtcNow;
}
