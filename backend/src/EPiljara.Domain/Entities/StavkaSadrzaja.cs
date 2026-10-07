using EPiljara.Domain.Common;

namespace EPiljara.Domain.Entities;

public class StavkaSadrzaja : Entity
{
    public Guid SedmicniSadrzajId { get; set; }
    public SedmicniSadrzaj? SedmicniSadrzaj { get; set; }

    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>Količina u jedinici artikla (Product.Unit).</summary>
    public decimal Kolicina { get; set; }

    /// <summary>
    /// Snapshot Product.Price. Upisuje se kad stavka nastane i ponovo pri objavi,
    /// da kasnija izmjena cijene artikla ne pomjeri vrijednost objavljenog sadržaja.
    /// Isti razlog kao kod OrderItem.
    /// </summary>
    public decimal JedinicnaCijena { get; set; }
}
