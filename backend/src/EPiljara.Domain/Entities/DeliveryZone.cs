using EPiljara.Domain.Common;

namespace EPiljara.Domain.Entities;

public class DeliveryZone : Entity
{
    public string Name { get; set; } = string.Empty;
    public decimal MinOrderAmount { get; set; }
    public decimal DeliveryFee { get; set; }
    /// <summary>Dostavljamo u ovo naselje. Neaktivno naselje odbija nove adrese kao da ne postoji.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Granica naselja kao GeoJSON Polygon/MultiPolygon (lon, lat). null = naselje bez granice:
    /// nijedna nova adresa ga ne može dobiti dok mu admin ne nacrta granicu.</summary>
    public string? Granica { get; set; }
    public double? CentarLat { get; set; }
    public double? CentarLon { get; set; }

    /// <summary>"procjena" (izračunata iz centara naselja) ili "potvrdjeno" (admin provjerio na mapi).</summary>
    public string IzvorGranice { get; set; } = IzvoriGranice.Procjena;
}

public static class IzvoriGranice
{
    public const string Procjena = "procjena";
    public const string Potvrdjeno = "potvrdjeno";
}
