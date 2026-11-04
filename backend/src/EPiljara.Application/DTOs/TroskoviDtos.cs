namespace EPiljara.Application.DTOs;

/// <summary>Stranica Troškovi: unosi jednog mjeseca + šifarnici. Samo admin.</summary>
public record TroskoviMjesecaDto
{
    public int Godina { get; init; }
    public int Mjesec { get; init; }
    public IReadOnlyList<TrosakDto> Troskovi { get; init; } = [];
    public IReadOnlyList<PonavljajuciTrosakDto> Ponavljajuci { get; init; } = [];
    public IReadOnlyList<VrstaTroskaDto> Vrste { get; init; } = [];
    /// <summary>Najnovija prva.</summary>
    public IReadOnlyList<CijenaGorivaDto> CijeneGoriva { get; init; } = [];
}

public record TrosakDto
{
    public Guid Id { get; init; }
    public DateOnly Datum { get; init; }
    public Guid VrstaId { get; init; }
    public string Vrsta { get; init; } = string.Empty;
    public bool JeGorivo { get; init; }
    public decimal Iznos { get; init; }
    public string Opis { get; init; } = string.Empty;
    public string? RacunUrl { get; init; }
}

public record PonavljajuciTrosakDto
{
    public Guid Id { get; init; }
    public Guid VrstaId { get; init; }
    public string Vrsta { get; init; } = string.Empty;
    public decimal Iznos { get; init; }
    public string Opis { get; init; } = string.Empty;
    public int OdGodina { get; init; }
    public int OdMjesec { get; init; }
    public int? DoGodina { get; init; }
    public int? DoMjesec { get; init; }
    public bool VaziUMjesecu { get; init; }
}

public record VrstaTroskaDto
{
    public Guid Id { get; init; }
    public string Naziv { get; init; } = string.Empty;
    public bool JeGorivo { get; init; }
    public int Redoslijed { get; init; }
    public bool Aktivna { get; init; }
    public bool UUpotrebi { get; init; }
}

public record CijenaGorivaDto
{
    public Guid Id { get; init; }
    public DateOnly VaziOd { get; init; }
    public decimal CijenaPoLitru { get; init; }
    public decimal PotrosnjaL100km { get; init; }
}
