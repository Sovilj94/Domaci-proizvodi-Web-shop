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
    public double? TrajanjeSekundi { get; set; }
    public string? Geometrija { get; set; }

    public ICollection<RouteStop> Stops { get; set; } = new List<RouteStop>();
}
