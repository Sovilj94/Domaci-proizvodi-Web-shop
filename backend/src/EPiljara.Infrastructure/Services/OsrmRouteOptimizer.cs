using System.Text;
using System.Text.Json;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.Features.Admin;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EPiljara.Infrastructure.Services;

/// <summary>
/// Računa redoslijed obilaska preko OSRM /trip endpointa, koji rješava problem
/// trgovačkog putnika i u istom odgovoru vraća kilometražu, trajanje i liniju rute.
///
/// Kad servis ne odgovori, vraća se na sortiranje po zonama — vozač dobije turu
/// koja radi, samo neoptimizovanu. Nikad prazan ekran zbog tuđeg servera.
/// </summary>
public class OsrmRouteOptimizer(
    HttpClient http,
    ZoneRouteOptimizer rezerva,
    IAppDbContext db,
    ILogger<OsrmRouteOptimizer> logger) : IRouteOptimizer
{
    public async Task<RouteOptimizationResult> OptimizeAsync(
        double startLat,
        double startLon,
        IReadOnlyList<RouteStopInput> stops,
        CancellationToken cancellationToken = default)
    {
        var pocetak = System.Diagnostics.Stopwatch.StartNew();

        // Optimizuju se samo stanice sa koordinatama; ostale idu na kraj.
        var saKoordinatama = stops
            .Where(s => s.Latitude.HasValue && s.Longitude.HasValue)
            .ToList();
        var bezKoordinata = stops
            .Where(s => !s.Latitude.HasValue || !s.Longitude.HasValue)
            .Select(s => s.OrderId)
            .ToList();

        // Ispod dvije tačke nema šta da se računa, i rezerva se namjerno NE zove:
        // po spec-u redoslijed tada ostaje onakav kakav ga je admin postavio.
        // Sortiranje po zonama je rezerva za neuspio poziv, ne za nedostatak podataka.
        if (saKoordinatama.Count < 2)
            return new RouteOptimizationResult(
                stops.Select(s => s.OrderId).ToList(), null, null, null);

        // Krajnja tačka se čita iz baze (a ne iz konfiguracije), pa je i pozivalac
        // koji već zna polazište ovdje ponovo pročita — jedan red, pročitan po
        // optimizaciji. Keširanje bi značilo da UPDATE na magacin ne proradi do
        // restarta, što je upravo ono što premještanje u bazu treba da izbjegne.
        var magacin = await MagacinCitac.UcitajAsync(db, cancellationToken);

        try
        {
            var tacke = new StringBuilder();
            tacke.Append(Broj(startLon)).Append(',').Append(Broj(startLat));
            foreach (var s in saKoordinatama)
                tacke.Append(';').Append(Broj(s.Longitude!.Value)).Append(',').Append(Broj(s.Latitude!.Value));
            // Magacin se šalje i kao krajnja tačka (destination=last), ne samo kao polazište
            // (source=first, startLat/startLon iznad — magacin kod admin dodjele, pozicija
            // vozača kod ručnog ponovnog računa). Mjereno na stvarnoj optimizaciji: ovo daje
            // identičan redoslijed i identičnu kilometražu kao ranije korišćeni roundtrip=true
            // kad je start magacin, ali za ponovni račun sa pozicije vozača tura sada ispravno
            // završava u magacinu umjesto da se petlja nazad na trenutnu poziciju vozača —
            // 15 od 24 slučaja u testu su se razlikovala, i u svih 15 je roundtrip=true davao
            // sporiju stvarnu rutu (prosjek ~6 min, najgore 18.6 min).
            tacke.Append(';').Append(Broj(magacin.Longitude)).Append(',').Append(Broj(magacin.Latitude));

            // roundtrip=false, uz i source=first I destination=last: OSRM /trip podržava samo
            // roundtrip=true (bilo koji source/destination) ili roundtrip=false uz oba fiksirana
            // kraja. source=first&roundtrip=false samostalno vraća code=NotImplemented (HTTP 400)
            // — nađeno tek kad je poziv stvarno stigao do OSRM-a (Task 9), jer je dotad odlazni
            // TCP iz .NET procesa u ovom okruženju bio blokiran i sve je tiho padalo na rezervu.
            // destination=last ovdje pinuje magacin (posljednja poslata tačka, iznad) kao kraj
            // ture — to je tačno ono što želimo, jer kombi uvijek završava u magacinu, bilo da
            // se optimizuje od magacina (admin dodjela) ili od pozicije vozača (ponovni račun).
            var url = $"trip/v1/driving/{tacke}"
                + "?source=first&destination=last&roundtrip=false&overview=simplified&geometries=geojson";

            using var response = await http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("OSRM vratio {Status}", (int)response.StatusCode);
                return await rezerva.OptimizeAsync(startLat, startLon, stops, cancellationToken);
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var kod = root.GetProperty("code").GetString();
            if (kod != "Ok")
            {
                logger.LogWarning("OSRM vratio code={Code} umjesto Ok", kod);
                return await rezerva.OptimizeAsync(startLat, startLon, stops, cancellationToken);
            }

            // waypoints[0] je start (source=first), posljednji je magacin (destination=last,
            // dodat iznad kao krajnja tačka); ostali nose waypoint_index — mjesto u obilasku.
            // Očekuje se tačno jedan waypoint po poslatoj tački, uključujući dodati magacin
            // (zato +2, ne +1); ako OSRM vrati manje (ili poremećen skup waypoint_index
            // vrijednosti), redoslijed bi bio nepotpun i tura bi se tiho izmiješala — bolje
            // rezerva nego pogrešna tura.
            var waypoints = root.GetProperty("waypoints");
            if (waypoints.GetArrayLength() != saKoordinatama.Count + 2)
            {
                logger.LogWarning(
                    "OSRM vratio {Broj} waypoint-a, očekivano {Ocekivano}",
                    waypoints.GetArrayLength(), saKoordinatama.Count + 2);
                return await rezerva.OptimizeAsync(startLat, startLon, stops, cancellationToken);
            }

            // Zadnji waypoint je dodati magacin (destination=last) — ne stanica, izbacuje se
            // iz redoslijeda isporuka gornjom granicom petlje (- 1).
            var poredak = new List<(int Mjesto, Guid OrderId)>();
            for (var i = 1; i < waypoints.GetArrayLength() - 1; i++)
            {
                var mjesto = waypoints[i].GetProperty("waypoint_index").GetInt32();
                poredak.Add((mjesto, saKoordinatama[i - 1].OrderId));
            }

            // destination=last pinuje magacin na waypoint_index = saKoordinatama.Count + 1
            // (posljednje mjesto u obilasku), pa stanicama i dalje ostaju tačno vrijednosti
            // 1..N — provjereno empirijski na stvarnoj optimizaciji, ne samo pretpostavljeno.
            var mjesta = poredak.Select(x => x.Mjesto).OrderBy(x => x).ToList();
            var ocekivanaMjesta = Enumerable.Range(1, saKoordinatama.Count).ToList();
            if (!mjesta.SequenceEqual(ocekivanaMjesta))
            {
                logger.LogWarning(
                    "OSRM waypoint_index vrijednosti nisu 1..{N} nego {Mjesta}",
                    saKoordinatama.Count, string.Join(',', mjesta));
                return await rezerva.OptimizeAsync(startLat, startLon, stops, cancellationToken);
            }

            var redoslijed = poredak
                .OrderBy(x => x.Mjesto)
                .Select(x => x.OrderId)
                .Concat(bezKoordinata)
                .ToList();

            // distance/duration (i geometrija ispod) sada opisuju start → stanice → magacin,
            // ne petlju nazad na start — kombi na kraju smjene ionako završava u magacinu,
            // bilo da se računa od magacina (admin dodjela) ili od pozicije vozača (ponovni
            // račun), pa je ovo tačan opis stvarne vožnje u oba slučaja.
            var trip = root.GetProperty("trips")[0];
            var duzina = trip.GetProperty("distance").GetDouble();
            var trajanje = trip.GetProperty("duration").GetDouble();
            var geometrija = trip.GetProperty("geometry").GetProperty("coordinates").GetRawText();

            // Jedini signal latencije otkad je HTTP logging za ovaj klijent ugašen
            // (vidi RemoveAllLoggers u DependencyInjection.cs) — namjerno bez koordinata.
            logger.LogInformation(
                "OSRM optimizacija uspjela za {BrojStanica} stanica za {Ms} ms",
                saKoordinatama.Count, pocetak.ElapsedMilliseconds);

            return new RouteOptimizationResult(redoslijed, duzina, trajanje, geometrija);
        }
        // Namjerno širok catch: tačan tip greške koju OSRM (ili mreža) može izazvati je
        // nepredvidiv — pogrešan tip vrijednosti u odgovoru, prazan trips niz, prekid
        // konekcije... Isto rezonovanje kao ZatraziObavjestenjaAsync u mobilnom repou:
        // tuđi servis koji se čudno ponaša ne smije koštati vozača cijele ture. Izuzetak
        // je jedino stvarno otkazivanje sa naše strane (cancellationToken) — to se
        // prosljeđuje dalje, umjesto da se pretvori u besmislen poziv rezerve.
        catch (Exception e) when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                e,
                "OSRM optimizacija nije uspjela poslije {Ms} ms, vraćam se na sortiranje po zonama",
                pocetak.ElapsedMilliseconds);
            return await rezerva.OptimizeAsync(startLat, startLon, stops, cancellationToken);
        }
    }

    // OSRM očekuje tačku kao decimalni razdjelnik. Na srpskoj lokalizaciji
    // ToString() bi dao zarez i URL bi se raspao na pogrešnom mjestu.
    private static string Broj(double v) =>
        v.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
}
