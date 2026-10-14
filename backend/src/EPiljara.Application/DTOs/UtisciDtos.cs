namespace EPiljara.Application.DTOs;

public record UtisakDto
{
    public Guid Id { get; init; }
    public string Ime { get; init; } = string.Empty;
    public string Mjesto { get; init; } = string.Empty;
    public string Tekst { get; init; } = string.Empty;
    public bool Objavljen { get; init; }
    public int Redoslijed { get; init; }
}
