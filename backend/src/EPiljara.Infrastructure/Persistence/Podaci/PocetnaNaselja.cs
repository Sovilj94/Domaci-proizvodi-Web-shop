using System.Globalization;
using System.Text;
using System.Text.Json;

namespace EPiljara.Infrastructure.Persistence.Podaci;

public record PocetnoNaselje(string Naziv, string Granica, double CentarLat, double CentarLon);

/// <summary>
/// Početne granice banjalučkih naselja, koje migracija <c>NaseljaIGranice</c> upisuje u bazu.
/// Izvor je <c>tools/naselja</c> (OSM, © OpenStreetMap contributors, ODbL). Granice su procjena
/// iz centara naselja; admin ih ispravlja na stranici „Naselja", pa se poslije migracije čitaju
/// isključivo iz baze.
/// </summary>
public static class PocetnaNaselja
{
    private const string Resurs = "EPiljara.Infrastructure.Persistence.Podaci.naselja-2026-11-11.json";

    public static IReadOnlyList<PocetnoNaselje> Ucitaj()
    {
        using var s = typeof(PocetnaNaselja).Assembly.GetManifestResourceStream(Resurs)
            ?? throw new InvalidOperationException($"Nema ugrađenog resursa {Resurs}.");
        using var doc = JsonDocument.Parse(s);
        return doc.RootElement.EnumerateArray()
            .Select(e => new PocetnoNaselje(
                e.GetProperty("naziv").GetString()!,
                e.GetProperty("granica").GetString()!,
                e.GetProperty("centarLat").GetDouble(),
                e.GetProperty("centarLon").GetDouble()))
            .ToList();
    }

    /// <summary>
    /// SQL za Postgres. Postojeća zona istog imena (bez kvačica i velikih slova) dobija granicu i
    /// centar; inače se dodaje novo aktivno naselje. Zone koje se ne poklope ostaju netaknute.
    /// <c>MigrationBuilder.Sql</c> nema parametre, pa se tekst bježi — podaci su naš fajl.
    /// </summary>
    public static string Sql(IEnumerable<PocetnoNaselje> naselja)
    {
        const string Kljuc = "lower(translate(\"Name\", 'čćžšđČĆŽŠĐ', 'cczsdCCZSD'))";
        var sb = new StringBuilder();
        foreach (var n in naselja)
        {
            var ime = Lit(n.Naziv);
            var kljuc = Lit(Normalizuj(n.Naziv));
            var granica = Lit(n.Granica);
            var lat = n.CentarLat.ToString(CultureInfo.InvariantCulture);
            var lon = n.CentarLon.ToString(CultureInfo.InvariantCulture);

            sb.AppendLine(
                $"UPDATE \"DeliveryZones\" SET \"Granica\" = {granica}, \"CentarLat\" = {lat}, \"CentarLon\" = {lon} " +
                $"WHERE {Kljuc} = {kljuc};");
            sb.AppendLine(
                "INSERT INTO \"DeliveryZones\" (\"Id\", \"Name\", \"MinOrderAmount\", \"DeliveryFee\", \"IsActive\", " +
                "\"Granica\", \"CentarLat\", \"CentarLon\", \"IzvorGranice\") " +
                $"SELECT md5('naselje:' || {kljuc})::uuid, {ime}, 0, 0, TRUE, {granica}, {lat}, {lon}, 'procjena' " +
                $"WHERE NOT EXISTS (SELECT 1 FROM \"DeliveryZones\" WHERE {Kljuc} = {kljuc});");
        }
        return sb.ToString();
    }

    private static string Lit(string s) => "'" + s.Replace("'", "''") + "'";

    private static string Normalizuj(string s)
    {
        var sb = new StringBuilder();
        foreach (var c in s)
            sb.Append(c switch
            {
                'č' or 'ć' => 'c', 'Č' or 'Ć' => 'C', 'ž' => 'z', 'Ž' => 'Z', 'š' => 's', 'Š' => 'S',
                'đ' => 'd', 'Đ' => 'D', _ => c,
            });
        return sb.ToString().ToLowerInvariant();
    }
}
