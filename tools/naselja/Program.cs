// Jednokratna priprema početnih granica banjalučkih naselja (vidi README.md).
// Ulaz: OSM izvoz u ulaz/. Izlaz: naselja-2026-11-11.json koji migracija ugrađuje u bazu.
using System.Globalization;
using System.Text;
using System.Text.Json;
using EPiljara.Application.Features.Naselja;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Polygonize;
using NetTopologySuite.Operation.Union;
using NetTopologySuite.Triangulate;

var dir = AppContext.BaseDirectory.Contains("bin") ? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../")) : ".";
var izlaz = args.Length > 0 ? args[0] : Path.Combine(dir, "naselja-2026-11-11.json");

var f = new GeometryFactory(new PrecisionModel(), 4326);
var kx = Math.Cos(44.77 * Math.PI / 180);

// Sva 51 naselja (57 MZ, numerisane spojene) — Voronoi se računa nad svima, da se granica
// rubnog naselja ne razvuče preko sela koja ne ulaze u početni spisak.
string[] svaNaselja =
[
    "Agino Selo", "Ada", "Bistrica", "Borik", "Borkovići", "Bočac", "Bronzani Majdan", "Bulevar", "Verići",
    "Vrbanja", "Goleši", "Gornja Piskavica", "Debeljaci", "Donja Kola", "Dragočaj", "Drakulić", "Zalužani",
    "Karanovac", "Kmećani", "Kola", "Kočićev Vijenac", "Krmine", "Krupa na Vrbasu", "Kuljani", "Lazarevo",
    "Lauš", "Ljubačevo", "Mišin Han", "Motike", "Nova Varoš", "Obilićevo", "Pavići", "Paprikovac", "Petrićevac",
    "Piskavica", "Pobrđe", "Potkozarje", "Prijakovci", "Priječani", "Rekavice", "Rosulje", "Saračica",
    "Srpske Toplice", "Starčevica", "Stratinska", "Stričići", "Centar", "Česma", "Čokorska Polja", "Šargovac",
    "Šimići",
];

// Odluka vlasnika 11.11.2026.: samo grad i bliska prigradska naselja.
string[] pocetna =
[
    "Centar", "Bulevar", "Obilićevo", "Nova Varoš", "Borik", "Kočićev Vijenac", "Paprikovac", "Pobrđe",
    "Rosulje", "Starčevica", "Lauš", "Petrićevac", "Ada", "Srpske Toplice", "Česma", "Lazarevo", "Vrbanja",
    "Drakulić", "Donja Kola", "Debeljaci", "Šargovac", "Motike", "Saračica", "Zalužani",
];

// OSM ime koje se razlikuje od imena MZ.
var alias = new Dictionary<string, string> { ["Donja Kola"] = "Kola Donja" };

// Prave granice iz OSM-a (admin_level 10).
var praveGranice = new Dictionary<string, long> { ["Srpske Toplice"] = 10079830, ["Kočićev Vijenac"] = 10546755 };
const long GradId = 2528156;

var relacije = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "ulaz/granice-osm.json")))
    .RootElement.GetProperty("elements").EnumerateArray()
    .ToDictionary(e => e.GetProperty("id").GetInt64(), Relacija);

var rang = new Dictionary<string, int>
{
    ["quarter"] = 0, ["suburb"] = 1, ["neighbourhood"] = 2, ["village"] = 3, ["town"] = 3,
    ["hamlet"] = 4, ["isolated_dwelling"] = 5, ["locality"] = 6,
};
var tacke = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "ulaz/mjesta-osm.json")))
    .RootElement.GetProperty("elements").EnumerateArray()
    .Where(e => e.TryGetProperty("tags", out var t) && t.TryGetProperty("name", out _) && e.TryGetProperty("lat", out _))
    .Select(e =>
    {
        var t = e.GetProperty("tags");
        var ime = t.TryGetProperty("name:sr-Latn", out var l) ? l.GetString()! : t.GetProperty("name").GetString()!;
        var mjesto = t.TryGetProperty("place", out var p) ? p.GetString()! : "";
        return (Ime: Norm(ime), Ime2: Norm(t.GetProperty("name").GetString()!), Rang: rang.GetValueOrDefault(mjesto, 9),
            Lat: e.GetProperty("lat").GetDouble(), Lon: e.GetProperty("lon").GetDouble());
    })
    .ToList();

var centri = new Dictionary<string, Coordinate>();
foreach (var n in svaNaselja)
{
    var k = Norm(alias.GetValueOrDefault(n, n));
    var c = tacke.Where(t => t.Ime == k || t.Ime2 == k).OrderBy(t => t.Rang).FirstOrDefault();
    if (c == default) { Console.WriteLine($"bez tačke u OSM-u: {n} (ne utiče na početna naselja)"); continue; }
    centri[n] = new Coordinate(c.Lon, c.Lat);
}
foreach (var n in pocetna)
    if (!centri.ContainsKey(n)) throw new Exception($"Početno naselje bez centra: {n}");

// Voronoi u lokalno projektovanim koordinatama (dužina × cos φ), da ćelije budu „okrugle" u metrima.
Geometry Proj(Geometry g) { var c = g.Copy(); c.Apply(new Skala(kx, 1)); c.GeometryChanged(); return c; }
Geometry Vrati(Geometry g) { var c = g.Copy(); c.Apply(new Skala(1 / kx, 1)); c.GeometryChanged(); return c; }

var grad = Proj(relacije[GradId]);
var prave = praveGranice.ToDictionary(p => p.Key, p => Proj(relacije[p.Value]));
var praveUnija = UnaryUnionOp.Union(prave.Values.ToList());

var vb = new VoronoiDiagramBuilder();
vb.SetSites(centri.Values.Select(c => new Coordinate(c.X * kx, c.Y)).ToList());
var okvir = grad.EnvelopeInternal.Copy(); okvir.ExpandBy(0.1);
vb.ClipEnvelope = okvir;
var dijagram = (GeometryCollection)vb.GetDiagram(f);

var rezultat = new List<object>();
var ukupnoPreklapanje = 0.0;
var sacuvane = new List<Geometry>();
foreach (var n in pocetna)
{
    var c = centri[n];
    var tp = f.CreatePoint(new Coordinate(c.X * kx, c.Y));
    Geometry celija = dijagram.Geometries.First(g => g.Covers(tp));

    Geometry g = prave.TryGetValue(n, out var prava)
        ? prava
        : celija.Intersection(grad).Difference(praveUnija);

    g = Vrati(g);
    g = Ocisti(g);
    if (!g.IsValid || g.IsEmpty) throw new Exception($"Neispravna granica: {n}");

    var json = Granica.Zapisi(g);
    var procitana = Granica.Procitaj(json); // isti put kojim će je čitati aplikacija
    foreach (var s in sacuvane) ukupnoPreklapanje += Granica.PreklapanjeM2(s, procitana);
    sacuvane.Add(procitana);

    rezultat.Add(new { naziv = n, granica = json, centarLat = Math.Round(c.Y, 6), centarLon = Math.Round(c.X, 6) });
    Console.WriteLine(FormattableString.Invariant($"{n,-16} {procitana.Area * 111320 * kx * 110540 / 1e6,8:0.00} km²"));
}

Console.WriteLine(FormattableString.Invariant($"naselja: {rezultat.Count}, ukupno preklapanje: {ukupnoPreklapanje:0.0} m²"));
File.WriteAllText(izlaz, JsonSerializer.Serialize(rezultat, new JsonSerializerOptions
{
    WriteIndented = true,
    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
}), new UTF8Encoding(false));
Console.WriteLine($"upisano: {izlaz}");

// --- pomoćno ---

Geometry Relacija(JsonElement rel)
{
    var linije = rel.GetProperty("members").EnumerateArray()
        .Where(m => m.GetProperty("type").GetString() == "way" && m.GetProperty("role").GetString() != "inner"
                    && m.TryGetProperty("geometry", out _))
        .Select(m => (Geometry)f.CreateLineString(m.GetProperty("geometry").EnumerateArray()
            .Select(p => new Coordinate(p.GetProperty("lon").GetDouble(), p.GetProperty("lat").GetDouble())).ToArray()))
        .ToList();
    var spojene = UnaryUnionOp.Union(linije); // čvorovi na spojevima, da Polygonizer zatvori prstenove
    var pz = new Polygonizer();
    pz.Add(spojene);
    var poligoni = pz.GetPolygons().ToList();
    if (poligoni.Count == 0) throw new Exception($"Relacija {rel.GetProperty("id")} nema zatvoren prsten.");
    // Polygonizer vraća i unutrašnje „rupe" kao poligone — unija spoljnih ih pokriva.
    return UnaryUnionOp.Union(poligoni);
}

// Samo poligonalni dijelovi, bez tankih ostataka presjeka.
Geometry Ocisti(Geometry g)
{
    var dijelovi = Enumerable.Range(0, g.NumGeometries).Select(g.GetGeometryN)
        .OfType<Polygon>().Where(p => p.Area > 1e-8).ToArray();
    return dijelovi.Length == 1 ? dijelovi[0] : f.CreateMultiPolygon(dijelovi);
}

static string Norm(string s)
{
    var d = s.Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
    var sb = new StringBuilder();
    foreach (var ch in d)
        if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
    var r = sb.ToString().ToLowerInvariant().Trim();
    return r.StartsWith("mz ") ? r[3..] : r;
}

sealed class Skala(double sx, double sy) : ICoordinateSequenceFilter
{
    public void Filter(CoordinateSequence seq, int i)
    {
        seq.SetX(i, seq.GetX(i) * sx);
        seq.SetY(i, seq.GetY(i) * sy);
    }
    public bool Done => false;
    public bool GeometryChanged => true;
}
