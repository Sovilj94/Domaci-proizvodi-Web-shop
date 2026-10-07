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

    /// <summary>Obična roba ili korpica. Korpica ne smije ući u sadržaj druge korpice.</summary>
    public VrstaProizvoda Vrsta { get; set; } = VrstaProizvoda.Obican;

    /// <summary>Postavljeno samo kad je <see cref="Vrsta"/> == Korpica.</summary>
    public TipKorpice? TipKorpice { get; set; }

    /// <summary>Postavljeno samo kad je <see cref="Vrsta"/> == Korpica.</summary>
    public VelicinaKorpice? Velicina { get; set; }

    /// <summary>Smije li obična roba ući u sadržaj korpice.</summary>
    public bool MozeUKorpicu { get; set; }

    /// <summary>0 = osnovni artikal (ide skoro uvijek), veći broj = dopuna.</summary>
    public int PrioritetUKorpici { get; set; }

    /// <summary>
    /// Mjeseci (1–12) u kojima je artikal dostupan. <b>Prazan niz znači cijele godine</b> —
    /// mliječno i suhomesnato nemaju sezonu. U bazi stoji kao CSV string, jer SQLite
    /// (testovi) nema tip niza; vidi AppDbContext.
    /// </summary>
    public int[] Mjeseci { get; set; } = [];
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
