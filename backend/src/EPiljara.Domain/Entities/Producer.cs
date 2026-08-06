using EPiljara.Domain.Common;

namespace EPiljara.Domain.Entities;

/// <summary>A local family/farm on Manjača that supplies products.</summary>
public class Producer : Entity
{
    public string Name { get; set; } = string.Empty;
    public string Village { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Phone { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
