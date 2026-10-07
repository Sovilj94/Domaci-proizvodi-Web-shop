using EPiljara.Domain.Enums;

namespace EPiljara.Application.Features.Korpice;

/// <summary>Podešavanja korpica, iz sekcije „Korpice" u konfiguraciji.</summary>
public class KorpiceOpcije
{
    public const string Sekcija = "Korpice";

    /// <summary>
    /// Koliko puta vrijednost sadržaja prelazi cijenu korpice. 1.15 znači da kupac
    /// dobija 15% više vrijednosti nego što plati — to je popust za to što ne bira sam.
    /// </summary>
    public decimal KoeficijentVrijednosti { get; set; } = 1.15m;

    /// <summary>Koliko sati prije isporuke se zatvara naručivanje.</summary>
    public int RokSati { get; set; } = 48;

    public Dictionary<VelicinaKorpice, int> MinimalanBrojArtikala { get; set; } = new()
    {
        [VelicinaKorpice.Mala] = 5,
        [VelicinaKorpice.Srednja] = 7,
        [VelicinaKorpice.Velika] = 9,
    };
}
