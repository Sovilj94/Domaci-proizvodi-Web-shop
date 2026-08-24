using EPiljara.Domain.Common;
using EPiljara.Domain.Enums;

namespace EPiljara.Domain.Entities;

/// <summary>A driver's delivery run for a given day, composed of ordered stops.</summary>
public class DeliveryRoute : Entity
{
    public DateOnly Date { get; set; }

    public Guid DriverId { get; set; }
    public Driver? Driver { get; set; }

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
