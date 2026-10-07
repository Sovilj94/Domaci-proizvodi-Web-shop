using EPiljara.Domain.Common;
using EPiljara.Domain.Enums;

namespace EPiljara.Domain.Entities;

/// <summary>Sadržaj jedne korpice za jednu kalendarsku sedmicu.</summary>
public class SedmicniSadrzaj : Entity
{
    /// <summary>Product sa Vrsta == Korpica.</summary>
    public Guid KorpicaId { get; set; }
    public Product? Korpica { get; set; }

    public int Godina { get; set; }

    /// <summary>ISO 8601 broj sedmice, 1–53.</summary>
    public int Sedmica { get; set; }

    public StatusSadrzaja Status { get; set; } = StatusSadrzaja.Nacrt;

    /// <summary>
    /// Zamrznuta maloprodajna vrijednost sadržaja, upisana pri objavi kao
    /// Σ(Kolicina × JedinicnaCijena). Dok je Nacrt, stoji 0. Ovo je budžet
    /// kupčevog editora u P2 — ne miješati sa ciljem generisanja.
    /// </summary>
    public decimal CiljnaVrijednost { get; set; }

    public DateTime? ObjavljenU { get; set; }
    public DateTime? ZakljucanU { get; set; }

    public ICollection<StavkaSadrzaja> Stavke { get; set; } = new List<StavkaSadrzaja>();
}
