using EPiljara.Domain.Common;
using EPiljara.Domain.Enums;

namespace EPiljara.Domain.Entities;

/// <summary>
/// A driver's delivery run for a given day, composed of ordered stops. Više ruta može
/// postojati za istog vozača istog dana — jedna po naselju (zoni), da bi svaka ostala
/// unutar Google Maps ograničenja od 10 stanica po linku.
/// </summary>
public class DeliveryRoute : Entity
{
    public DateOnly Date { get; set; }

    public Guid DriverId { get; set; }
    public Driver? Driver { get; set; }

    /// <summary>Naziv koji vozač vidi na kartici — naziv zone, uz redni broj ("Centar 1")
    /// kad je zona podijeljena na više ruta jer ima više od 10 stanica.</summary>
    public string Naziv { get; set; } = string.Empty;

    /// <summary>Zona kojoj ruta pripada; null kad su stanice sastavljene od narudžbi bez
    /// zone (podaci stariji od uvođenja obavezne zone na narudžbi).</summary>
    public Guid? DeliveryZoneId { get; set; }
    public DeliveryZone? DeliveryZone { get; set; }

    public RouteStatus Status { get; set; } = RouteStatus.Planned;

    /// <summary>
    /// Rezultat optimizacije. null kad optimizacija nije uspjela ili nije ni pokušana —
    /// ruta tada radi, samo bez linije i procjene.
    /// Geometrija je GeoJSON niz koordinata kao tekst, spreman za crtanje na mapi.
    /// </summary>
    public double? DuzinaMetara { get; set; }
    /// <summary>
    /// Dužina cijele ture magacin → stanice → magacin, iz optimizacije od magacina. Vozačeva ponovna
    /// optimizacija od trenutne pozicije mijenja DuzinaMetara (ostatak puta), a ovo ne — po ovome se
    /// računa trošak goriva.
    /// </summary>
    public double? KilometrazaMetara { get; set; }
    public double? TrajanjeSekundi { get; set; }
    public string? Geometrija { get; set; }

    /// <summary>Planirani redoslijed ruta vozača u danu (1 = prva). null = ruta iz vremena prije
    /// redoslijeda; takve se ređaju po nazivu iza numerisanih.</summary>
    public int? RedniBroj { get; set; }

    /// <summary>Odakle je redoslijed stanica: "osrm" (pravi putevi) ili "vazdusno" (rezerva po
    /// vazdušnoj udaljenosti — DuzinaMetara je tada samo procjena). null = nije računat.</summary>
    public string? IzvorRedoslijeda { get; set; }

    public ICollection<RouteStop> Stops { get; set; } = new List<RouteStop>();
}
