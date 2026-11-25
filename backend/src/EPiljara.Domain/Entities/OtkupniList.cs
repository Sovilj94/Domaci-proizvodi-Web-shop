using EPiljara.Domain.Common;
using EPiljara.Domain.Enums;

namespace EPiljara.Domain.Entities;

/// <summary>
/// Otkupni list: dokument koji gazdinstvo potpisuje pri predaji robe. Jedan po gazdinstvu i
/// sedmici isporuke. Stavke su SNIMAK (naziv, jedinica, cijena) — kasnija izmjena proizvoda ne
/// smije promijeniti potpisan dokument, isto kao što OrderItem snima cijenu.
/// </summary>
public class OtkupniList : Entity
{
    /// <summary>Jedinstven broj, „OL-2026-0001" — brojač po godini.</summary>
    public string Broj { get; set; } = string.Empty;
    public Guid ProducerId { get; set; }
    public Producer? Producer { get; set; }
    /// <summary>ISO godina i sedmica ISPORUKE (kao Order.Godina/Sedmica).</summary>
    public int Godina { get; set; }
    public int Sedmica { get; set; }
    /// <summary>Dan nabavke (UTC).</summary>
    public DateTime Datum { get; set; }
    /// <summary>Admin je poslije zaključavanja mijenjao narudžbe te sedmice — količine možda više ne važe.</summary>
    public bool Zastario { get; set; }
    public string? Napomena { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<OtkupniListStavka> Stavke { get; set; } = new List<OtkupniListStavka>();
}

public class OtkupniListStavka : Entity
{
    public Guid OtkupniListId { get; set; }
    public Guid ProductId { get; set; }
    public string Naziv { get; set; } = string.Empty;
    public Unit Jedinica { get; set; } = Unit.Kom;
    public decimal Naruceno { get; set; }
    /// <summary>Otkupna (nabavna) cijena po jedinici; null kad proizvod nema nabavnu cijenu.</summary>
    public decimal? Cijena { get; set; }
    /// <summary>Stvarno primljeno — unosi admin poslije nabavke; null = još nije uneseno.</summary>
    public decimal? Primljeno { get; set; }
    public int Redoslijed { get; set; }
}
