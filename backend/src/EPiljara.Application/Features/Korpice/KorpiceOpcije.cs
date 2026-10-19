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
    /// Najmanja marža korpice, kao udio njene cijene. 0.25 znači da nabavna vrijednost
    /// sadržaja smije biti najviše 75% cijene korpice. Generator po tome određuje količine
    /// artikala sa unesenom nabavnom cijenom, a kupčeva zamjena je ne smije probiti.
    /// </summary>
    public decimal MinimalnaMarza { get; set; } = 0.25m;

    /// <summary>Koliko predloženih zamjena generator dodaje uz svaki sadržaj.</summary>
    public int BrojZamjena { get; set; } = 4;

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

    /// <summary>
    /// Vrijeme isporuke (npr. "16:00") — SAMO rezerva za slučaj kad u bazi ne postoji
    /// nijedan <see cref="EPiljara.Domain.Entities.DeliverySlot"/>. Dok DeliverySlot
    /// postoji, njegov TimeFrom je jedini izvor istine i ovo polje se ne čita.
    /// </summary>
    public string VrijemeIsporukeRezerva { get; set; } = "16:00";

    public Dictionary<VelicinaKorpice, int> MinimalanBrojArtikala { get; set; } = new()
    {
        [VelicinaKorpice.Mala] = 5,
        [VelicinaKorpice.Srednja] = 7,
        [VelicinaKorpice.Velika] = 9,
    };
}
