using EPiljara.Domain.Enums;

namespace EPiljara.Domain.Entities;

/// <summary>
/// Iz kojih kategorija se puni koji tip korpice. Veza bez sopstvenog Id-a —
/// složeni ključ (TipKorpice, CategoryId). Ista kategorija smije u više tipova:
/// „Mliječni proizvodi" ide i u Mliječnu i u Domaćinsku korpicu.
/// </summary>
public class KorpicaKategorija
{
    public TipKorpice TipKorpice { get; set; }
    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }
}
