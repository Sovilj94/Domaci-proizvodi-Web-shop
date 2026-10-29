using EPiljara.Domain.Common;

namespace EPiljara.Domain.Entities;

public class Category : Entity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    /// <summary>Naziv na engleskom; prazno = srpski naziv.</summary>
    public string? NazivEn { get; set; }
    public int SortOrder { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
