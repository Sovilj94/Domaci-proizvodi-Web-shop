namespace EPiljara.Application.DTOs;

public record PoeniStavkaDto
{
    public int Iznos { get; init; }
    public string Vrsta { get; init; } = string.Empty;
    public string Opis { get; init; } = string.Empty;
    public string? OrderCode { get; init; }
    public DateTime Kreirana { get; init; }
}

public record MojiPoeniDto
{
    public int Stanje { get; init; }
    public decimal VrijednostPoenaKM { get; init; }
    /// <summary>Stanje u KM; nikad negativno, ma kakvo bilo stanje.</summary>
    public decimal VrijednostKM { get; init; }
    public int NagradaPreporuka { get; init; }
    public string PreporukaKod { get; init; } = string.Empty;
    public int BrojPreporuka { get; init; }
    public IReadOnlyList<PoeniStavkaDto> Istorija { get; init; } = [];
}
