using EPiljara.Domain.Common;
using EPiljara.Domain.Enums;

namespace EPiljara.Domain.Entities;

public class Product : Entity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public Unit Unit { get; set; } = Unit.Kom;
    public decimal Stock { get; set; }
    public bool IsAvailable { get; set; } = true;
    public bool IsFeatured { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }

    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }

    public Guid ProducerId { get; set; }
    public Producer? Producer { get; set; }

    /// <summary>Soft delete — removed products stay in the DB for order history integrity.</summary>
    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
