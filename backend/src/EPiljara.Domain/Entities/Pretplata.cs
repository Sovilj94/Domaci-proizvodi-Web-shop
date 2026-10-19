using EPiljara.Domain.Common;
using EPiljara.Domain.Enums;

namespace EPiljara.Domain.Entities;

/// <summary>
/// Nalog za ponavljanje: jedna korpica, svake sedmice, dok je kupac ne pauzira ili otkaže.
///
/// Nije narudžba i ne postaje narudžba — narudžbe nastaju iz nje, jedna po sedmici, kroz
/// isti put kojim ide i checkout. Zato ovdje nema ni cijene ni stavki: cijena se čita iz
/// korpice u trenutku isporuke (vidi PretplatnaCijena), a sadržaj iz sedmičnog šablona.
///
/// Adresa, zona i termin stoje ovdje kao VRIJEDNOSTI, ne kao veza na adresar. Kupac koji
/// obriše adresu iz adresara ne smije time tiho pomjeriti gdje mu stiže sljedeća korpica.
/// </summary>
public class Pretplata : Entity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Product sa Vrsta == Korpica.</summary>
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public StatusPretplate Status { get; set; } = StatusPretplate.Aktivna;

    public Guid DeliveryZoneId { get; set; }
    public DeliveryZone? DeliveryZone { get; set; }

    public Guid DeliverySlotId { get; set; }
    public DeliverySlot? DeliverySlot { get; set; }

    public string DeliveryAddress { get; set; } = string.Empty;

    /// <summary>Nullable kao svuda — Nominatim ne nalazi svaku adresu, a 0,0 je u Atlantiku.</summary>
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    /// <summary>Napomena za vozača; prepisuje se na svaku narudžbu ove pretplate.</summary>
    public string? Napomena { get; set; }

    /// <summary>
    /// Koliko je narudžbi ove pretplate stvarno ISPORUČENO. Osnov popusta, pa se diže samo
    /// na prelaz u Delivered i samo kroz BrojacIsporuka — vidi tamo zašto postoji pomoćnik.
    /// Izveden podatak: mjerodavan je broj narudžbi sa PretplataId u statusu Delivered.
    /// </summary>
    public int BrojIsporuka { get; set; }

    /// <summary>Plan (R2): određuje popust i obavezu — vidi PretplatnaCijena.</summary>
    public PlanPretplate Plan { get; set; } = PlanPretplate.Nedjeljna;

    /// <summary>
    /// Na koliko isporuka se kupac obavezao pri pretplati (snimak plana iz tog trenutka, da
    /// izmjena opcija ne promijeni već preuzetu obavezu). 0 = bez obaveze.
    /// </summary>
    public int ObavezaIsporuka { get; set; }

    /// <summary>
    /// Sedmica za koju je kupac obaviješten da mu je isporuka preskočena (A2) — da obavještenje
    /// ode tačno jednom, iako posao prolazi svakih 15 minuta.
    /// </summary>
    public int? ObavijestPreskocenaGodina { get; set; }
    public int? ObavijestPreskocenaSedmica { get; set; }

    /// <summary>
    /// Prva sedmica u kojoj pretplata ponovo radi. Dok je ciljna sedmica manja od ove,
    /// posao preskače. null kad pauze nema.
    /// </summary>
    public int? PauzaDoGodina { get; set; }
    public int? PauzaDoSedmica { get; set; }

    /// <summary>
    /// Sedmica za koju je posao posljednji put napravio narudžbu. Ovo je ključ
    /// idempotentnosti i upisuje se u ISTOJ transakciji kao narudžba — razdvojeno bi pad
    /// između dva upisa dao kupcu dvije korpice i dva računa pouzećem.
    /// </summary>
    public int? PoslednjaGodina { get; set; }
    public int? PoslednjaSedmica { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? OtkazanaU { get; set; }

    /// <summary>Dozvoljeni prelazi. Otkazana je završna — istorija narudžbi visi o njoj.</summary>
    public static readonly IReadOnlyDictionary<StatusPretplate, StatusPretplate[]> Prelazi =
        new Dictionary<StatusPretplate, StatusPretplate[]>
        {
            [StatusPretplate.Aktivna] = [StatusPretplate.Pauzirana, StatusPretplate.Otkazana],
            [StatusPretplate.Pauzirana] = [StatusPretplate.Aktivna, StatusPretplate.Otkazana],
            [StatusPretplate.Otkazana] = [],
        };

    public bool MozePreci(StatusPretplate u) =>
        Prelazi.TryGetValue(Status, out var dozvoljeni) && dozvoljeni.Contains(u);

    public void Predji(StatusPretplate u)
    {
        if (!MozePreci(u))
            throw new ConflictException(
                $"Prelaz statusa pretplate {Status} → {u} nije dozvoljen.",
                "nedozvoljen_prelaz_pretplate");

        Status = u;
        if (u == StatusPretplate.Otkazana)
        {
            OtkazanaU = DateTime.UtcNow;
            // Pauza na otkazanoj pretplati nema šta da znači, a ostala bi da zbunjuje
            // svakoga ko kasnije gleda red.
            PauzaDoGodina = null;
            PauzaDoSedmica = null;
        }
        if (u == StatusPretplate.Aktivna)
        {
            PauzaDoGodina = null;
            PauzaDoSedmica = null;
        }
    }
}
