using EPiljara.Domain.Common;

namespace EPiljara.Domain.Entities;

/// <summary>
/// Poslovna pravila piljare — korpice, pretplate, poeni, prodavnica. Jedan red (kao
/// <see cref="Magacin"/>): <see cref="JediniId"/> je jedini dopušten Id i to drži CHECK u bazi.
///
/// Stoje u bazi, a ne u kodu ili konfiguraciji, da ih vlasnik mijenja u adminu bez deploya i da
/// nijedan broj ne živi u kodu. Bez ovog reda API odbija da se pokrene.
/// </summary>
public class Podesavanja : Entity
{
    public static readonly Guid JediniId = new("a0000002-0000-4000-8000-000000000001");

    // ----- Korpice -----
    /// <summary>Koliko puta vrijednost sadržaja prelazi cijenu korpice (1.15 = 15% više).</summary>
    public decimal KoeficijentVrijednosti { get; set; }
    /// <summary>Najmanja marža korpice, udio cijene (0.25 = nabavno najviše 75% cijene).</summary>
    public decimal MinimalnaMarza { get; set; }
    /// <summary>Koliko predloženih zamjena generator dodaje uz sadržaj.</summary>
    public int BrojZamjena { get; set; }
    /// <summary>Najmanje različitih namirnica po veličini korpice.</summary>
    public int MinArtikalaMala { get; set; }
    public int MinArtikalaSrednja { get; set; }
    public int MinArtikalaVelika { get; set; }
    /// <summary>Rok i dan isporuke kad u bazi nema nijednog termina dostave.</summary>
    public int RokSatiRezerva { get; set; }
    public DayOfWeek DanIsporukeRezerva { get; set; }
    public string VrijemeIsporukeRezerva { get; set; } = string.Empty;

    // ----- Pretplate -----
    /// <summary>Popust plana na punu cijenu (0.10 = 10%), od prve isporuke.</summary>
    public decimal PopustNedjeljna { get; set; }
    public decimal PopustMjesecna { get; set; }
    public decimal PopustTriMjeseca { get; set; }
    /// <summary>Na koliko isporuka se kupac obavezuje.</summary>
    public int ObavezaMjesecna { get; set; }
    public int ObavezaTriMjeseca { get; set; }
    /// <summary>Koliko sati prije roka se pravi pretplatna narudžba.</summary>
    public int SatiUnaprijed { get; set; }
    /// <summary>Koliko sati prije roka admin dobija mejl za neobjavljen sadržaj.</summary>
    public int AlarmSatiPrijeRoka { get; set; }
    /// <summary>Najduža pauza odjednom, u sedmicama.</summary>
    public int MaksPauzaSedmica { get; set; }

    // ----- Poeni -----
    public int PoenaPoKM { get; set; }
    public decimal VrijednostPoenaKM { get; set; }
    public int NagradaPreporuka { get; set; }

    // ----- Prodavnica -----
    /// <summary>Od ove zalihe naniže artikal je „pri kraju" („Još samo X na stanju").</summary>
    public decimal PragPriKraju { get; set; }
}
