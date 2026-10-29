namespace EPiljara.Application.DTOs;

public record CategoryDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
    public string? NazivEn { get; init; }
    public int SortOrder { get; init; }
    public int? ProductCount { get; init; }
}

public record ProducerRefDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Village { get; init; } = string.Empty;
    public string? PhotoUrl { get; init; }
}

public record ProducerDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Village { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? PhotoUrl { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public string? Phone { get; init; }
    /// <summary>
    /// Do četiri imena dostupnih običnih artikala, po imenu — za karticu domaćina na
    /// početnoj. Puni ga samo GetProducersHandler; ostali pozivaoci ToDto() dobijaju [].
    /// </summary>
    public IReadOnlyList<string> Artikli { get; init; } = [];
}

public record ProductDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public string Unit { get; init; } = "kom";
    public decimal Stock { get; init; }
    public bool IsAvailable { get; init; }
    public Guid CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public string CategorySlug { get; init; } = string.Empty;
    public Guid ProducerId { get; init; }
    public ProducerRefDto? Producer { get; init; }
    public string? ImageUrl { get; init; }
    public string? IzrezUrl { get; init; }
    public string? NazivEn { get; init; }
    public string? OpisEn { get; init; }
    /// <summary>Boja pločice — uvijek popunjena (zadana ili iz palete, vidi BojaProizvoda).</summary>
    public string Boja { get; init; } = string.Empty;
    /// <summary>Boja koju je admin zadao; null kad se koristi paleta. Za formu proizvoda.</summary>
    public string? ZadanaBoja { get; init; }
    public string Description { get; init; } = string.Empty;
    public bool IsFeatured { get; init; }
    /// <summary>Smije li ovaj artikal ući u sadržaj korpice (vidi Product.MozeUKorpicu).</summary>
    public bool MozeUKorpicu { get; init; }
    /// <summary>0 = osnovni artikal, veći broj = dopuna (vidi Product.PrioritetUKorpici).</summary>
    public int PrioritetUKorpici { get; init; }
    /// <summary>Mjeseci (1–12) sezone; praznina znači cijele godine (vidi Product.Mjeseci).</summary>
    public IReadOnlyList<int> Mjeseci { get; init; } = [];
}

/// <summary>
/// Proizvod za admin ekran: javni <see cref="ProductDto"/> plus nabavna cijena i marža.
/// Odvojen tip, da nabavna cijena ne može slučajno procuriti na javni katalog.
/// </summary>
public record AdminProductDto : ProductDto
{
    public AdminProductDto(ProductDto osnova) : base(osnova) { }

    public decimal? NabavnaCijena { get; init; }

    /// <summary>Udio marže u prodajnoj cijeni, 0–1 (0.35 = 35%). Null bez nabavne cijene.</summary>
    public decimal? Marza { get; init; }
}
