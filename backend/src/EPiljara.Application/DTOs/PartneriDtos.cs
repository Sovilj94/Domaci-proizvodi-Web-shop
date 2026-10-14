namespace EPiljara.Application.DTOs;

/// <summary>Odgovor javnog obrasca. Bez id-a, isti za uspjeh i za honeypot.</summary>
public record PrijavaPrimljenaDto
{
    public bool Primljena { get; init; } = true;
}

public record PartnerPrijavaDto
{
    public Guid Id { get; init; }
    public string Ime { get; init; } = string.Empty;
    public string Gazdinstvo { get; init; } = string.Empty;
    public string Mjesto { get; init; } = string.Empty;
    public string Telefon { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Proizvodi { get; init; } = string.Empty;
    /// <summary>„Nova", „UObradi", „Prihvacena" ili „Odbijena".</summary>
    public string Status { get; init; } = string.Empty;
    public string AdminNapomena { get; init; } = string.Empty;
    public DateTime Kreirana { get; init; }
}
