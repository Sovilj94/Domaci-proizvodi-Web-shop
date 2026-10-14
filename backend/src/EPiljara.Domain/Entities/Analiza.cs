using EPiljara.Domain.Common;
using EPiljara.Domain.Enums;

namespace EPiljara.Domain.Entities;

/// <summary>
/// Objavljen laboratorijski nalaz proizvođača (spec P5). Unosi ga admin; javno se vide samo
/// objavljeni, pa i oni koji nisu uskladjeni (O12). Nikad se ne seeduje (O2).
/// </summary>
public class Analiza : Entity
{
    public Guid ProducerId { get; set; }
    public Producer? Producer { get; set; }
    /// <summary>Opcion — nalaz može važiti za cijelog proizvođača.</summary>
    public Guid? ProductId { get; set; }
    public Product? Product { get; set; }
    public string Uzorak { get; set; } = string.Empty;
    public string Laboratorija { get; set; } = string.Empty;
    public string BrojIzvjestaja { get; set; } = string.Empty;
    public DateOnly DatumUzorkovanja { get; set; }
    public string Ispitano { get; set; } = string.Empty;
    public RezultatAnalize Rezultat { get; set; } = RezultatAnalize.Uskladjeno;
    public string Napomena { get; set; } = string.Empty;
    public string? PdfUrl { get; set; }
    public bool Objavljena { get; set; }
    public DateTime Kreiran { get; set; } = DateTime.UtcNow;
}
