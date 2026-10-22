using EPiljara.Domain.Common;

namespace EPiljara.Domain.Entities;

/// <summary>
/// Artikal koji kupac ne želi u korpici — nikad. Kupac ga označi jednom („ne stavljaj mi
/// ovo"), a svaka njegova naredna korpica pri narudžbi dobije zamjenu umjesto njega, bez
/// ikakvog klika. Admin spisak vidi kad ručno uređuje kupčevu korpicu.
/// </summary>
public class IzbjegnutArtikal : Entity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public DateTime Kreiran { get; set; } = DateTime.UtcNow;
}
