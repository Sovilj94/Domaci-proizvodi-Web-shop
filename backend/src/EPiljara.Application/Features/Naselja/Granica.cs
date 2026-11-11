using System.Globalization;
using System.Text;
using System.Text.Json;
using EPiljara.Domain.Common;
using NetTopologySuite.Geometries;

namespace EPiljara.Application.Features.Naselja;

/// <summary>
/// Granica naselja: GeoJSON <c>Polygon</c> ili <c>MultiPolygon</c> (lon, lat) kao tekst u koloni
/// <c>DeliveryZone.Granica</c>. Geometrija se računa u memoriji preko NetTopologySuite — bez PostGIS-a,
/// da Neon/Render podešavanje ostane isto. Naselja je dvadesetak, pa čitanje po zahtjevu ne košta.
/// </summary>
public static class Granica
{
    private static readonly GeometryFactory Fabrika = new(new PrecisionModel(), 4326);

    /// <summary>Metara po stepenu na geografskoj širini Banje Luke — dovoljno za površine reda
    /// veličine naselja (greška ispod 1 %).</summary>
    private const double MetaraPoStepenuSirine = 110540;
    private static readonly double MetaraPoStepenuDuzine = 111320 * Math.Cos(44.77 * Math.PI / 180);

    public static Geometry Procitaj(string geoJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(geoJson);
            var root = doc.RootElement;
            var tip = root.GetProperty("type").GetString();
            var koord = root.GetProperty("coordinates");

            Geometry g = tip switch
            {
                "Polygon" => Poligon(koord),
                "MultiPolygon" => Fabrika.CreateMultiPolygon(
                    koord.EnumerateArray().Select(Poligon).ToArray()),
                _ => throw Greska(),
            };

            if (g.IsEmpty || !g.IsValid) throw Greska();
            return g;
        }
        catch (DomainException) { throw; }
        catch (Exception e) when (e is JsonException or KeyNotFoundException
                                   or InvalidOperationException or ArgumentException
                                   or FormatException or IndexOutOfRangeException)
        {
            throw Greska();
        }
    }

    public static string Zapisi(Geometry g)
    {
        var sb = new StringBuilder();
        switch (g)
        {
            case Polygon p:
                sb.Append("{\"type\":\"Polygon\",\"coordinates\":");
                PisiPoligon(sb, p);
                break;
            case MultiPolygon mp:
                sb.Append("{\"type\":\"MultiPolygon\",\"coordinates\":[");
                for (var i = 0; i < mp.NumGeometries; i++)
                {
                    if (i > 0) sb.Append(',');
                    PisiPoligon(sb, (Polygon)mp.GetGeometryN(i));
                }
                sb.Append(']');
                break;
            default:
                throw Greska();
        }
        sb.Append('}');
        return sb.ToString();
    }

    /// <summary>Tačka unutar granice ili tačno na njoj.</summary>
    public static bool Sadrzi(Geometry g, double lat, double lon) =>
        g.Covers(Fabrika.CreatePoint(new Coordinate(lon, lat)));

    /// <summary>Površina presjeka dvije granice u m².</summary>
    public static double PreklapanjeM2(Geometry a, Geometry b)
    {
        if (!a.EnvelopeInternal.Intersects(b.EnvelopeInternal)) return 0;
        var presjek = a.Intersection(b);
        return presjek.Area * MetaraPoStepenuDuzine * MetaraPoStepenuSirine;
    }

    /// <summary>Tačka koja sigurno leži unutar granice (i za konkavan oblik).</summary>
    public static (double Lat, double Lon) Centar(Geometry g)
    {
        var t = g.InteriorPoint;
        return (t.Y, t.X);
    }

    private static Polygon Poligon(JsonElement prstenovi)
    {
        var lista = prstenovi.EnumerateArray().Select(Prsten).ToArray();
        if (lista.Length == 0) throw Greska();
        return Fabrika.CreatePolygon(lista[0], lista.Skip(1).ToArray());
    }

    private static LinearRing Prsten(JsonElement tacke)
    {
        var c = tacke.EnumerateArray()
            .Select(t => new Coordinate(t[0].GetDouble(), t[1].GetDouble()))
            .ToList();
        if (c.Count > 0 && !c[0].Equals2D(c[^1])) c.Add(c[0].Copy());
        if (c.Count < 4) throw Greska();
        return Fabrika.CreateLinearRing(c.ToArray());
    }

    private static void PisiPoligon(StringBuilder sb, Polygon p)
    {
        sb.Append('[');
        PisiPrsten(sb, p.ExteriorRing);
        foreach (var rupa in p.InteriorRings)
        {
            sb.Append(',');
            PisiPrsten(sb, rupa);
        }
        sb.Append(']');
    }

    private static void PisiPrsten(StringBuilder sb, LineString r)
    {
        sb.Append('[');
        for (var i = 0; i < r.NumPoints; i++)
        {
            if (i > 0) sb.Append(',');
            var c = r.GetCoordinateN(i);
            sb.Append('[')
              .Append(Math.Round(c.X, 6).ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(Math.Round(c.Y, 6).ToString(CultureInfo.InvariantCulture)).Append(']');
        }
        sb.Append(']');
    }

    private static DomainException Greska() =>
        new("Granica nije ispravan poligon.", "neispravna_granica");
}
