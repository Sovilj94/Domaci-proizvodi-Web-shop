using EPiljara.Domain.Common;

namespace EPiljara.Domain.Entities;

/// <summary>
/// Vrsta troška (Gorivo, Zakup, Ambalaža na veliko…). Pravi ih admin — nijedna nije zakucana u kod.
/// <see cref="JeGorivo"/> označava vrstu čiji računi u analitici zamjenjuju procjenu goriva po rutama.
/// </summary>
public class VrstaTroska : Entity
{
    public string Naziv { get; set; } = string.Empty;
    public bool JeGorivo { get; set; }
    public int Redoslijed { get; set; }
    public bool Aktivna { get; set; } = true;
}

/// <summary>Jednokratni trošak: račun, uplata. Mjesec troška je mjesec <see cref="Datum"/>-a.</summary>
public class Trosak : Entity
{
    public DateOnly Datum { get; set; }
    public Guid VrstaTroskaId { get; set; }
    public VrstaTroska? Vrsta { get; set; }
    public decimal Iznos { get; set; }
    public string Opis { get; set; } = string.Empty;
    /// <summary>Slika računa u bazi (/slike/troskovi/…); null kad nije priložena.</summary>
    public string? RacunUrl { get; set; }
    public DateTime Kreiran { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Trošak koji se ponavlja svaki mjesec (zakup, internet) od mjeseca Od do mjeseca Do (uključivo).
/// Ne pravi redove po mjesecu — analitika ga uračunava za svaki mjesec u opsegu. Do = null: još traje.
/// </summary>
public class PonavljajuciTrosak : Entity
{
    public Guid VrstaTroskaId { get; set; }
    public VrstaTroska? Vrsta { get; set; }
    public decimal Iznos { get; set; }
    public string Opis { get; set; } = string.Empty;
    public int OdGodina { get; set; }
    public int OdMjesec { get; set; }
    public int? DoGodina { get; set; }
    public int? DoMjesec { get; set; }

    public bool VaziU(int godina, int mjesec)
    {
        var m = godina * 12 + mjesec;
        if (m < OdGodina * 12 + OdMjesec) return false;
        return DoGodina is not int dg || DoMjesec is not int dm || m <= dg * 12 + dm;
    }
}

/// <summary>
/// Cijena goriva i potrošnja vozila od datuma <see cref="VaziOd"/> do sljedećeg unosa. Iz nje se
/// računa trošak goriva ruta; nova cijena ne mijenja prošle mjesece.
/// </summary>
public class CijenaGoriva : Entity
{
    public DateOnly VaziOd { get; set; }
    public decimal CijenaPoLitru { get; set; }
    public decimal PotrosnjaL100km { get; set; }
}
