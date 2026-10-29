using EPiljara.Domain.Common;

namespace EPiljara.Domain.Entities;

/// <summary>
/// Slika sačuvana u bazi — jedino mjesto gdje slike proizvoda, kategorija, korpica i domaćina
/// žive. API je servira na <c>/slike/{Kljuc}</c>, a polja <c>ImageUrl</c>/<c>PhotoUrl</c> čuvaju
/// tu putanju. Nova slika iz admin panela dobija novi ključ, pa keš nikad ne vrati staru.
/// </summary>
public class Slika : Entity
{
    /// <summary>Putanja ispod <c>/slike/</c>, npr. <c>products/kajmak.jpg</c>. Jedinstven.</summary>
    public string Kljuc { get; set; } = string.Empty;
    public string ContentType { get; set; } = "image/jpeg";
    public byte[] Podaci { get; set; } = [];
    public int Velicina { get; set; }
    public DateTime Kreirana { get; set; } = DateTime.UtcNow;
}
