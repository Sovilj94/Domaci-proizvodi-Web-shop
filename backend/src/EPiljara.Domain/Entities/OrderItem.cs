using EPiljara.Domain.Common;
using EPiljara.Domain.Enums;

namespace EPiljara.Domain.Entities;

/// <summary>
/// A line in an order. Product name, unit, image and unit price are SNAPSHOTS taken
/// at order time — they never change even if the product is later edited or deleted.
/// </summary>
public class OrderItem : Entity
{
    public Guid OrderId { get; set; }
    public Order? Order { get; set; }

    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public Unit Unit { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string? ImageUrl { get; set; }

    public decimal LineTotal => Math.Round(UnitPrice * Quantity, 2);

    /// <summary>
    /// Sadržaj korpice, kad je ovaj red korpica (Product.Vrsta == Korpica). Za obične
    /// artikle je prazan. Čuva se JEDNOM po redu — dvije kutije iste korpice
    /// (Quantity == 2) dijele isti sadržaj, a pakerska lista ga množi sa Quantity.
    /// </summary>
    public ICollection<StavkaKorpiceNarudzbe> KorpicaStavke { get; set; }
        = new List<StavkaKorpiceNarudzbe>();
}
