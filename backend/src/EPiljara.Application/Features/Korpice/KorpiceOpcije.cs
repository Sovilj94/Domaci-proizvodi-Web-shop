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

    /// <summary>
    /// Koliko sati prije isporuke se zatvara naručivanje — SAMO rezerva za slučaj
    /// kad u bazi ne postoji nijedan <see cref="EPiljara.Domain.Entities.DeliverySlot"/>
    /// (prazna baza, ili test kojem ovo nije bitno). Dok DeliverySlot postoji, njegov
    /// CutoffHours je jedini izvor istine i ovo polje se ne čita — ciklus je vezan za
    /// DeliverySlot, ne za konfiguraciju.
    /// </summary>
    public int RokSati { get; set; } = 48;

    /// <summary>
    /// Dan u sedmici za isporuku korpica — SAMO rezerva za slučaj kad u bazi ne
    /// postoji nijedan <see cref="EPiljara.Domain.Entities.DeliverySlot"/>. Dok
    /// DeliverySlot postoji, dan sa najnižim DayOfWeek (prvi dan isporuke u sedmici)
    /// je jedini izvor istine i ovo polje se ne čita.
    /// </summary>
    public DayOfWeek DanIsporukeRezerva { get; set; } = DayOfWeek.Friday;

    public Dictionary<VelicinaKorpice, int> MinimalanBrojArtikala { get; set; } = new()
    {
        [VelicinaKorpice.Mala] = 5,
        [VelicinaKorpice.Srednja] = 7,
        [VelicinaKorpice.Velika] = 9,
    };
}
