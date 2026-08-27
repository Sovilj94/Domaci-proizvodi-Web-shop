using EPiljara.Domain.Common;
using EPiljara.Domain.Enums;

namespace EPiljara.Domain.Entities;

public class Order : Entity
{
    public string Code { get; set; } = string.Empty; // e.g. "EP-2607-0042"

    /// <summary>Set when a registered customer placed the order; null for guest checkout.</summary>
    public Guid? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public string? CustomerEmail { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public decimal Subtotal { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal TotalAmount { get; set; }

    public Guid DeliverySlotId { get; set; }
    public DeliverySlot? DeliverySlot { get; set; }

    public Guid DeliveryZoneId { get; set; }
    public DeliveryZone? DeliveryZone { get; set; }

    public string DeliveryAddress { get; set; } = string.Empty;
    public string? Note { get; set; }

    /// <summary>
    /// Geografska širina i dužina adrese dostave. null kad geokodiranje nije uspjelo —
    /// takva narudžba se i dalje može dodijeliti ruti, samo nema pin na mapi vozača.
    /// </summary>
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Trenutak (UTC) kad je vozač zatvorio stanicu — status prešao u Delivered ili
    /// FailedDelivery. null za sve narudžbe od prije uvođenja ovog polja i za one koje
    /// još nisu isporučene; to je normalno stanje, ne greška.
    /// </summary>
    public DateTime? ZatvorenaU { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();

    /// <summary>Allowed forward transitions for the order status state machine.</summary>
    public static readonly IReadOnlyDictionary<OrderStatus, OrderStatus[]> Transitions =
        new Dictionary<OrderStatus, OrderStatus[]>
        {
            [OrderStatus.Pending] = [OrderStatus.Confirmed, OrderStatus.Cancelled],
            [OrderStatus.Confirmed] = [OrderStatus.Packing, OrderStatus.Cancelled],
            [OrderStatus.Packing] = [OrderStatus.OutForDelivery],
            [OrderStatus.OutForDelivery] = [OrderStatus.Delivered, OrderStatus.FailedDelivery],
            [OrderStatus.Delivered] = [],
            [OrderStatus.FailedDelivery] = [OrderStatus.OutForDelivery, OrderStatus.Cancelled],
            [OrderStatus.Cancelled] = [],
        };

    public bool CanTransitionTo(OrderStatus next) =>
        Status == next || Transitions[Status].Contains(next);

    public void TransitionTo(OrderStatus next)
    {
        if (!CanTransitionTo(next))
            throw new DomainException(
                $"Prelaz statusa {Status} → {next} nije dozvoljen.", "invalid_transition");
        Status = next;
    }
}
