using EPiljara.Domain.Common;
using EPiljara.Domain.Enums;

namespace EPiljara.Domain.Entities;

/// <summary>A driver's delivery run for a given day, composed of ordered stops.</summary>
public class DeliveryRoute : Entity
{
    public DateOnly Date { get; set; }

    public Guid DriverId { get; set; }
    public Driver? Driver { get; set; }

    public RouteStatus Status { get; set; } = RouteStatus.Planned;

    public ICollection<RouteStop> Stops { get; set; } = new List<RouteStop>();
}
