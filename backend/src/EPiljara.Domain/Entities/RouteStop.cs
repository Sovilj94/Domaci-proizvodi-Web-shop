using EPiljara.Domain.Common;

namespace EPiljara.Domain.Entities;

/// <summary>One delivery stop within a route, positioned by <see cref="Sequence"/>.</summary>
public class RouteStop : Entity
{
    public Guid RouteId { get; set; }
    public DeliveryRoute? Route { get; set; }

    public Guid OrderId { get; set; }
    public Order? Order { get; set; }

    public int Sequence { get; set; }
}
