namespace EPiljara.Application.DTOs;

public record ReceptKratkoDto
{
    public Guid Id { get; init; }
    public string Naslov { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Opis { get; init; } = string.Empty;
    public string? SlikaUrl { get; init; }
    public int VrijemeMinuta { get; init; }
    public int Porcije { get; init; }
    /// <summary>„Lako" | „Srednje" | „Zahtjevno".</summary>
    public string Tezina { get; init; } = string.Empty;
}

public record SastojakDto
{
    public string Naziv { get; init; } = string.Empty;
    public string Kolicina { get; init; } = string.Empty;
    /// <summary>Samo za običan, neobrisan proizvod; inače odsutno.</summary>
    public ProductDto? Proizvod { get; init; }
}

public record ReceptDto : ReceptKratkoDto
{
    public IReadOnlyList<string> Koraci { get; init; } = [];
    public IReadOnlyList<SastojakDto> Sastojci { get; init; } = [];
    public bool Objavljen { get; init; }
}

public record SastojakAdminDto
{
    public string Naziv { get; init; } = string.Empty;
    public string Kolicina { get; init; } = string.Empty;
    public Guid? ProductId { get; init; }
}

/// <summary>Admin vidi sirovu vezu (ProductId), ne prikaz proizvoda; koraci su razbijeni kao u javnom DTO-u.</summary>
public record ReceptAdminDto : ReceptKratkoDto
{
    public IReadOnlyList<string> Koraci { get; init; } = [];
    public IReadOnlyList<SastojakAdminDto> Sastojci { get; init; } = [];
    public bool Objavljen { get; init; }
    public DateTime Kreiran { get; init; }
}
