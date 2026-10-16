namespace EPiljara.Application.DTOs;

public record AnalizaDto
{
    public Guid Id { get; init; }
    public Guid ProducerId { get; init; }
    public string Proizvodjac { get; init; } = string.Empty;
    public string Selo { get; init; } = string.Empty;
    public Guid? ProductId { get; init; }
    public string? Proizvod { get; init; }
    /// <summary>Slika vezanog proizvoda — za obojenu karticu analize (null bez proizvoda).</summary>
    public string? Slika { get; init; }
    /// <summary>Boja kartice: boja vezanog proizvoda, inače iz palete po uzorku.</summary>
    public string Boja { get; init; } = string.Empty;
    public string Uzorak { get; init; } = string.Empty;
    public string Laboratorija { get; init; } = string.Empty;
    public string BrojIzvjestaja { get; init; } = string.Empty;
    /// <summary>System.Text.Json piše DateOnly kao „yyyy-MM-dd".</summary>
    public DateOnly DatumUzorkovanja { get; init; }
    public string Ispitano { get; init; } = string.Empty;
    /// <summary>„Uskladjeno" ili „NijeUskladjeno".</summary>
    public string Rezultat { get; init; } = string.Empty;
    public string Napomena { get; init; } = string.Empty;
    public string? PdfUrl { get; init; }
    public bool Objavljena { get; init; }
}
