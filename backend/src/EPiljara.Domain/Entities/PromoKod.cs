using EPiljara.Domain.Common;
using EPiljara.Domain.Enums;

namespace EPiljara.Domain.Entities;

/// <summary>
/// Kod za popust na robu (nikad na dostavu). Pravila primjene stoje samo u
/// <c>PromoKodPravila</c>; ovdje su samo podaci. Kod se poslije kreiranja ne mijenja —
/// narudžbe ga pamte kao snimak teksta.
/// </summary>
public class PromoKod : Entity
{
    public string Kod { get; set; } = string.Empty;
    public string Opis { get; set; } = string.Empty;
    public TipPopusta Tip { get; set; }
    public decimal Vrijednost { get; set; }
    public decimal? MinIznos { get; set; }
    public DateOnly? VaziOd { get; set; }
    public DateOnly? VaziDo { get; set; }
    public int? MaxKoristenja { get; set; }
    public bool JednomPoKupcu { get; set; }
    public bool SamoNoviKupci { get; set; }
    public bool Aktivan { get; set; } = true;
    public DateTime Kreiran { get; set; } = DateTime.UtcNow;
}
