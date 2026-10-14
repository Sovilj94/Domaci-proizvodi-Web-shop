using EPiljara.Domain.Common;

namespace EPiljara.Domain.Entities;

/// <summary>
/// Utisak kupca za početnu stranicu. Unosi ga admin (spec P4, O1); nema veze na kupca ni
/// narudžbu — ta veza dolazi sa ocjenom isporuke u P6 kao nullable kolona.
/// </summary>
public class Utisak : Entity
{
    public string Ime { get; set; } = string.Empty;
    /// <summary>Naselje kupca („Borik"); prazan string kad ga admin ne unese.</summary>
    public string Mjesto { get; set; } = string.Empty;
    public string Tekst { get; set; } = string.Empty;
    public bool Objavljen { get; set; }
    /// <summary>Manji ide prvi. Kod jednakih odlučuje <see cref="Kreiran"/>.</summary>
    public int Redoslijed { get; set; }
    public DateTime Kreiran { get; set; } = DateTime.UtcNow;
}
