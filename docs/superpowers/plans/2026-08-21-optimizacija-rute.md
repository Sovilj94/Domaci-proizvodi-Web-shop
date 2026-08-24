# Optimizacija rute i navigacija u serijama — plan implementacije

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Sistem sam izračuna najbrži redoslijed obilaska iz magacina, prikaže vozaču liniju rute na mapi, i jednim dodirom pokrene navigaciju za cijelu seriju stanica.

**Architecture:** `ZoneRouteOptimizer` se zamjenjuje `OsrmRouteOptimizer`-om koji zove OSRM `/trip` — jedan poziv vraća redoslijed, kilometražu, trajanje i geometriju. Optimizacija se pokreće automatski pri dodjeli rute. Google Maps ne preslaže stanice, pa je naš redoslijed jedini izvor optimizacije; njemu se stanice šalju u serijama, jer jedan link prima najviše deset.

**Tech Stack:** ASP.NET Core 9 (MediatR, EF Core + PostgreSQL), OSRM `/trip` (besplatan, bez ključa), .NET MAUI `net10.0-android`, Leaflet, xUnit.

**Spec:** `docs/superpowers/specs/2026-08-21-optimizacija-rute-design.md`

## Global Constraints

- **Svi korisnički vidljivi tekstovi i API poruke su na srpskom.**
- **Kad OSRM ne odgovori, vraćamo se na sortiranje po zonama.** Vozač nikad ne smije ostati bez rute zbog spoljnog servisa.
- **Stanice bez koordinata se ne šalju optimizatoru.** Idu na kraj redoslijeda i ostaju u traci „Bez tačne lokacije".
- **Dvije rezerve su različite i ne smiju se pobrkati.** Kad poziv OSRM-u ne uspije iako imamo koordinate → sortiranje po zonama. Kad koordinata nema (manje od dvije tačke) → redoslijed ostaje onakav kakav ga je admin postavio, i ne zove se ništa.
- **Pozicija vozača se ne čuva.** Šalje se samo kao parametar ručne ponovne optimizacije, na izričit dodir, i nigdje se ne upisuje ni loguje.
- **Google Maps prima 9 međustanica kad link otvori aplikacija, a 3 kad ga otvori mobilni browser.** Serija se kroji prema tome, nikad naslijepo na deset.
- **Backend nema automatske testove, po dogovoru repoa.** Ne uvoditi test projekat; provjera je build + pozivanje API-ja. Mobilni repo ima xUnit i tamo se testira.
- **Mobilni DTO-ovi se prepisuju ručno i drže 1:1 sa backendom.** Zabranjena referenca na backend projekat.
- **`net10.0-android` i ništa drugo** za mobilni projekat.
- **Commit poslije svakog taska.**

## Dva repozitorija

| Repo | Putanja | Grana | Taskovi |
|---|---|---|---|
| **Backend** | `C:\Users\soviljn\Desktop\epiljara-backend-wt` | `driver-app-backend` | 1, 3, 4 |
| **Mobilni** | `C:\Users\soviljn\Desktop\EPiljaraVozac` | `main` | 5, 6, 7 |
| **Oba** | | | 2, 8 |

## Šta već postoji

- `RouteStopInput(Guid OrderId, string Address, string ZoneName, double? Latitude, double? Longitude)` — već nosi koordinate
- `IRouteOptimizer.OptimizeAsync(stops, ct) → Task<IReadOnlyList<Guid>>` — mijenja se u Tasku 1
- `ZoneRouteOptimizer` — sortira po `["Centar","Borik","Lauš","Starčevica"]`, ostaje kao rezerva
- `AssignRouteHandler` — pravi rutu sa `Sequence = i + 1` redom kojim je admin poslao
- `OptimizeRouteHandler` — pokreće se samo dugmetom
- `RouteStop.Sequence` — postoji, optimizacija ga prepisuje
- Mobilni: `map.html` sa `postaviStanice`, `postaviPoziciju`, `fokusiraj`; `RouteViewModel` sa `Aktivne`, `Zavrsene`, `TrenutnaStanica`, `PracenjeRadi`; `StopViewModel.NavigacijaAsync` gradi link za jednu adresu

---

### Task 1: Magacin i novi oblik optimizatora

**Files:**
- Modify: `backend/src/EPiljara.Application/Common/Interfaces/Services.cs`
- Modify: `backend/src/EPiljara.Infrastructure/Services/ZoneRouteOptimizer.cs`
- Modify: `backend/src/EPiljara.Application/Features/Admin/AdminRoutes.cs`
- Modify: `backend/src/EPiljara.API/appsettings.json`
- Modify: `backend/src/EPiljara.API/appsettings.Development.json`

**Interfaces:**
- Consumes: `RouteStopInput` (postoji)
- Produces:
  - `RouteOptimizationResult(IReadOnlyList<Guid> Order, double? DuzinaMetara, double? TrajanjeSekundi, string? Geometrija)`
  - `IRouteOptimizer.OptimizeAsync(double startLat, double startLon, IReadOnlyList<RouteStopInput> stops, CancellationToken ct = default) → Task<RouteOptimizationResult>`
  - konfiguraciona sekcija `Magacin:Latitude` i `Magacin:Longitude`

- [ ] **Step 1: Novi oblik rezultata i interfejsa**

U `backend/src/EPiljara.Application/Common/Interfaces/Services.cs` zamijeni `IRouteOptimizer` i dodaj rezultat:

```csharp
/// <summary>
/// Rezultat optimizacije. Kilometraža, trajanje i geometrija su nullable jer
/// rezervno sortiranje po zonama ne zna ništa o putevima — vraća samo redoslijed.
/// </summary>
public record RouteOptimizationResult(
    IReadOnlyList<Guid> Order,
    double? DuzinaMetara,
    double? TrajanjeSekundi,
    string? Geometrija);

/// <summary>
/// Računa redoslijed obilaska stanica, počev od zadatog polazišta.
/// Polazište je magacin, ili trenutna pozicija vozača kod ručne ponovne optimizacije.
/// </summary>
public interface IRouteOptimizer
{
    Task<RouteOptimizationResult> OptimizeAsync(
        double startLat,
        double startLon,
        IReadOnlyList<RouteStopInput> stops,
        CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Prilagodi ZoneRouteOptimizer novom obliku**

U `backend/src/EPiljara.Infrastructure/Services/ZoneRouteOptimizer.cs` zamijeni potpis i povratnu vrijednost. Logika sortiranja ostaje netaknuta:

```csharp
    public Task<RouteOptimizationResult> OptimizeAsync(
        double startLat,
        double startLon,
        IReadOnlyList<RouteStopInput> stops,
        CancellationToken cancellationToken = default)
    {
        var ordered = stops
            .Select((stop, index) => (stop, index))
            .OrderBy(x =>
            {
                var idx = Array.IndexOf(ZoneOrder, x.stop.ZoneName);
                return idx < 0 ? int.MaxValue : idx;
            })
            .ThenBy(x => x.index)
            .Select(x => x.stop.OrderId)
            .ToList();

        // Sortiranje po zonama ne zna razdaljine ni puteve, pa nema šta da javi
        // o kilometraži, trajanju i liniji. Polazište se namjerno ignoriše.
        return Task.FromResult(new RouteOptimizationResult(ordered, null, null, null));
    }
```

Ažuriraj i XML komentar iznad klase da više ne obećava Google Routes, nego kaže da je ovo rezerva kad OSRM ne odgovori.

- [ ] **Step 3: Prilagodi jedinog pozivaoca**

U `backend/src/EPiljara.Application/Features/Admin/AdminRoutes.cs`, u `OptimizeRouteHandler.Handle`, zamijeni poziv:

```csharp
        var rezultat = await optimizer.OptimizeAsync(
            magacin.Latitude, magacin.Longitude, inputs, ct);

        var sequence = 1;
        foreach (var orderId in rezultat.Order)
        {
            var stop = route.Stops.FirstOrDefault(s => s.OrderId == orderId);
            if (stop is not null) stop.Sequence = sequence++;
        }
```

Magacin se dobija kroz novu pomoćnu klasu — dodaj je u isti fajl, iznad `OptimizeRouteHandler`:

```csharp
/// <summary>Polazište tura. Jedna piljara, jedan magacin, pa stoji u konfiguraciji.</summary>
public record Magacin(double Latitude, double Longitude);
```

i ubrizgaj je u handler kroz `IOptions<Magacin>`; registracija ide u Tasku 3 zajedno sa OSRM servisom. Do tada handler neće moći da se razriješi — to je očekivano i popravlja se u Tasku 3.

- [ ] **Step 4: Dodaj konfiguraciju**

U `backend/src/EPiljara.API/appsettings.json`, uz ostale sekcije:

```json
  "Magacin": {
    "_napomena": "Polaziste svake ture. PLACEHOLDER — upisati stvarne koordinate magacina prije produkcije.",
    "Latitude": 44.7722,
    "Longitude": 17.1910
  }
```

Isto i u `appsettings.Development.json`.

- [ ] **Step 5: Build**

```bash
cd "C:/Users/soviljn/Desktop/epiljara-backend-wt/backend"
dotnet build
```

Expected: `Build succeeded`.

Ranija verzija ovog koraka je tvrdila da build pada zato što `Magacin` nije registrovan u DI. To je bilo netačno i ispravljeno je: `dotnet build` ne pravi DI kontejner, pa se nedostajuća registracija ne može ispoljiti kao greška prevođenja — ispolji se tek u vrijeme izvršavanja, kad zahtjev stigne do `OptimizeRouteHandler`. Registracija i dalje dolazi u Tasku 3; do tada je taj endpoint neupotrebljiv, ali se sve prevodi.

- [ ] **Step 6: Commit**

```bash
cd "C:/Users/soviljn/Desktop/epiljara-backend-wt"
git add backend/src
git commit -m "refactor(api): optimizer takes a start point and returns distance, duration and geometry"
```

---

### Task 2: Ruta pamti kilometražu, trajanje i liniju

**Files:**
- Modify: `backend/src/EPiljara.Domain/Entities/DeliveryRoute.cs`
- Create: `backend/src/EPiljara.Infrastructure/Persistence/Migrations/` (generisano)
- Modify: `backend/src/EPiljara.Application/DTOs/AdminDtos.cs`
- Modify: `backend/src/EPiljara.Application/Mapping/MappingExtensions.cs`
- Modify (drugi repo): `EPiljara.Driver/Models/Dtos.cs`
- Test (drugi repo): `EPiljara.Driver.Tests/ApiClientTests.cs`

**Interfaces:**
- Consumes: ništa
- Produces: `DeliveryRouteDto.DuzinaMetara`, `.TrajanjeSekundi`, `.Geometrija` — sva tri nullable, i na backendu i u mobilnom `Dtos.cs`

**Ovaj task dira oba repoa.** Koraci 1–5 u backend repou, 6–9 u mobilnom.

- [ ] **Step 1: Dodaj polja na entitet**

U `backend/src/EPiljara.Domain/Entities/DeliveryRoute.cs`, ispod `Status`:

```csharp
    /// <summary>
    /// Rezultat optimizacije. null kad optimizacija nije uspjela ili nije ni pokušana —
    /// ruta tada radi, samo bez linije i procjene.
    /// Geometrija je GeoJSON niz koordinata kao tekst, spreman za crtanje na mapi.
    /// </summary>
    public double? DuzinaMetara { get; set; }
    public double? TrajanjeSekundi { get; set; }
    public string? Geometrija { get; set; }
```

- [ ] **Step 2: Napravi migraciju**

```bash
cd "C:/Users/soviljn/Desktop/epiljara-backend-wt/backend"
dotnet tool restore
dotnet dotnet-ef migrations add RouteOptimizationResult -p src/EPiljara.Infrastructure -s src/EPiljara.Infrastructure -o Persistence/Migrations
```

- [ ] **Step 3: Provjeri migraciju**

Otvori novonastali `*_RouteOptimizationResult.cs`. Očekivano u `Up`: tri `AddColumn` na `DeliveryRoutes` — dva `double` nullable i jedan `string` nullable. Ako sadrži bilo šta drugo, **stani i javi**.

Napomena: build će i dalje padati zbog nedostajuće DI registracije iz Taska 1. To ne smeta `dotnet-ef` alatu jer on radi nad `EPiljara.Infrastructure` projektom. Ako migracija ne može da se generiše zbog toga, javi umjesto da zaobilaziš.

- [ ] **Step 4: Iznesi polja kroz DTO**

U `backend/src/EPiljara.Application/DTOs/AdminDtos.cs`, u `DeliveryRouteDto`, poslije `Status`:

```csharp
    /// <summary>Rezultat optimizacije; null kad je izostala.</summary>
    public double? DuzinaMetara { get; init; }
    public double? TrajanjeSekundi { get; init; }
    public string? Geometrija { get; init; }
```

U `backend/src/EPiljara.Application/Mapping/MappingExtensions.cs`, u `ToDto(this DeliveryRoute r)`, poslije `Status`:

```csharp
        DuzinaMetara = r.DuzinaMetara,
        TrajanjeSekundi = r.TrajanjeSekundi,
        Geometrija = r.Geometrija,
```

- [ ] **Step 5: Commit backend dijela**

```bash
cd "C:/Users/soviljn/Desktop/epiljara-backend-wt"
git add backend/src
git commit -m "feat(api): store and expose route distance, duration and geometry"
```

- [ ] **Step 6: Napiši test koji pada u mobilnom repou**

U `C:/Users/soviljn/Desktop/EPiljaraVozac/EPiljara.Driver.Tests/ApiClientTests.cs`, dodaj na kraj klase:

```csharp
    [Fact]
    public async Task Cita_kilometrazu_trajanje_i_geometriju_rute()
    {
        const string body = """
        {"data":{"id":"22222222-2222-2222-2222-222222222222","date":"2026-08-21","driverId":"33333333-3333-3333-3333-333333333333","driverName":"Mladen (kombi 1)","status":"Optimized","duzinaMetara":3686.9,"trajanjeSekundi":473.7,"geometrija":"[[17.191036,44.77218],[17.188304,44.769492]]","stops":[]},"meta":null}
        """;
        var client = Client(new StubHandler(HttpStatusCode.OK, body));

        var result = await client.SendAsync<DeliveryRouteDto>(Get());

        Assert.Equal(3686.9, result!.DuzinaMetara);
        Assert.Equal(473.7, result.TrajanjeSekundi);
        Assert.Equal("[[17.191036,44.77218],[17.188304,44.769492]]", result.Geometrija);
    }

    [Fact]
    public async Task Ruta_bez_optimizacije_daje_null_umjesto_nule()
    {
        const string body = """
        {"data":{"id":"22222222-2222-2222-2222-222222222222","date":"2026-08-21","driverId":"33333333-3333-3333-3333-333333333333","driverName":"Mladen (kombi 1)","status":"Planned","stops":[]},"meta":null}
        """;
        var client = Client(new StubHandler(HttpStatusCode.OK, body));

        var result = await client.SendAsync<DeliveryRouteDto>(Get());

        Assert.Null(result!.DuzinaMetara);
        Assert.Null(result.TrajanjeSekundi);
        Assert.Null(result.Geometrija);
    }
```

Drugi test je važan iz istog razloga kao kod koordinata: da su polja `double` umjesto `double?`, ruta bez optimizacije prijavila bi **0 km i 0 minuta** kao da je izračunata.

- [ ] **Step 7: Pokreni testove i vidi da padaju**

```bash
cd "C:/Users/soviljn/Desktop/EPiljaraVozac"
dotnet test EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj
```

Expected: FAIL — `DeliveryRouteDto` nema `DuzinaMetara`.

- [ ] **Step 8: Dodaj polja u mobilni DTO**

U `EPiljara.Driver/Models/Dtos.cs`, u `DeliveryRouteDto`, dodaj tri parametra **na kraj**:

```csharp
public record DeliveryRouteDto(
    Guid Id,
    string Date,
    Guid DriverId,
    string? DriverName,
    string Status,
    IReadOnlyList<RouteStopDto> Stops,
    double? DuzinaMetara,
    double? TrajanjeSekundi,
    string? Geometrija);
```

To lomi svaku gradnju `DeliveryRouteDto` u testovima. Pretraži `EPiljara.Driver.Tests` za sve pozive i dopuni ih sa `null` na kraju — u `RouteViewModelTests.cs` postoji pomoćna metoda `Route(...)`, a ima poziva i u drugim test fajlovima. Build testova pada dok ijedan ostane.

- [ ] **Step 9: Testovi, build, commit**

```bash
cd "C:/Users/soviljn/Desktop/EPiljaraVozac"
dotnet test EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj
cd EPiljara.Driver && dotnet build -f net10.0-android -c Debug
cd "C:/Users/soviljn/Desktop/EPiljaraVozac"
git add -A
git commit -m "feat: mirror route distance, duration and geometry"
```

Expected: 53 testa prolaze (51 postojeći + 2 nova), build uspijeva. Build traje oko 2 minuta — koristi velikodušan timeout (600000 ms).

---

### Task 3: OsrmRouteOptimizer

**Files:**
- Create: `backend/src/EPiljara.Infrastructure/Services/OsrmRouteOptimizer.cs`
- Modify: `backend/src/EPiljara.Infrastructure/DependencyInjection.cs`

**Interfaces:**
- Consumes: `IRouteOptimizer`, `RouteOptimizationResult`, `RouteStopInput` (Task 1)
- Produces: `OsrmRouteOptimizer` kao registrovana implementacija `IRouteOptimizer`; `Magacin` registrovan iz konfiguracije

- [ ] **Step 1: Napiši optimizator**

`backend/src/EPiljara.Infrastructure/Services/OsrmRouteOptimizer.cs`:

```csharp
using System.Text;
using System.Text.Json;
using EPiljara.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

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
    ILogger<OsrmRouteOptimizer> logger) : IRouteOptimizer
{
    public async Task<RouteOptimizationResult> OptimizeAsync(
        double startLat,
        double startLon,
        IReadOnlyList<RouteStopInput> stops,
        CancellationToken cancellationToken = default)
    {
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

        try
        {
            var tacke = new StringBuilder();
            tacke.Append(Broj(startLon)).Append(',').Append(Broj(startLat));
            foreach (var s in saKoordinatama)
                tacke.Append(';').Append(Broj(s.Longitude!.Value)).Append(',').Append(Broj(s.Latitude!.Value));

            var url = $"trip/v1/driving/{tacke}"
                + "?source=first&roundtrip=false&overview=simplified&geometries=geojson";

            using var response = await http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("OSRM vratio {Status}", (int)response.StatusCode);
                return await rezerva.OptimizeAsync(startLat, startLon, stops, cancellationToken);
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.GetProperty("code").GetString() != "Ok")
                return await rezerva.OptimizeAsync(startLat, startLon, stops, cancellationToken);

            // waypoints[0] je magacin; ostali nose waypoint_index — mjesto u obilasku.
            var waypoints = root.GetProperty("waypoints");
            var poredak = new List<(int Mjesto, Guid OrderId)>();
            for (var i = 1; i < waypoints.GetArrayLength(); i++)
            {
                var mjesto = waypoints[i].GetProperty("waypoint_index").GetInt32();
                poredak.Add((mjesto, saKoordinatama[i - 1].OrderId));
            }

            var redoslijed = poredak
                .OrderBy(x => x.Mjesto)
                .Select(x => x.OrderId)
                .Concat(bezKoordinata)
                .ToList();

            var trip = root.GetProperty("trips")[0];
            var duzina = trip.GetProperty("distance").GetDouble();
            var trajanje = trip.GetProperty("duration").GetDouble();
            var geometrija = trip.GetProperty("geometry").GetProperty("coordinates").GetRawText();

            return new RouteOptimizationResult(redoslijed, duzina, trajanje, geometrija);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException)
        {
            logger.LogWarning(e, "OSRM optimizacija nije uspjela, vraćam se na sortiranje po zonama");
            return await rezerva.OptimizeAsync(startLat, startLon, stops, cancellationToken);
        }
    }

    // OSRM očekuje tačku kao decimalni razdjelnik. Na srpskoj lokalizaciji
    // ToString() bi dao zarez i URL bi se raspao na pogrešnom mjestu.
    private static string Broj(double v) =>
        v.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
}
```

Redoslijed koordinata je **dužina pa širina** — obrnuto od svega ostalog u ovom projektu. To je OSRM-ov format i mora ostati tako; zamjena bi poslala tačke u Somaliju.

- [ ] **Step 2: Registruj servis i magacin**

**Prvo promijeni oblik `Magacin`-a.** Task 1 ga je napravio kao pozicioni record:

```csharp
public record Magacin(double Latitude, double Longitude);
```

To se **ne prevodi** sa `AddOptions`/`Configure`, jer oni imaju ograničenje `where TOptions : class, new()`, a pozicioni record nema konstruktor bez parametara. U `backend/src/EPiljara.Application/Features/Admin/AdminRoutes.cs` zamijeni ga oblikom koji prati kućni obrazac (`JwtOptions` u `JwtTokenService.cs`):

```csharp
/// <summary>Polazište tura. Jedna piljara, jedan magacin, pa stoji u konfiguraciji.
/// Obična klasa sa set svojstvima, ne pozicioni record — AddOptions traži new().</summary>
public class Magacin
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
```

Namjerno **bez podrazumijevanih vrijednosti**: `0,0` je usred Atlantika, a validacija ispod hvata izostanak konfiguracije glasno.

Zatim u `backend/src/EPiljara.Infrastructure/DependencyInjection.cs` zamijeni postojeću registraciju `IRouteOptimizer`:

```csharp
        services.AddOptions<EPiljara.Application.Features.Admin.Magacin>()
            .Bind(configuration.GetSection("Magacin"))
            // Bez koordinata magacina cijela ruta bi se racunala od 0°N 0°E, usred
            // Atlantika, a rezultat bi izgledao uvjerljivo. Bolje da se aplikacija
            // ne digne nego da tiho vozi iz pogresne tacke.
            .Validate(m => GeoBounds.Contains(m.Latitude, m.Longitude),
                "Magacin:Latitude i Magacin:Longitude moraju biti unutar Bosne i Hercegovine.")
            .ValidateOnStart();

        // Rezerva se registruje zasebno jer je OsrmRouteOptimizer prima kroz konstruktor.
        services.AddSingleton<ZoneRouteOptimizer>();
        services.AddHttpClient<IRouteOptimizer, OsrmRouteOptimizer>(c =>
        {
            c.BaseAddress = new Uri("https://router.project-osrm.org/");
            // Javni demo server; predstavljamo se kao i kod geokodiranja.
            c.DefaultRequestHeaders.UserAgent.ParseAdd("EPiljara/1.0 (dostava Banja Luka)");
            c.Timeout = TimeSpan.FromSeconds(20);
        });
```

Ako je postojala starija registracija `IRouteOptimizer` (npr. `AddSingleton<IRouteOptimizer, ZoneRouteOptimizer>()`), obriši je — inače posljednja registracija tiho pobjeđuje.

- [ ] **Step 3: Build**

```bash
cd "C:/Users/soviljn/Desktop/epiljara-backend-wt/backend"
dotnet build
```

Expected: `Build succeeded`. Greška o `Magacin` iz Taska 1 sada nestaje.

- [ ] **Step 4: Primijeni migraciju i provjeri optimizaciju uživo**

```bash
docker ps --format "{{.Names}}" | grep epiljara-postgres
cd "C:/Users/soviljn/Desktop/epiljara-backend-wt/backend"
dotnet dotnet-ef database update -p src/EPiljara.Infrastructure -s src/EPiljara.Infrastructure
dotnet run --project src/EPiljara.API --urls http://0.0.0.0:5080
```

U drugom terminalu, dodijeli rutu sa bar tri narudžbe koje imaju koordinate, pa pokreni optimizaciju:

```bash
API=http://localhost:5080
TOKEN=$(curl -s -X POST $API/auth/login -H "Content-Type: application/json" \
  -d '{"email":"admin@epiljara.ba","password":"admin123"}' | sed -n 's/.*"token":"\([^"]*\)".*/\1/p')
curl -s "$API/admin/routes?date=$(date +%F)" -H "Authorization: Bearer $TOKEN" | head -c 400
# uzmi id rute iz odgovora:
curl -s -X POST "$API/admin/routes/<ROUTE_ID>/optimize" -H "Authorization: Bearer $TOKEN" | head -c 500
```

Expected: `sequence` vrijednosti se razlikuju od redoslijeda kojim su narudžbe dodijeljene.

**`duzinaMetara`, `trajanjeSekundi` i `geometrija` će u ovom tasku biti `null` čak i kad OSRM odgovori, i to je očekivano.** `OptimizeRouteHandler` ih još ne upisuje na entitet — taj upis dolazi u Tasku 4, u `Optimizacija.PrimijeniAsync`. Ranija verzija ovog koraka je tražila da budu različiti od nule, što je bilo nedostižno i navelo bi te da tražiš nepostojeći kvar.

**Napomena o okruženju:** odlazni TCP iz .NET procesa je blokiran u ovom sandboxu (`WSAEACCES`), dok `curl` prolazi. Ako se to i dalje dešava, OSRM poziv će pasti i vratiće se sortiranje po zonama — što je i dalje koristan rezultat, jer dokazuje da rezerva radi. U tom slučaju to **izričito zapiši** i nemoj tvrditi da je OSRM provjeren.

Ugasi API proces prije nastavka.

- [ ] **Step 5: Commit**

```bash
cd "C:/Users/soviljn/Desktop/epiljara-backend-wt"
git add backend/src
git commit -m "feat(api): compute the visiting order with OSRM, falling back to the zone sort"
```

---

### Task 4: Automatska optimizacija i ponovni račun od pozicije vozača

**Files:**
- Modify: `backend/src/EPiljara.Application/Features/Admin/AdminRoutes.cs`
- Create: `backend/src/EPiljara.Application/Features/Driver/DriverReoptimize.cs`
- Modify: `backend/src/EPiljara.API/Controllers/DriverController.cs`
- Modify: `backend/src/EPiljara.API/Controllers/Requests.cs`

**Interfaces:**
- Consumes: `IRouteOptimizer`, `Magacin` (Task 3)
- Produces: `POST /driver/route/reoptimize` sa tijelom `{ latitude, longitude }` → `DeliveryRouteDto`

- [ ] **Step 1: Izdvoji zajedničko računanje u jednu metodu**

U `AdminRoutes.cs`, iznad `AssignRouteHandler`, dodaj:

```csharp
/// <summary>
/// Računa redoslijed i upisuje ga na stanice. Zatvorene stanice se ne diraju i
/// zadržavaju svoje brojeve; otvorene se prenumerišu počev od broja za jedan većeg
/// od najvećeg zatvorenog, da se brojevi ne bi sudarili i pokvarili sječenje na serije.
/// </summary>
internal static class Optimizacija
{
    private static readonly HashSet<OrderStatus> Zatvoreni =
        [OrderStatus.Delivered, OrderStatus.FailedDelivery, OrderStatus.Cancelled];

    public static async Task PrimijeniAsync(
        DeliveryRoute route, double startLat, double startLon,
        IRouteOptimizer optimizer, CancellationToken ct)
    {
        var otvorene = route.Stops
            .Where(s => s.Order is null || !Zatvoreni.Contains(s.Order.Status))
            .ToList();

        if (otvorene.Count < 2) return;

        var inputs = otvorene.Select(s => new RouteStopInput(
            s.OrderId,
            s.Order?.DeliveryAddress ?? string.Empty,
            s.Order?.DeliveryZone?.Name ?? string.Empty,
            s.Order?.Latitude,
            s.Order?.Longitude)).ToList();

        var rezultat = await optimizer.OptimizeAsync(startLat, startLon, inputs, ct);

        var pocetak = route.Stops
            .Where(s => s.Order is not null && Zatvoreni.Contains(s.Order.Status))
            .Select(s => s.Sequence)
            .DefaultIfEmpty(0)
            .Max() + 1;

        foreach (var orderId in rezultat.Order)
        {
            var stop = route.Stops.FirstOrDefault(s => s.OrderId == orderId);
            if (stop is not null) stop.Sequence = pocetak++;
        }

        route.DuzinaMetara = rezultat.DuzinaMetara;
        route.TrajanjeSekundi = rezultat.TrajanjeSekundi;
        route.Geometrija = rezultat.Geometrija;

        // Status "Optimized" samo kad je stvarno optimizovano. Rezerva po zonama
        // vraća redoslijed ali nema kilometražu — kad bismo i tada pisali Optimized,
        // ništa u sistemu ne bi razlikovalo pravu rutu od degradirane.
        if (rezultat.DuzinaMetara.HasValue) route.Status = RouteStatus.Optimized;
    }
}
```

- [ ] **Step 2: Pokreni optimizaciju odmah pri dodjeli rute**

U `AssignRouteHandler`, dopuni konstruktor sa `IRouteOptimizer optimizer, IOptions<Magacin> magacin`, pa poslije prvog `SaveChangesAsync` dodaj:

```csharp
        // Ponovo učitaj sa narudžbama, jer optimizacija treba koordinate i statuse.
        var saStavkama = await db.DeliveryRoutes
            .Include(r => r.Stops).ThenInclude(s => s.Order).ThenInclude(o => o!.DeliveryZone)
            .FirstAsync(r => r.Id == route.Id, ct);

        await Optimizacija.PrimijeniAsync(
            saStavkama, magacin.Value.Latitude, magacin.Value.Longitude, optimizer, ct);
        await db.SaveChangesAsync(ct);
```

- [ ] **Step 3: Prepiši OptimizeRouteHandler da koristi istu metodu**

U istom fajlu zamijeni cijelo tijelo `OptimizeRouteHandler.Handle` ovim. Logika prenumeracije se **ne duplira** — živi samo u `Optimizacija`:

```csharp
    public async Task<DeliveryRouteDto> Handle(OptimizeRouteCommand cmd, CancellationToken ct)
    {
        var route = await db.DeliveryRoutes
            .Include(r => r.Stops).ThenInclude(s => s.Order).ThenInclude(o => o!.DeliveryZone)
            .FirstOrDefaultAsync(r => r.Id == cmd.RouteId, ct)
            ?? throw new NotFoundException("Ruta nije pronađena.");

        await Optimizacija.PrimijeniAsync(
            route, magacin.Value.Latitude, magacin.Value.Longitude, optimizer, ct);
        await db.SaveChangesAsync(ct);

        return await LoadRouteDtoAsync(db, route.Id, ct);
    }
```

Konstruktor handlera postaje `OptimizeRouteHandler(IAppDbContext db, IRouteOptimizer optimizer, IOptions<Magacin> magacin)`. Ako se stvarni naziv komande ili polja razlikuje od `OptimizeRouteCommand.RouteId`, zadrži postojeći naziv i zapiši stvarni oblik u izvještaju.

- [ ] **Step 4: Endpoint za ponovni račun od pozicije vozača**

`backend/src/EPiljara.Application/Features/Driver/DriverReoptimize.cs`:

```csharp
using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Features.Admin;
using EPiljara.Application.Mapping;
using EPiljara.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Driver;

/// <summary>
/// Ponovo računa redoslijed preostalih stanica od mjesta gdje je vozač sada.
/// Pozicija se koristi samo za ovaj račun i nigdje se ne upisuje ni loguje.
/// </summary>
public record ReoptimizeRouteCommand(double Latitude, double Longitude) : IRequest<DeliveryRouteDto>;

public class ReoptimizeRouteHandler(IAppDbContext db, ICurrentUser current, IRouteOptimizer optimizer)
    : IRequestHandler<ReoptimizeRouteCommand, DeliveryRouteDto>
{
    public async Task<DeliveryRouteDto> Handle(ReoptimizeRouteCommand cmd, CancellationToken ct)
    {
        if (!GeoBounds.Contains(cmd.Latitude, cmd.Longitude))
            throw new DomainException(GeoBounds.OutsideMessage, "invalid_coordinates");

        var driver = await db.Drivers.FirstOrDefaultAsync(d => d.UserId == current.Id, ct)
            ?? throw new ForbiddenException("Niste registrovani kao vozač.");

        var danas = DateOnly.FromDateTime(DateTime.UtcNow);
        var route = await db.DeliveryRoutes
            .Include(r => r.Driver)
            .Include(r => r.Stops).ThenInclude(s => s.Order).ThenInclude(o => o!.DeliveryZone)
            .FirstOrDefaultAsync(r => r.DriverId == driver.Id && r.Date == danas, ct)
            ?? throw new NotFoundException("Nemate rutu za danas.");

        await Optimizacija.PrimijeniAsync(route, cmd.Latitude, cmd.Longitude, optimizer, ct);
        await db.SaveChangesAsync(ct);

        return route.ToDto();
    }
}
```

`GeoBounds` već postoji iz Faze 2a i odbija tačke izvan Bosne i Hercegovine — ista zaštita koja hvata zamijenjene koordinate.

- [ ] **Step 5: Izloži rutu**

U `backend/src/EPiljara.API/Controllers/DriverController.cs`:

```csharp
    [HttpPost("driver/route/reoptimize")]
    public async Task<IActionResult> Reoptimize([FromBody] ReoptimizeRequest body) =>
        Envelope(await Mediator.Send(new ReoptimizeRouteCommand(body.Latitude, body.Longitude)));
```

U `Requests.cs`:

```csharp
public record ReoptimizeRequest(double Latitude, double Longitude);
```

- [ ] **Step 6: Build i provjera uživo**

```bash
cd "C:/Users/soviljn/Desktop/epiljara-backend-wt/backend"
dotnet build
```

Uz pokrenut API, dodijeli novu rutu i provjeri da `sequence` **odmah** nije redoslijed kojim su narudžbe poslate. Zatim kao vozač:

```bash
DRIVER=$(curl -s -X POST $API/auth/login -H "Content-Type: application/json" \
  -d '{"email":"vozac@epiljara.ba","password":"vozac123"}' | sed -n 's/.*"token":"\([^"]*\)".*/\1/p')
curl -s -X POST $API/driver/route/reoptimize -H "Authorization: Bearer $DRIVER" \
  -H "Content-Type: application/json" -d '{"latitude":44.7800,"longitude":17.2000}' | head -c 400
curl -s -X POST $API/driver/route/reoptimize -H "Authorization: Bearer $DRIVER" \
  -H "Content-Type: application/json" -d '{"latitude":17.19,"longitude":44.77}' | head -c 200
```

Expected: prvi poziv `200` sa izmijenjenim redoslijedom; drugi `400` sa `invalid_coordinates`, jer je par zamijenjen i pada izvan BiH.

Ako su neke stanice već zatvorene, provjeri u bazi da su zadržale svoje brojeve, a otvorene dobile veće.

- [ ] **Step 7: Commit**

```bash
cd "C:/Users/soviljn/Desktop/epiljara-backend-wt"
git add backend/src
git commit -m "feat(api): optimize on assignment, and let a driver re-optimize from their position"
```

---

### Task 5: Linija rute na mapi

**Files:**
- Modify: `EPiljara.Driver/Resources/Raw/map.html`
- Modify: `EPiljara.Driver/ViewModels/RouteViewModel.cs`
- Modify: `EPiljara.Driver/Views/RoutePage.xaml.cs`
- Modify: `EPiljara.Driver/Views/RoutePage.xaml`
- Test: `EPiljara.Driver.Tests/RouteViewModelTests.cs`

**Interfaces:**
- Consumes: `DeliveryRouteDto.Geometrija`, `.DuzinaMetara`, `.TrajanjeSekundi` (Task 2)
- Produces:
  - `map.html` funkcija `postaviRutu(base64Json)` — prima base64 JSON niza `[[širina, dužina], ...]`
  - `RouteViewModel.RutaTreba` → `event Action<string>?`
  - `RouteViewModel.Sazetak` → `string` (npr. `"3,7 km · 8 min"`, prazno kad nema procjene)
  - `RouteViewModel.GeoJsonULeaflet(string?) → IReadOnlyList<double[]>` — javna, čista, testabilna

- [ ] **Step 1: Napiši test za zamjenu koordinata**

Ovo je jedina prava zamka ovog taska: GeoJSON nosi `[dužina, širina]`, a Leaflet očekuje `[širina, dužina]`. Zamjena se radi **u C#-u**, ne u JavaScriptu, upravo zato da se može testirati.

U `EPiljara.Driver.Tests/RouteViewModelTests.cs` dodaj:

```csharp
    [Fact]
    public void GeoJson_koordinate_se_okrecu_u_leaflet_redoslijed()
    {
        // GeoJSON: [dužina, širina]. Leaflet: [širina, dužina].
        // Banja Luka je 44.77 N, 17.19 E — ako izađe obrnuto, pin je u Somaliji.
        var rezultat = RouteViewModel.GeoJsonULeaflet("[[17.1910,44.7722],[17.2000,44.7800]]");

        Assert.Equal(2, rezultat.Count);
        Assert.Equal(44.7722, rezultat[0][0]);
        Assert.Equal(17.1910, rezultat[0][1]);
        Assert.Equal(44.7800, rezultat[1][0]);
        Assert.Equal(17.2000, rezultat[1][1]);
    }

    [Fact]
    public void Prazna_ili_neispravna_geometrija_daje_praznu_listu()
    {
        Assert.Empty(RouteViewModel.GeoJsonULeaflet(null));
        Assert.Empty(RouteViewModel.GeoJsonULeaflet(""));
        Assert.Empty(RouteViewModel.GeoJsonULeaflet("ovo nije json"));
    }

    [Fact]
    public void Sazetak_pise_kilometre_i_minute()
    {
        var vm = new RouteViewModel(null!, null!, null!);

        vm.Primijeni(new DeliveryRouteDto(
            Guid.NewGuid(), "2026-08-21", Guid.NewGuid(), "Mladen", "Optimized",
            [], 3686.9, 473.7, null));

        Assert.Equal("3,7 km · 8 min", vm.Sazetak);
    }

    [Fact]
    public void Sazetak_je_prazan_kad_optimizacija_nije_uspjela()
    {
        var vm = new RouteViewModel(null!, null!, null!);

        vm.Primijeni(new DeliveryRouteDto(
            Guid.NewGuid(), "2026-08-21", Guid.NewGuid(), "Mladen", "Planned",
            [], null, null, null));

        Assert.Equal(string.Empty, vm.Sazetak);
    }
```

- [ ] **Step 2: Pokreni testove i vidi da padaju**

```bash
cd "C:/Users/soviljn/Desktop/EPiljaraVozac"
dotnet test EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj
```

Expected: FAIL — `GeoJsonULeaflet` i `Sazetak` ne postoje.

- [ ] **Step 3: Dodaj pretvaranje i sažetak u RouteViewModel**

U `RouteViewModel` dodaj:

```csharp
    [ObservableProperty] private string _sazetak = string.Empty;

    /// <summary>Javlja stranici da nacrta liniju rute; nosi base64 JSON za postaviRutu().</summary>
    public event Action<string>? RutaTreba;

    /// <summary>
    /// GeoJSON nosi [dužina, širina], Leaflet očekuje [širina, dužina].
    /// Zamjena je ovdje, a ne u JavaScriptu, da bi se mogla testirati.
    /// Neispravan ulaz daje praznu listu — linija se onda ne crta, ruta i dalje radi.
    /// </summary>
    public static IReadOnlyList<double[]> GeoJsonULeaflet(string? geoJson)
    {
        if (string.IsNullOrWhiteSpace(geoJson)) return [];
        try
        {
            var tacke = JsonSerializer.Deserialize<double[][]>(geoJson);
            if (tacke is null) return [];
            return tacke
                .Where(t => t.Length >= 2)
                .Select(t => new[] { t[1], t[0] })
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }
```

U `Primijeni`, poslije `OsvjeziMapu(stops)`, dodaj:

```csharp
        Sazetak = route?.DuzinaMetara is { } m && route.TrajanjeSekundi is { } s
            ? $"{m / 1000:0.#} km · {Math.Round(s / 60)} min"
            : string.Empty;

        var linija = GeoJsonULeaflet(route?.Geometrija);
        RutaTreba?.Invoke(Convert.ToBase64String(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(linija))));
```

Prazna lista se šalje namjerno — tako mapa obriše staru liniju kad nove nema.

- [ ] **Step 4: Dodaj crtanje u map.html**

U `EPiljara.Driver/Resources/Raw/map.html`, uz postojeće funkcije:

```javascript
  var slojRute = L.layerGroup().addTo(mapa);

  function postaviRutu(base64) {
    slojRute.clearLayers();
    var tacke = JSON.parse(decodeURIComponent(escape(atob(base64))));
    if (!tacke || tacke.length < 2) return;
    L.polyline(tacke, { color: '#512BD4', weight: 5, opacity: 0.7 }).addTo(slojRute);
  }
```

Koordinate stižu već okrenute iz C#-a, pa se ovdje **ne diraju**.

- [ ] **Step 5: Poveži događaj u RoutePage**

U `EPiljara.Driver/Views/RoutePage.xaml.cs` dodaj drugi keš uz `_cekaNaMapu`:

```csharp
    private string? _cekaRuta;
```

pretplati se u konstruktoru, odmah ispod `_vm.MapaTreba += PosaljiStanice;`:

```csharp
        _vm.RutaTreba += PosaljiRutu;
```

dodaj primaoca uz `PosaljiStanice`:

```csharp
    /// <summary>Ista pravila kao za stanice: payload se pamti i ne briše poslije slanja,
    /// jer se WebView može ponovo učitati i tada <c>Navigated</c> mora imati šta poslati.</summary>
    private void PosaljiRutu(string base64)
    {
        _cekaRuta = base64;
        if (_mapaSpremna) IsprazniRed();
    }
```

i proširi `IsprazniRed` da šalje oba:

```csharp
    private void IsprazniRed()
    {
        var stanice = _cekaNaMapu;
        var ruta = _cekaRuta;
        if (stanice is null && ruta is null) return;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                if (stanice is not null)
                    await Mapa.EvaluateJavaScriptAsync($"postaviStanice('{stanice}')");
                if (ruta is not null)
                    await Mapa.EvaluateJavaScriptAsync($"postaviRutu('{ruta}')");
            }
            catch (Exception ex)
            {
                // Mapa (još) nije spremna; sljedeće Navigated ili osvježavanje će je popuniti.
                System.Diagnostics.Debug.WriteLine($"[RoutePage] slanje na mapu nije uspjelo: {ex}");
            }
        });
    }
```

Oba keša se čitaju u lokalne promjenljive prije prelaska na glavnu nit, da ih `Primijeni` ne bi zamijenio usred slanja.

- [ ] **Step 6: Prikaži sažetak na kartici**

U `EPiljara.Driver/Views/RoutePage.xaml`, na kartici trenutne stanice, ispod `Napredak`:

```xml
                <Label Text="{Binding Sazetak}" FontSize="13" TextColor="Gray"
                       IsVisible="{Binding Sazetak, Converter={StaticResource IsStringNotEmpty}}" />
```

- [ ] **Step 7: Testovi, build, commit**

```bash
cd "C:/Users/soviljn/Desktop/EPiljaraVozac"
dotnet test EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj
cd EPiljara.Driver && dotnet build -f net10.0-android -c Debug
cd "C:/Users/soviljn/Desktop/EPiljaraVozac"
git add -A
git commit -m "feat: draw the route line and show distance and duration"
```

Expected: 57 testova prolazi (53 + 4 nova), build uspijeva.

---

### Task 6: Navigacija u serijama

**Files:**
- Create: `EPiljara.Driver/Services/NavigacijaLink.cs`
- Create: `EPiljara.Driver.Tests/NavigacijaLinkTests.cs`
- Modify: `EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj`
- Modify: `EPiljara.Driver/ViewModels/RouteViewModel.cs`
- Modify: `EPiljara.Driver/Views/RoutePage.xaml`

**Interfaces:**
- Consumes: `RouteStopDto` (postoji)
- Produces:
  - `NavigacijaLink.MaxStanica(bool mapsInstaliran) → int` (10 ili 4)
  - `NavigacijaLink.Serija(IEnumerable<RouteStopDto> otvorene, int max) → IReadOnlyList<RouteStopDto>`
  - `NavigacijaLink.Napravi(IReadOnlyList<RouteStopDto> serija) → string`
  - `RouteViewModel.NavigacijaNatpis` → `string`
  - komanda `NavigacijaSerijaCommand`

- [ ] **Step 1: Poveži novi fajl u test projekat**

U `EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj`, uz ostale linkove:

```xml
    <Compile Include="..\EPiljara.Driver\Services\NavigacijaLink.cs" Link="Services\NavigacijaLink.cs" />
```

- [ ] **Step 2: Napiši testove**

`EPiljara.Driver.Tests/NavigacijaLinkTests.cs`:

```csharp
using EPiljara.Driver.Models;
using EPiljara.Driver.Services;

namespace EPiljara.Driver.Tests;

public class NavigacijaLinkTests
{
    private static RouteStopDto Stanica(int redni, string adresa = "Ulica") => new(
        OrderId: Guid.NewGuid(),
        OrderCode: $"EP-2608-{redni:D4}",
        CustomerName: $"Kupac {redni}",
        Address: $"{adresa} {redni}",
        ZoneName: "Centar",
        Sequence: redni,
        CustomerPhone: null,
        Status: "Confirmed",
        Latitude: 44.77,
        Longitude: 17.19);

    private static List<RouteStopDto> Stanice(int koliko) =>
        Enumerable.Range(1, koliko).Select(i => Stanica(i)).ToList();

    [Fact]
    public void Sa_instaliranim_google_maps_serija_je_deset()
    {
        Assert.Equal(10, NavigacijaLink.MaxStanica(mapsInstaliran: true));
    }

    [Fact]
    public void Bez_google_maps_serija_je_cetiri()
    {
        // Mobilni browser prima samo tri međustanice plus odredište.
        Assert.Equal(4, NavigacijaLink.MaxStanica(mapsInstaliran: false));
    }

    [Fact]
    public void Serija_uzima_prvih_deset_kad_ih_ima_vise()
    {
        var serija = NavigacijaLink.Serija(Stanice(24), 10);

        Assert.Equal(10, serija.Count);
        Assert.Equal(1, serija[0].Sequence);
        Assert.Equal(10, serija[9].Sequence);
    }

    [Fact]
    public void Serija_uzima_sve_kad_ih_je_manje_od_maksimuma()
    {
        Assert.Equal(3, NavigacijaLink.Serija(Stanice(3), 10).Count);
    }

    [Fact]
    public void Serija_postuje_redoslijed_po_sequence_a_ne_redoslijed_u_listi()
    {
        var izmijesane = new List<RouteStopDto> { Stanica(5), Stanica(2), Stanica(9), Stanica(1) };

        var serija = NavigacijaLink.Serija(izmijesane, 10);

        Assert.Equal([1, 2, 5, 9], serija.Select(s => s.Sequence));
    }

    [Fact]
    public void Link_nosi_odrediste_i_medjustanice_u_redoslijedu()
    {
        var link = NavigacijaLink.Napravi(NavigacijaLink.Serija(Stanice(3), 10));

        Assert.StartsWith("https://www.google.com/maps/dir/?api=1", link);
        Assert.Contains("travelmode=driving", link);
        // Posljednja stanica je odredište, prethodne su međustanice.
        Assert.Contains("destination=Ulica%203", link);
        Assert.Contains("waypoints=Ulica%201%7CUlica%202", link);
    }

    [Fact]
    public void Link_za_jednu_stanicu_nema_medjustanica()
    {
        var link = NavigacijaLink.Napravi(NavigacijaLink.Serija(Stanice(1), 10));

        Assert.Contains("destination=Ulica%201", link);
        Assert.DoesNotContain("waypoints=", link);
    }

    [Fact]
    public void Prazna_serija_daje_prazan_link()
    {
        Assert.Equal(string.Empty, NavigacijaLink.Napravi([]));
    }
}
```

- [ ] **Step 3: Pokreni testove i vidi da padaju**

```bash
cd "C:/Users/soviljn/Desktop/EPiljaraVozac"
dotnet test EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj
```

Expected: FAIL — `NavigacijaLink` ne postoji.

- [ ] **Step 4: Napiši NavigacijaLink**

`EPiljara.Driver/Services/NavigacijaLink.cs`:

```csharp
using EPiljara.Driver.Models;

namespace EPiljara.Driver.Services;

/// <summary>
/// Gradi Google Maps link za seriju stanica. Google ne preslaže međustanice —
/// vozi ih redom kojim mu se daju — pa je naš izračunati redoslijed jedini
/// izvor optimizacije. Čista logika, bez platforme, testira se bez telefona.
/// </summary>
public static class NavigacijaLink
{
    /// <summary>
    /// Koliko naših stanica stane u jedan link. Google dozvoljava devet međustanica
    /// kad link otvori njihova aplikacija, a samo tri u mobilnom browseru — plus
    /// odredište u oba slučaja. Bez provjere bi telefon bez Google Maps-a tiho
    /// izgubio šest stanica iz serije od deset.
    /// </summary>
    public static int MaxStanica(bool mapsInstaliran) => mapsInstaliran ? 10 : 4;

    /// <summary>Prvih <paramref name="max"/> stanica po Sequence — prozor u već izračunatu rutu.</summary>
    public static IReadOnlyList<RouteStopDto> Serija(IEnumerable<RouteStopDto> otvorene, int max) =>
        otvorene.OrderBy(s => s.Sequence).Take(max).ToList();

    /// <summary>Posljednja stanica u seriji je odredište, prethodne su međustanice.</summary>
    public static string Napravi(IReadOnlyList<RouteStopDto> serija)
    {
        if (serija.Count == 0) return string.Empty;

        static string Tacka(RouteStopDto s) => Uri.EscapeDataString($"{s.Address}, Banja Luka");

        var odrediste = Tacka(serija[^1]);
        var link = "https://www.google.com/maps/dir/?api=1&travelmode=driving"
                 + $"&destination={odrediste}";

        if (serija.Count > 1)
        {
            var medju = string.Join("%7C", serija.Take(serija.Count - 1).Select(Tacka));
            link += $"&waypoints={medju}";
        }

        return link;
    }
}
```

`%7C` je znak `|` kojim Google razdvaja međustanice; ne smije se dodatno escape-ovati.

Testovi očekuju `destination=Ulica%203` — adresa se escape-uje zajedno sa `, Banja Luka`, pa provjeri da tvrdnja `Assert.Contains` i dalje prolazi na tom obliku. Ako escape daje drugačiji rezultat nego što test očekuje, **popravi test da odgovara stvarnom izlazu, a ne obrnuto** — i zapiši u izvještaju šta je stvarni oblik.

- [ ] **Step 5: Dodaj komandu i natpis u RouteViewModel**

```csharp
    [ObservableProperty] private string _navigacijaNatpis = "Navigacija";

    private static bool GoogleMapsInstaliran()
    {
#if ANDROID
        try
        {
            var pm = global::Android.App.Application.Context.PackageManager;
            return pm?.GetLaunchIntentForPackage("com.google.android.apps.maps") is not null;
        }
        catch (Exception) { return false; }
#else
        return true;
#endif
    }

    private void OsvjeziNavigacijuNatpis()
    {
        var max = NavigacijaLink.MaxStanica(GoogleMapsInstaliran());
        NavigacijaNatpis = Aktivne.Count > max
            ? $"Navigacija ({max} od {Aktivne.Count})"
            : "Navigacija";
    }

    [RelayCommand]
    private async Task NavigacijaSerijaAsync()
    {
        var max = NavigacijaLink.MaxStanica(GoogleMapsInstaliran());
        var link = NavigacijaLink.Napravi(NavigacijaLink.Serija(Aktivne, max));
        if (link.Length == 0)
        {
            Error = "Nema otvorenih stanica.";
            return;
        }

        Error = null;
        await Launcher.OpenAsync(link);
    }
```

Pozovi `OsvjeziNavigacijuNatpis();` na kraju `Primijeni`.

- [ ] **Step 6: Dodaj dugme**

U `RoutePage.xaml`, na kartici trenutne stanice, ispod „Otvori stanicu":

```xml
                    <Button Text="{Binding NavigacijaNatpis}"
                            Command="{Binding NavigacijaSerijaCommand}" />
```

- [ ] **Step 7: Testovi, build, commit**

```bash
cd "C:/Users/soviljn/Desktop/EPiljaraVozac"
dotnet test EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj
cd EPiljara.Driver && dotnet build -f net10.0-android -c Debug
cd "C:/Users/soviljn/Desktop/EPiljaraVozac"
git add -A
git commit -m "feat: open a whole batch of stops in Google Maps"
```

Expected: 65 testova prolazi (57 + 8 novih), build uspijeva.

---

### Task 7: Dugme za ponovni račun od trenutne pozicije

**Files:**
- Modify: `EPiljara.Driver/Services/DriverService.cs`
- Modify: `EPiljara.Driver/ViewModels/RouteViewModel.cs`
- Modify: `EPiljara.Driver/Views/RoutePage.xaml`

**Interfaces:**
- Consumes: `POST /driver/route/reoptimize` (Task 4); `LocationTracker` (Faza 2b)
- Produces: `DriverService.ReoptimizeAsync(double lat, double lon) → Task<DeliveryRouteDto?>`; komanda `PreracunajCommand`

- [ ] **Step 1: Dodaj poziv u DriverService**

```csharp
    /// <summary>Ponovo računa redoslijed od zadate pozicije. Pozicija se na serveru ne čuva.</summary>
    public Task<DeliveryRouteDto?> ReoptimizeAsync(double lat, double lon)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "driver/route/reoptimize")
        {
            Content = JsonContent.Create(new { latitude = lat, longitude = lon }),
        };
        return api.SendAsync<DeliveryRouteDto>(request);
    }
```

- [ ] **Step 2: Zapamti posljednju poziciju i dodaj komandu**

U `RouteViewModel`, u `NaPomjeraj`, zapamti poziciju u polja `_zadnjaSirina` i `_zadnjaDuzina` (obična `double?` polja, ne observable). Zatim:

```csharp
    [RelayCommand]
    private async Task PreracunajAsync()
    {
        if (_zadnjaSirina is not { } lat || _zadnjaDuzina is not { } lon)
        {
            Error = "Lokacija još nije poznata.";
            return;
        }

        if (IsRefreshing) return;
        IsRefreshing = true;
        Error = null;
        try
        {
            Primijeni(await driver.ReoptimizeAsync(lat, lon));
        }
        catch (ApiException e)
        {
            Error = e.Message;
        }
        finally
        {
            IsRefreshing = false;
        }
    }
```

- [ ] **Step 3: Dodaj dugme, vidljivo samo kad praćenje radi**

U `RoutePage.xaml`, uz dugme „Osvježi":

```xml
                <Button Text="Preračunaj od moje lokacije" FontSize="13"
                        Command="{Binding PreracunajCommand}"
                        IsVisible="{Binding PracenjeRadi}"
                        IsEnabled="{Binding IsRefreshing, Converter={StaticResource InvertedBool}}" />
```

`PracenjeRadi` je iz Faze 2b i tačna je zavisnost: bez žive lokacije dugme nema šta da pošalje.

- [ ] **Step 4: Testovi, build, commit**

```bash
cd "C:/Users/soviljn/Desktop/EPiljaraVozac"
dotnet test EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj
cd EPiljara.Driver && dotnet build -f net10.0-android -c Debug
cd "C:/Users/soviljn/Desktop/EPiljaraVozac"
git add -A
git commit -m "feat: let the driver re-optimize from their current position"
```

Expected: 65 testova i dalje prolaze, build uspijeva.

---

### Task 8: Provjera na emulatoru

**Files:** nijedan — ovo je provjera.

- [ ] **Step 1: Podigni okruženje ispravnim redoslijedom**

**Prvo Docker, pa emulator** — obrnuto obori emulatorov NAT, a pomaže samo hladno pokretanje (`-no-snapshot-load`).

```bash
docker compose up -d postgres
cd "C:/Users/soviljn/Desktop/epiljara-backend-wt/backend"
dotnet run --project src/EPiljara.API --urls http://0.0.0.0:5080
# drugi terminal:
"$LOCALAPPDATA/Android/Sdk/emulator/emulator.exe" -avd epiljara_api35
cd "C:/Users/soviljn/Desktop/EPiljaraVozac/EPiljara.Driver"
dotnet build -f net10.0-android -c Debug -t:Run
```

- [ ] **Step 2: Napravi rutu sa najmanje 12 stanica**

Kroz admin panel dodijeli 12 ili više narudžbi vozaču **Mladen (kombi 1)** za današnji datum, od kojih bar deset ima koordinate. Onima koje nemaju postavi ih dugmetom „Bez tačne lokacije — postavi".

Dvanaest je namjerno: tek preko deset se vidi da serije rade.

- [ ] **Step 3: Provjeri liniju i sažetak**

Prijavi se kao `vozac@epiljara.ba` / `vozac123`.

Expected: linija rute ide **preko ulica**, a ne kao prava crta između pinova. Na kartici piše kilometraža i vrijeme. Brojevi na pinovima prate izračunati redoslijed, a ne redoslijed kojim je admin dodijelio.

- [ ] **Step 4: Provjeri serije**

Prvo utvrdi šta emulator uopšte ima, jer od toga zavisi tačan očekivani natpis:

```bash
adb shell pm list packages | grep com.google.android.apps.maps
```

- Ako paket **postoji** → veličina serije je 10, natpis je **„Navigacija (10 od 12)"**.
- Ako paket **ne postoji** → veličina serije je 4, natpis je **„Navigacija (4 od 12)"**, i link se otvara u browseru.

Oba ishoda su prolaz. Ono što **ne smije** da se desi jeste natpis „10 od 12" na uređaju bez Google Maps-a — to bi značilo da provjera instaliranosti ne radi i da bi šest stanica tiho ispalo iz serije.

Zapiši koji je slučaj bio, jer drugi slučaj ostaje neprovjeren do testa na stvarnom telefonu (stavka iz produkcijske liste).

Dodir na dugme otvara stanice u tačnom redoslijedu, sa posljednjom kao odredištem.

Vrati se u aplikaciju, zatvori tri stanice (kroz popup ili ekran stanice), pa ponovo tapni dugme.

Expected: ostaje devet otvorenih stanica. Sa Google Maps-om natpis pada na samo „Navigacija" i svih devet ide u jedan link; bez njega natpis je „Navigacija (4 od 9)". U oba slučaja serija kreće od **prve još otvorene** stanice, ne od one koja je bila prva na početku ture.

- [ ] **Step 5: Provjeri preračunavanje od pozicije**

```bash
adb emu geo fix 17.2000 44.7800
```

Tapni „Preračunaj od moje lokacije".

Expected: redoslijed i linija se mijenjaju, zatvorene stanice zadržavaju svoje brojeve, a otvorene dobijaju veće. Sažetak se osvježava.

Zatim provjeri i rub: bez dozvole za lokaciju dugme se **ne vidi**.

- [ ] **Step 6: Provjeri rezervu**

Ugasi backendu pristup internetu (ili privremeno pokvari `BaseAddress` OSRM klijenta u konfiguraciji na nepostojeći host), dodijeli novu rutu.

Expected: ruta se i dalje pravi, `sequence` prati sortiranje po zonama, sažetak je prazan, linije nema — i **ništa ne puca**.

- [ ] **Step 7: Zabilježi nalaze**

Zapiši šta je prošlo a šta nije. Ako neka provjera padne, to je nalaz — javi, ne prepravljaj plan.

---

## Prije produkcije

Ovaj plan namjerno ostavlja nekoliko stvari nedovršenim jer su za razvoj ispravne, a za produkciju nisu. Puna tabela sa obrazloženjima je u spec-u pod „Šta moraš uraditi prije produkcije"; ukratko:

| Stavka | Gdje |
|---|---|
| Zamijeniti OSRM demo server sopstvenom instancom ili OpenRouteService-om | `DependencyInjection.cs`, `BaseAddress` — Task 3 |
| Upisati stvarne koordinate magacina umjesto placeholder-a | `appsettings.json`, sekcija `Magacin` — Task 1 |
| Zamijeniti `https://api.example.invalid` | `appsettings.Release.json` u mobilnom repou — naslijeđeno iz Faze 2a |
| Provjeriti seriju od četiri na telefonu **bez** Google Maps-a | Task 8 Step 4 pokriva samo onaj slučaj koji emulator ima |
| Zamijeniti Nominatim za geokodiranje | naslijeđeno iz Faze 2a |
| Provjeriti foreground servis na stvarnom telefonu | naslijeđeno iz Faze 2b, `NotSticky` |

Nijedna od ovih stavki ne blokira taskove iz ovog plana.

## Šta ovaj plan namjerno ne radi

Navigacija skretanje-po-skretanje u našoj aplikaciji, vremenski prozori dostave, kapacitet kombija, više vozača u jednoj optimizaciji, i čuvanje ili praćenje pozicije vozača. Sve je to van obima po spec-u.
