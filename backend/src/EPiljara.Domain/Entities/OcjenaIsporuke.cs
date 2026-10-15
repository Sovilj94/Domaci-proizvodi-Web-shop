using EPiljara.Domain.Common;

namespace EPiljara.Domain.Entities;

/// <summary>Ocjena isporuke: jedna po narudžbi, konačna (spec P6a, O3).</summary>
public class OcjenaIsporuke : Entity
{
    public Guid OrderId { get; set; }
    public Order? Order { get; set; }
    public int Ocjena { get; set; }
    public string Komentar { get; set; } = string.Empty;
    public bool DozvolaZaObjavu { get; set; }
    public DateTime Kreirana { get; set; } = DateTime.UtcNow;
}
