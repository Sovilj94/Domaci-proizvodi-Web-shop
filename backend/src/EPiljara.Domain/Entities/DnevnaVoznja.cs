using EPiljara.Domain.Common;

namespace EPiljara.Domain.Entities;

/// <summary>
/// Vozačev radni dan: procjena dnevne ture (magacin → sve stanice tog dana, preko svih zona → nazad
/// u magacin, gdje kombi stoji) i, po potrebi, koliko je vozač stvarno platio gorivo na pumpi.
/// Jedan red po vozaču i danu. Procjena se preračunava kad se promijeni skup stanica (vidi
/// <see cref="ProcjenaPotpis"/>); unos goriva se pri tome nikad ne dira.
/// </summary>
public class DnevnaVoznja : Entity
{
    public Guid DriverId { get; set; }
    public Driver? Driver { get; set; }
    public DateOnly Datum { get; set; }

    /// <summary>Dužina cijele dnevne ture sa povratkom u magacin; null kad nijedna stanica nema koordinate.</summary>
    public double? ProcjenaMetara { get; set; }
    public double? ProcjenaSekundi { get; set; }
    /// <summary>"osrm" (stvarni putevi) ili "vazdusno" (vazdušna linija × faktor puta, kad OSRM nije dostupan).</summary>
    public string? ProcjenaIzvor { get; set; }
    public int ProcjenaStanica { get; set; }
    public int ProcjenaBezKoordinata { get; set; }
    /// <summary>Otisak skupa stanica i magacina iz kog je procjena izračunata — drugačiji otisak znači zastarjelu procjenu.</summary>
    public string ProcjenaPotpis { get; set; } = string.Empty;
    public DateTime? ProcjenaIzracunata { get; set; }

    /// <summary>Iznos sa pumpe koji je vozač (ili admin) unio; kad postoji, u dobiti zamjenjuje procjenu za ovaj dan.</summary>
    public decimal? PlacenoGorivo { get; set; }
    public decimal? Litara { get; set; }
    public string? Napomena { get; set; }
    public DateTime? PlacenoUneseno { get; set; }
    /// <summary>"vozac" ili "admin".</summary>
    public string? PlacenoUnio { get; set; }

    /// <summary>Stanje sata (km) kad vozač krene iz magacina.</summary>
    public int? KmPocetak { get; set; }
    /// <summary>Stanje sata (km) kad se vrati u magacin. Pređeni km dana = KmKraj − KmPocetak; tada
    /// zamjenjuju procijenjenu turu u procjeni goriva.</summary>
    public int? KmKraj { get; set; }

    public int? PredjenoKm => KmPocetak is int p && KmKraj is int k && k >= p ? k - p : null;
}
