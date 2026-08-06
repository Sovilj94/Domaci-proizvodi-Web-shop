using EPiljara.Domain.Common;

namespace EPiljara.Domain.Entities;

public class DeliveryZone : Entity
{
    public string Name { get; set; } = string.Empty;
    public decimal MinOrderAmount { get; set; }
    public decimal DeliveryFee { get; set; }
    public bool IsActive { get; set; } = true;
}
