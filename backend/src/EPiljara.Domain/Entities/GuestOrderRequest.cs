using EPiljara.Domain.Common;
using EPiljara.Domain.Enums;

namespace EPiljara.Domain.Entities;

/// <summary>
/// Narudžba gosta koja još nije potvrđena. Ne postaje <see cref="Order"/> dok kupac
/// ne klikne link iz mejla, a to mora unutar <see cref="TrajanjeMinuta"/> minuta.
/// Poslije isteka se briše bez traga — zato ovdje stoje snimljene cijene, a ne
/// veza na aktuelni cjenovnik.
/// </summary>
public class GuestOrderRequest : Entity
{
    public const int TrajanjeMinuta = 15;

    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string CustomerName { get; set; } = string.Empty;
    /// <summary>Obavezan — bez njega nema gdje poslati link za potvrdu.</summary>
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;

    public Guid DeliveryZoneId { get; set; }
    public DeliveryZone? DeliveryZone { get; set; }
    public Guid DeliverySlotId { get; set; }
    public DeliverySlot? DeliverySlot { get; set; }

    public string DeliveryAddress { get; set; } = string.Empty;
    public string? Note { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public decimal Subtotal { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Snimak promo koda iz trenutka slanja forme — prikaz ne zavisi od tabele kodova.
    /// <c>Popust</c> je već uračunat u <c>TotalAmount</c>.
    /// </summary>
    public Guid? PromoKodId { get; set; }
    public string? PromoKod { get; set; }
    public decimal Popust { get; set; }

    public ICollection<GuestOrderRequestItem> Items { get; set; } = new List<GuestOrderRequestItem>();

    public bool Istekao(DateTime sada) => ExpiresAt <= sada;
}

/// <summary>
/// Stavka zahtjeva sa snimljenom cijenom, nazivom i jedinicom — isto kao
/// <see cref="OrderItem"/>, samo snimljeno u trenutku slanja forme umjesto
/// u trenutku nastanka narudžbe. Kupac plaća ono što je vidio.
/// </summary>
public class GuestOrderRequestItem : Entity
{
    public Guid RequestId { get; set; }
    public GuestOrderRequest? Request { get; set; }

    /// <summary>Bez stranog ključa namjerno — brisanje proizvoda ne smije oboriti zahtjev prije isteka.</summary>
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public Unit Unit { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Quantity { get; set; }
    public string? ImageUrl { get; set; }
}
