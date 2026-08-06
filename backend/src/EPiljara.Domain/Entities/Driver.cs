using EPiljara.Domain.Common;

namespace EPiljara.Domain.Entities;

/// <summary>
/// A delivery driver. Linked (optionally) to a <see cref="User"/> with role Driver
/// so the mobile driver app can authenticate and load its assigned route.
/// </summary>
public class Driver : Entity
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;

    public Guid? UserId { get; set; }
    public User? User { get; set; }

    public ICollection<DeliveryRoute> Routes { get; set; } = new List<DeliveryRoute>();
}
