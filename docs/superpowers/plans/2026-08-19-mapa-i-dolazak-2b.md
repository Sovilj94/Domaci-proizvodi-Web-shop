# Mapa i automatski popup na dolasku (Faza 2, dio 2b) — plan implementacije

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Vozač otvori aplikaciju i vidi mapu sa svojim stanicama i svojom pozicijom; kad se približi adresi, popup mu sam iskoči i ponudi da pozove kupca i potvrdi primopredaju.

**Architecture:** Mapa je Leaflet u `WebView`-u, sa OpenStreetMap pločicama — bez API ključa i bez Google naloga. Detekcija dolaska nije Android Geofencing API nego obično računanje razdaljine na svakom GPS očitanju, izdvojeno u čistu funkciju koja se testira bez telefona. Praćenje lokacije ide kroz `Geolocation.StartListeningForegroundAsync` uz Android foreground servis, da sistem ne uspava aplikaciju dok vozač vozi.

**Tech Stack:** .NET MAUI (`net10.0-android`), CommunityToolkit.Mvvm, Leaflet 1.9.4 (spakovan lokalno), OpenStreetMap pločice, xUnit.

**Spec:** `docs/superpowers/specs/2026-08-18-mapa-i-dolazak-design.md`

## Global Constraints

- **Svi korisnički vidljivi tekstovi su na srpskom.**
- **`net10.0-android` i ništa drugo.** Nikad ne dodavati drugi TFM.
- **Koordinate su nullable.** Stanica bez koordinata nema pin i ne okida popup — prikazuje se u traci ispod mape i zatvara ručno. Nikad ne crtati pin za `null`.
- **Pozicija vozača se ne šalje na server.** Ostaje na telefonu, koristi se samo za mapu i razdaljinu.
- **Leaflet se pakuje u aplikaciju**, ne povlači se sa CDN-a.
- **Odbijena dozvola za lokaciju ne smije zaustaviti rad** — mapa i dalje radi, popup se pokreće ručnim dugmetom „Stigao sam".
- **Radijus dolaska: 150 m. Očitanja sa tačnošću lošijom od 100 m se odbacuju.**
- **Commit poslije svakog taska.**

## Repozitorij

Sav rad je u mobilnom repou: `C:\Users\soviljn\Desktop\EPiljaraVozac`, grana `main`.
Backend se **ne dira** — `RouteStopDto` već nosi `Latitude`/`Longitude` iz dijela 2a.

## Šta već postoji

- `Models/Dtos.cs`: `RouteStopDto(Guid OrderId, string OrderCode, string CustomerName, string Address, string ZoneName, int Sequence, string? CustomerPhone, string Status, double? Latitude, double? Longitude)`
- `Services/DriverService.cs`: `GetRouteAsync(string? date = null)`, `UpdateStopStatusAsync(Guid orderId, string status)`
- `ViewModels/RouteViewModel.cs`: `Aktivne`, `Zavrsene`, `Napredak`, `NemaRute`, `TuraGotova`, `IsRefreshing`, `Error`, `Primijeni(DeliveryRouteDto?)`, `UcitajAsync()`, `OtvoriStanicuAsync(stop)`
- `ViewModels/StopViewModel.cs` i `Views/StopPage.xaml` — ekran stanice, **ostaje netaknut**
- Test projekat `EPiljara.Driver.Tests` (25 testova) linkuje izvorne fajlove preko `<Compile Include/Link>`

## Struktura fajlova

```
EPiljara.Driver/
  Services/
    Geo.cs                     NOVO  Haversine razdaljina
    ArrivalDetector.cs         NOVO  čista odluka "je li stigao"
    LocationTracker.cs         NOVO  omotač oko Geolocation + dozvole
  Resources/Raw/
    leaflet.js                 NOVO  spakovan Leaflet 1.9.4
    leaflet.css                NOVO
    map.html                   NOVO  mapa + most prema C#
  Views/
    RoutePage.xaml(.cs)        MIJENJA se: lista → mapa
  ViewModels/
    RouteViewModel.cs          MIJENJA se: pinovi, trenutna stanica, popup
  Platforms/Android/
    AndroidManifest.xml        MIJENJA se: dozvole za lokaciju
    LocationForegroundService.cs  NOVO
EPiljara.Driver.Tests/
  GeoTests.cs                  NOVO
  ArrivalDetectorTests.cs      NOVO
```

---

### Task 1: Razdaljina i odluka o dolasku

Ovo je jedini dio 2b koji nosi stvarni rizik za tačnost, i jedini koji se može testirati bez telefona. Zato ide prvi.

**Files:**
- Create: `EPiljara.Driver/Services/Geo.cs`
- Create: `EPiljara.Driver/Services/ArrivalDetector.cs`
- Create: `EPiljara.Driver.Tests/GeoTests.cs`
- Create: `EPiljara.Driver.Tests/ArrivalDetectorTests.cs`
- Modify: `EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj`

**Interfaces:**
- Consumes: `RouteStopDto` iz `Models/Dtos.cs`
- Produces:
  - `Geo.RazdaljinaMetara(double lat1, double lon1, double lat2, double lon2) → double`
  - `ArrivalDetector` sa `Provjeri(double lat, double lon, double? tacnostMetara, IEnumerable<RouteStopDto> stanice) → RouteStopDto?`
  - `ArrivalDetector.Zaboravi(Guid orderId)` — skida stanicu sa liste već javljenih
  - konstante `ArrivalDetector.RadijusMetara = 150`, `ArrivalDetector.NajgoraTacnostMetara = 100`

- [ ] **Step 1: Poveži nove fajlove u test projekat**

U `EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj`, u postojeći `<ItemGroup>` sa `<Compile Include/Link>` stavkama, dodaj:

```xml
    <Compile Include="..\EPiljara.Driver\Services\Geo.cs" Link="Services\Geo.cs" />
    <Compile Include="..\EPiljara.Driver\Services\ArrivalDetector.cs" Link="Services\ArrivalDetector.cs" />
```

- [ ] **Step 2: Napiši testove za razdaljinu**

`EPiljara.Driver.Tests/GeoTests.cs`:

```csharp
using EPiljara.Driver.Services;

namespace EPiljara.Driver.Tests;

public class GeoTests
{
    // Trg Krajine i Banski dvor u Banjoj Luci — oko 210 m vazdušne linije.
    private const double TrgLat = 44.7722, TrgLon = 17.1910;
    private const double DvorLat = 44.7705, DvorLon = 17.1901;

    [Fact]
    public void Ista_tacka_daje_nulu()
    {
        Assert.Equal(0, Geo.RazdaljinaMetara(TrgLat, TrgLon, TrgLat, TrgLon), 1);
    }

    [Fact]
    public void Dvije_tacke_u_Banjoj_Luci_daju_ocekivanu_razdaljinu()
    {
        var m = Geo.RazdaljinaMetara(TrgLat, TrgLon, DvorLat, DvorLon);
        Assert.InRange(m, 180, 240);
    }

    [Fact]
    public void Razdaljina_je_simetricna()
    {
        var ab = Geo.RazdaljinaMetara(TrgLat, TrgLon, DvorLat, DvorLon);
        var ba = Geo.RazdaljinaMetara(DvorLat, DvorLon, TrgLat, TrgLon);
        Assert.Equal(ab, ba, 6);
    }

    [Fact]
    public void Jedan_stepen_geografske_sirine_je_oko_111_kilometara()
    {
        var m = Geo.RazdaljinaMetara(44.0, 17.0, 45.0, 17.0);
        Assert.InRange(m, 111_000, 111_400);
    }

    [Fact]
    public void Zamijenjene_koordinate_vode_na_sasvim_drugo_mjesto()
    {
        // Banja Luka je 44.77 N, 17.19 E. Ako neko zamijeni argumente, tačka
        // odleti na 17.19 N, 44.77 E — u Arabijsko more, hiljadama kilometara daleko.
        // Ovo nije provjera Haversine formule nego brana protiv zamjene argumenata.
        var odletjelo = Geo.RazdaljinaMetara(TrgLat, TrgLon, TrgLon, TrgLat);

        Assert.True(odletjelo > 1_000_000, $"očekivano preko 1000 km, dobijeno {odletjelo:F0} m");
    }
}
```

- [ ] **Step 3: Napiši testove za detekciju dolaska**

`EPiljara.Driver.Tests/ArrivalDetectorTests.cs`:

```csharp
using EPiljara.Driver.Models;
using EPiljara.Driver.Services;

namespace EPiljara.Driver.Tests;

public class ArrivalDetectorTests
{
    private const double CiljLat = 44.7722, CiljLon = 17.1910;

    private static RouteStopDto Stanica(
        int sequence = 1,
        string status = "Confirmed",
        double? lat = CiljLat,
        double? lon = CiljLon,
        Guid? id = null) => new(
        OrderId: id ?? Guid.NewGuid(),
        OrderCode: $"EP-2608-{sequence:D4}",
        CustomerName: $"Kupac {sequence}",
        Address: $"Ulica {sequence}",
        ZoneName: "Centar",
        Sequence: sequence,
        CustomerPhone: "+387 65 000 000",
        Status: status,
        Latitude: lat,
        Longitude: lon);

    [Fact]
    public void Javlja_stanicu_kad_je_vozac_u_krugu()
    {
        var d = new ArrivalDetector();
        var s = Stanica();

        var pogodak = d.Provjeri(CiljLat, CiljLon, 10, [s]);

        Assert.Equal(s.OrderId, pogodak?.OrderId);
    }

    [Fact]
    public void Ne_javlja_kad_je_vozac_predaleko()
    {
        var d = new ArrivalDetector();

        // ~1.1 km sjevernije
        var pogodak = d.Provjeri(CiljLat + 0.01, CiljLon, 10, [Stanica()]);

        Assert.Null(pogodak);
    }

    [Fact]
    public void Istu_stanicu_ne_javlja_dvaput()
    {
        var d = new ArrivalDetector();
        var s = Stanica();

        Assert.NotNull(d.Provjeri(CiljLat, CiljLon, 10, [s]));
        Assert.Null(d.Provjeri(CiljLat, CiljLon, 10, [s]));
    }

    [Fact]
    public void Ponovo_se_naoruza_kad_vozac_izadje_pa_se_vrati()
    {
        var d = new ArrivalDetector();
        var s = Stanica();

        Assert.NotNull(d.Provjeri(CiljLat, CiljLon, 10, [s]));
        d.Provjeri(CiljLat + 0.01, CiljLon, 10, [s]);   // izašao iz kruga
        Assert.NotNull(d.Provjeri(CiljLat, CiljLon, 10, [s]));
    }

    [Fact]
    public void Preskace_zatvorene_stanice()
    {
        var d = new ArrivalDetector();

        Assert.Null(d.Provjeri(CiljLat, CiljLon, 10, [Stanica(status: "Delivered")]));
        Assert.Null(d.Provjeri(CiljLat, CiljLon, 10, [Stanica(status: "FailedDelivery")]));
        Assert.Null(d.Provjeri(CiljLat, CiljLon, 10, [Stanica(status: "Cancelled")]));
    }

    [Fact]
    public void Preskace_stanice_bez_koordinata()
    {
        var d = new ArrivalDetector();

        Assert.Null(d.Provjeri(CiljLat, CiljLon, 10, [Stanica(lat: null, lon: null)]));
    }

    [Fact]
    public void Odbacuje_netacna_ocitanja()
    {
        var d = new ArrivalDetector();

        // Tačnost 250 m je gora od praga od 100 m — očitanju se ne vjeruje.
        Assert.Null(d.Provjeri(CiljLat, CiljLon, 250, [Stanica()]));
    }

    [Fact]
    public void Ocitanje_bez_podatka_o_tacnosti_se_prihvata()
    {
        var d = new ArrivalDetector();

        Assert.NotNull(d.Provjeri(CiljLat, CiljLon, null, [Stanica()]));
    }

    [Fact]
    public void Kad_su_dvije_stanice_u_krugu_javlja_blizu()
    {
        var d = new ArrivalDetector();
        var daleka = Stanica(sequence: 1, lat: CiljLat + 0.001, lon: CiljLon);  // ~111 m
        var bliza = Stanica(sequence: 2, lat: CiljLat, lon: CiljLon);           // 0 m

        var pogodak = d.Provjeri(CiljLat, CiljLon, 10, [daleka, bliza]);

        Assert.Equal(bliza.OrderId, pogodak?.OrderId);
    }

    [Fact]
    public void Zaboravi_dozvoljava_ponovno_javljanje()
    {
        var d = new ArrivalDetector();
        var s = Stanica();

        Assert.NotNull(d.Provjeri(CiljLat, CiljLon, 10, [s]));
        d.Zaboravi(s.OrderId);
        Assert.NotNull(d.Provjeri(CiljLat, CiljLon, 10, [s]));
    }
}
```

- [ ] **Step 4: Pokreni testove i vidi da padaju**

```bash
cd "C:/Users/soviljn/Desktop/EPiljaraVozac"
dotnet test EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj
```

Expected: FAIL — `Geo` i `ArrivalDetector` ne postoje.

- [ ] **Step 5: Napiši Geo**

`EPiljara.Driver/Services/Geo.cs`:

```csharp
namespace EPiljara.Driver.Services;

/// <summary>Geografski račun. Bez ijedne zavisnosti od platforme — testira se bez telefona.</summary>
public static class Geo
{
    private const double PoluprecnikZemljeMetara = 6_371_000;

    /// <summary>
    /// Razdaljina po površini Zemlje, Haversine formulom. Redoslijed je
    /// (širina, dužina) — zamjena argumenata daje rezultat pogrešan za stotine kilometara.
    /// </summary>
    public static double RazdaljinaMetara(double lat1, double lon1, double lat2, double lon2)
    {
        var fi1 = URadijanima(lat1);
        var fi2 = URadijanima(lat2);
        var deltaFi = URadijanima(lat2 - lat1);
        var deltaLambda = URadijanima(lon2 - lon1);

        var a = Math.Sin(deltaFi / 2) * Math.Sin(deltaFi / 2)
                + Math.Cos(fi1) * Math.Cos(fi2)
                * Math.Sin(deltaLambda / 2) * Math.Sin(deltaLambda / 2);

        return PoluprecnikZemljeMetara * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double URadijanima(double stepeni) => stepeni * Math.PI / 180;
}
```

- [ ] **Step 6: Napiši ArrivalDetector**

`EPiljara.Driver/Services/ArrivalDetector.cs`:

```csharp
using EPiljara.Driver.Models;

namespace EPiljara.Driver.Services;

/// <summary>
/// Odlučuje da li je vozač stigao na neku stanicu. Čista logika, bez GPS-a i bez UI-ja,
/// pa se cio tok dolaska testira bez telefona i bez vožnje.
/// Pamti kome je već javila, da isti popup ne iskače u petlji dok vozač stoji pred kućom.
/// </summary>
public class ArrivalDetector
{
    /// <summary>Bliže od ovoga smatra se dolaskom.</summary>
    public const double RadijusMetara = 150;

    /// <summary>Očitanjima lošijim od ovoga se ne vjeruje — inače popup iskače pred pogrešnom zgradom.</summary>
    public const double NajgoraTacnostMetara = 100;

    /// <summary>Naoružavanje se vraća tek kad vozač izađe ovoliko daleko, da treperenje GPS-a
    /// oko same granice ne bi palilo popup više puta.</summary>
    private const double IzlazMetara = RadijusMetara * 1.5;

    private static readonly HashSet<string> Zatvoreni =
        new(["Delivered", "FailedDelivery", "Cancelled"], StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<Guid> _vecJavljene = [];

    /// <summary>Vraća stanicu na koju je vozač upravo stigao, ili null.</summary>
    public RouteStopDto? Provjeri(
        double lat, double lon, double? tacnostMetara, IEnumerable<RouteStopDto> stanice)
    {
        if (tacnostMetara is > NajgoraTacnostMetara) return null;

        RouteStopDto? najbliza = null;
        var najblizaRazdaljina = double.MaxValue;

        foreach (var s in stanice)
        {
            if (s.Latitude is not { } sLat || s.Longitude is not { } sLon) continue;
            if (Zatvoreni.Contains(s.Status)) continue;

            var razdaljina = Geo.RazdaljinaMetara(lat, lon, sLat, sLon);

            // Naoružaj ponovo čim se dovoljno udalji.
            if (razdaljina > IzlazMetara) _vecJavljene.Remove(s.OrderId);

            if (razdaljina > RadijusMetara) continue;
            if (_vecJavljene.Contains(s.OrderId)) continue;

            if (razdaljina < najblizaRazdaljina)
            {
                najblizaRazdaljina = razdaljina;
                najbliza = s;
            }
        }

        if (najbliza is not null) _vecJavljene.Add(najbliza.OrderId);
        return najbliza;
    }

    /// <summary>Skida stanicu sa liste već javljenih — koristi se kad vozač zatvori popup
    /// bez radnje, pa treba da mu se javi ponovo ako se vrati.</summary>
    public void Zaboravi(Guid orderId) => _vecJavljene.Remove(orderId);
}
```

- [ ] **Step 7: Pokreni testove i potvrdi da prolaze**

```bash
cd "C:/Users/soviljn/Desktop/EPiljaraVozac"
dotnet test EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj
```

Expected: `Passed! - Failed: 0, Passed: 40` (25 postojećih + 5 Geo + 10 ArrivalDetector).

- [ ] **Step 8: Build i commit**

```bash
cd "C:/Users/soviljn/Desktop/EPiljaraVozac/EPiljara.Driver"
dotnet build -f net10.0-android -c Debug
cd "C:/Users/soviljn/Desktop/EPiljaraVozac"
git add -A
git commit -m "feat: haversine distance and arrival detection"
```

Build traje oko 2 minuta — koristi velikodušan timeout (600000 ms).

---

### Task 2: Leaflet mapa kao lokalni resurs

**Files:**
- Create: `EPiljara.Driver/Resources/Raw/leaflet.js`
- Create: `EPiljara.Driver/Resources/Raw/leaflet.css`
- Create: `EPiljara.Driver/Resources/Raw/map.html`

**Interfaces:**
- Consumes: ništa
- Produces: HTML stranica koja izlaže tri JS funkcije koje C# poziva:
  - `postaviStanice(base64Json)` — crta pinove
  - `postaviPoziciju(lat, lon)` — crta plavu tačku vozača
  - `fokusiraj(lat, lon)` — centrira mapu
  I jedan kanal nazad: klik na pin vodi na `app://stop/{orderId}`.

- [ ] **Step 1: Preuzmi Leaflet**

```bash
cd "C:/Users/soviljn/Desktop/EPiljaraVozac/EPiljara.Driver/Resources/Raw"
curl -sL -o leaflet.js https://unpkg.com/leaflet@1.9.4/dist/leaflet.js
curl -sL -o leaflet.css https://unpkg.com/leaflet@1.9.4/dist/leaflet.css
ls -la leaflet.js leaflet.css
```

Expected: `leaflet.js` oko 145 KB, `leaflet.css` oko 15 KB. Ako je neki fajl manji od 10 KB, preuzimanje nije uspjelo — provjeri sadržaj prije nego što nastaviš.

Napomena: `.NET` procesi u ovom okruženju nemaju izlaz na internet, ali `curl` ima. Zato se preuzima ovako, a ne iz koda.

- [ ] **Step 2: Napiši map.html**

`EPiljara.Driver/Resources/Raw/map.html`:

```html
<!doctype html>
<html lang="sr">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no">
<link rel="stylesheet" href="leaflet.css">
<style>
  html, body, #mapa { height: 100%; margin: 0; padding: 0; }
  .pin {
    background: #512BD4; color: #fff; border: 2px solid #fff; border-radius: 50%;
    width: 28px; height: 28px; line-height: 24px; text-align: center;
    font: bold 14px sans-serif; box-shadow: 0 1px 4px rgba(0,0,0,.4);
  }
  .pin.gotova { background: #9e9e9e; }
</style>
</head>
<body>
<div id="mapa"></div>
<script src="leaflet.js"></script>
<script>
  var BANJA_LUKA = [44.7722, 17.1910];
  var mapa = L.map('mapa', { zoomControl: false, attributionControl: false }).setView(BANJA_LUKA, 13);
  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { maxZoom: 19 }).addTo(mapa);

  var slojStanica = L.layerGroup().addTo(mapa);
  var pozicija = null;

  // Klik na pin je jedini kanal nazad prema C#-u: promjena lokacije na app:// šemu
  // koju WebView-ov Navigating događaj presretne i otkaže.
  function otvoriStanicu(id) { window.location.href = 'app://stop/' + id; }

  function postaviStanice(base64) {
    var stanice = JSON.parse(decodeURIComponent(escape(atob(base64))));
    slojStanica.clearLayers();
    var tacke = [];

    stanice.forEach(function (s) {
      if (s.lat === null || s.lon === null) return;
      var ikona = L.divIcon({
        className: '',
        html: '<div class="pin' + (s.gotova ? ' gotova' : '') + '">' + s.redni + '</div>',
        iconSize: [28, 28], iconAnchor: [14, 14]
      });
      L.marker([s.lat, s.lon], { icon: ikona })
        .addTo(slojStanica)
        .on('click', function () { otvoriStanicu(s.id); });
      tacke.push([s.lat, s.lon]);
    });

    if (pozicija) tacke.push(pozicija.getLatLng());
    if (tacke.length === 1) mapa.setView(tacke[0], 15);
    else if (tacke.length > 1) mapa.fitBounds(L.latLngBounds(tacke), { padding: [40, 40] });
  }

  function postaviPoziciju(lat, lon) {
    if (!pozicija) {
      pozicija = L.circleMarker([lat, lon], {
        radius: 8, color: '#fff', weight: 2, fillColor: '#1a73e8', fillOpacity: 1
      }).addTo(mapa);
    } else {
      pozicija.setLatLng([lat, lon]);
    }
  }

  function fokusiraj(lat, lon) { mapa.setView([lat, lon], 16); }
</script>
</body>
</html>
```

`decodeURIComponent(escape(atob(...)))` je namjerno: `atob` sam ne razumije UTF-8, pa bi imena kupaca sa č, ć, š, ž stigla izlomljena.

- [ ] **Step 3: Potvrdi da su fajlovi spakovani kao MauiAsset**

`EPiljara.Driver.csproj` već sadrži `<MauiAsset Include="Resources\Raw\**" ... />`, pa se nova tri fajla pakuju sama. Potvrdi:

```bash
cd "C:/Users/soviljn/Desktop/EPiljaraVozac/EPiljara.Driver"
grep -n "MauiAsset" EPiljara.Driver.csproj
```

Expected: red sa `Resources\Raw\**`. Ako ga nema, dodaj ga — bez toga se HTML ne nađe u aplikaciji.

- [ ] **Step 4: Build i commit**

```bash
cd "C:/Users/soviljn/Desktop/EPiljaraVozac/EPiljara.Driver"
dotnet build -f net10.0-android -c Debug
cd "C:/Users/soviljn/Desktop/EPiljaraVozac"
git add -A
git commit -m "feat: bundle leaflet and the map page"
```

---

### Task 3: Ekran rute postaje mapa

**Files:**
- Modify: `EPiljara.Driver/ViewModels/RouteViewModel.cs`
- Modify: `EPiljara.Driver/Views/RoutePage.xaml`
- Modify: `EPiljara.Driver/Views/RoutePage.xaml.cs`

**Interfaces:**
- Consumes: `map.html` iz Taska 2; `RouteStopDto` sa `Latitude`/`Longitude`
- Produces:
  - `RouteViewModel.TrenutnaStanica` → `RouteStopDto?` (prva nezatvorena po `Sequence`)
  - `RouteViewModel.BezLokacije` → `ObservableCollection<RouteStopDto>` (aktivne bez koordinata)
  - `RouteViewModel.ImaBezLokacije` → `bool`
  - `RouteViewModel.StaniceZaMapu` → `string` (base64 JSON koji ide u `postaviStanice`)
  - događaj `RouteViewModel.MapaTreba` → `event Action<string>?` koji stranica sluša

- [ ] **Step 1: Dopuni RouteViewModel**

U `EPiljara.Driver/ViewModels/RouteViewModel.cs` dodaj na vrh `using System.Text;` i `using System.Text.Json;`, pa unutar klase:

```csharp
    /// <summary>Aktivne stanice kojima nedostaju koordinate — nemaju pin, zatvaraju se ručno.</summary>
    public ObservableCollection<RouteStopDto> BezLokacije { get; } = [];

    [ObservableProperty] private RouteStopDto? _trenutnaStanica;
    [ObservableProperty] private bool _imaBezLokacije;

    /// <summary>Javlja stranici da mapu treba prekrojiti; nosi base64 JSON za postaviStanice().</summary>
    public event Action<string>? MapaTreba;

    /// <summary>Oblik koji map.html očekuje. Namjerno kratka imena — ide kroz JS most.</summary>
    private record MapaStanica(string id, int redni, double? lat, double? lon, bool gotova);

    private void OsvjeziMapu(IEnumerable<RouteStopDto> sve)
    {
        var zaMapu = sve.Select(s => new MapaStanica(
            s.OrderId.ToString(),
            s.Sequence,
            s.Latitude,
            s.Longitude,
            Zatvoreni.Contains(s.Status))).ToList();

        var json = JsonSerializer.Serialize(zaMapu);
        MapaTreba?.Invoke(Convert.ToBase64String(Encoding.UTF8.GetBytes(json)));
    }
```

- [ ] **Step 2: Proširi Primijeni**

U istoj klasi, u metodi `Primijeni`, poslije reda `TuraGotova = stops.Count > 0 && Aktivne.Count == 0;` dodaj:

```csharp
        TrenutnaStanica = Aktivne.FirstOrDefault();

        BezLokacije.Clear();
        foreach (var s in Aktivne.Where(s => s.Latitude is null || s.Longitude is null))
            BezLokacije.Add(s);
        ImaBezLokacije = BezLokacije.Count > 0;

        OsvjeziMapu(stops);
```

`stops` se prosljeđuje cijelo, ne samo aktivne — zatvorene stanice ostaju na mapi kao sive, da vozač vidi šta je već odradio.

- [ ] **Step 3: Prekroji RoutePage.xaml**

Zamijeni sadržaj `EPiljara.Driver/Views/RoutePage.xaml`:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:vm="clr-namespace:EPiljara.Driver.ViewModels"
             xmlns:models="clr-namespace:EPiljara.Driver.Models"
             x:DataType="vm:RouteViewModel"
             x:Class="EPiljara.Driver.Views.RoutePage"
             Title="Moja ruta">

    <Grid RowDefinitions="*,Auto,Auto,Auto">

        <WebView x:Name="Mapa" Grid.Row="0" />

        <Label Grid.Row="0" Text="Nemate rutu za danas."
               IsVisible="{Binding NemaRute}"
               BackgroundColor="White" Padding="20"
               HorizontalOptions="Center" VerticalOptions="Center" />

        <Label Grid.Row="1" Text="{Binding Error}" TextColor="White" BackgroundColor="#B00020"
               Padding="12,8"
               IsVisible="{Binding Error, Converter={StaticResource IsStringNotEmpty}}" />

        <Border Grid.Row="2" Stroke="#DDD" StrokeThickness="1" Padding="14" BackgroundColor="White">
            <VerticalStackLayout Spacing="4">
                <Label Text="{Binding Napredak}" FontAttributes="Bold" FontSize="16" />

                <Label Text="Tura je završena. Svaka čast!"
                       IsVisible="{Binding TuraGotova}" FontAttributes="Bold" />

                <VerticalStackLayout IsVisible="{Binding TrenutnaStanica, Converter={StaticResource IsNotNull}}">
                    <Label FontAttributes="Bold" FontSize="17">
                        <Label.Text>
                            <MultiBinding StringFormat="{}{0}. {1}">
                                <Binding Path="TrenutnaStanica.Sequence" />
                                <Binding Path="TrenutnaStanica.CustomerName" />
                            </MultiBinding>
                        </Label.Text>
                    </Label>
                    <Label Text="{Binding TrenutnaStanica.Address}" FontSize="14" />
                    <Label Text="{Binding TrenutnaStanica.ZoneName}" FontSize="12" TextColor="Gray" />

                    <Button Text="Otvori stanicu" Margin="0,8,0,0"
                            Command="{Binding OtvoriStanicuCommand}"
                            CommandParameter="{Binding TrenutnaStanica}" />
                </VerticalStackLayout>
            </VerticalStackLayout>
        </Border>

        <Border Grid.Row="3" Stroke="#DDD" StrokeThickness="1" Padding="12"
                BackgroundColor="#FFF8E1" IsVisible="{Binding ImaBezLokacije}">
            <VerticalStackLayout Spacing="6">
                <Label Text="Bez tačne lokacije — nemaju pin na mapi" FontSize="12" FontAttributes="Bold" />
                <CollectionView ItemsSource="{Binding BezLokacije}" HeightRequest="90">
                    <CollectionView.ItemTemplate>
                        <DataTemplate x:DataType="models:RouteStopDto">
                            <Grid Padding="0,4">
                                <Label FontSize="13">
                                    <Label.Text>
                                        <MultiBinding StringFormat="{}{0}. {1} — {2}">
                                            <Binding Path="Sequence" />
                                            <Binding Path="CustomerName" />
                                            <Binding Path="Address" />
                                        </MultiBinding>
                                    </Label.Text>
                                </Label>
                                <Grid.GestureRecognizers>
                                    <TapGestureRecognizer
                                        Command="{Binding Source={RelativeSource AncestorType={x:Type vm:RouteViewModel}}, Path=OtvoriStanicuCommand}"
                                        CommandParameter="{Binding .}" />
                                </Grid.GestureRecognizers>
                            </Grid>
                        </DataTemplate>
                    </CollectionView.ItemTemplate>
                </CollectionView>
            </VerticalStackLayout>
        </Border>

    </Grid>
</ContentPage>
```

- [ ] **Step 4: Dodaj IsNotNull konverter**

U `EPiljara.Driver/Converters.cs` dodaj:

```csharp
public class IsNotNullConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
```

i registruj ga u `EPiljara.Driver/App.xaml`, uz postojeće konvertere:

```xml
<local:IsNotNullConverter x:Key="IsNotNull" />
```

- [ ] **Step 5: Poveži WebView u code-behind**

Zamijeni `EPiljara.Driver/Views/RoutePage.xaml.cs`:

```csharp
using EPiljara.Driver.ViewModels;

namespace EPiljara.Driver.Views;

public partial class RoutePage : ContentPage
{
    private readonly RouteViewModel _vm;
    private bool _mapaSpremna;
    private string? _cekaNaMapu;

    public RoutePage(RouteViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;

        Mapa.Source = new UrlWebViewSource { Url = "file:///android_asset/map.html" };
        Mapa.Navigated += (_, _) => { _mapaSpremna = true; IsprazniRed(); };
        Mapa.Navigating += NaNavigaciju;

        _vm.MapaTreba += PosaljiStanice;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.UcitajCommand.Execute(null);
    }

    /// <summary>Stanice mogu stići prije nego što se HTML učita; tada se drže u redu.</summary>
    private void PosaljiStanice(string base64)
    {
        _cekaNaMapu = base64;
        if (_mapaSpremna) IsprazniRed();
    }

    private void IsprazniRed()
    {
        if (_cekaNaMapu is not { } base64) return;
        _cekaNaMapu = null;
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try { await Mapa.EvaluateJavaScriptAsync($"postaviStanice('{base64}')"); }
            catch (Exception) { /* mapa nije spremna; sljedeće osvježavanje će je popuniti */ }
        });
    }

    /// <summary>Klik na pin u JS-u mijenja lokaciju na app://stop/{id}; presrećemo i otkazujemo.</summary>
    private void NaNavigaciju(object? sender, WebNavigatingEventArgs e)
    {
        const string prefiks = "app://stop/";
        if (!e.Url.StartsWith(prefiks, StringComparison.OrdinalIgnoreCase)) return;

        e.Cancel = true;
        if (!Guid.TryParse(e.Url[prefiks.Length..].TrimEnd('/'), out var orderId)) return;

        var stanica = _vm.Aktivne.Concat(_vm.Zavrsene).FirstOrDefault(s => s.OrderId == orderId);
        if (stanica is not null) _vm.OtvoriStanicuCommand.Execute(stanica);
    }
}
```

`file:///android_asset/map.html` je putanja pod kojom Android vidi `Resources/Raw` sadržaj.

- [ ] **Step 6: Javi kad nema interneta**

Bez mreže Leaflet ne može povući pločice i mapa ostane siva. Pinovi se i dalje crtaju, a kartica trenutne stanice i traka bez lokacije su ionako ispod mape — pa vozač nije blokiran, samo ne vidi ulice. Treba mu to reći.

U `RouteViewModel` dodaj:

```csharp
    [ObservableProperty] private bool _nemaInterneta;

    /// <summary>Poziva se pri svakom učitavanju rute — mapa bez mreže ostane siva.</summary>
    private void ProvjeriMrezu() =>
        NemaInterneta = Connectivity.Current.NetworkAccess != NetworkAccess.Internet;
```

i pozovi `ProvjeriMrezu();` kao prvi red u `UcitajAsync`.

U `RoutePage.xaml`, odmah ispod `<WebView x:Name="Mapa" ... />`, dodaj traku:

```xml
        <Label Grid.Row="0" Text="Mapa nije dostupna bez interneta."
               IsVisible="{Binding NemaInterneta}"
               BackgroundColor="#B00020" TextColor="White" Padding="12,8"
               VerticalOptions="Start" />
```

- [ ] **Step 7: Testovi i build**

```bash
cd "C:/Users/soviljn/Desktop/EPiljaraVozac"
dotnet test EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj
cd EPiljara.Driver && dotnet build -f net10.0-android -c Debug
```

Expected: 40 testova prolazi (`RouteViewModel` je linkovan u test projekat, pa nove članove mora da prevede), build uspijeva.

Napomena: `Connectivity.Current` je MAUI Essentials API. Test projekat linkuje `RouteViewModel.cs` i referencira `Microsoft.Maui.Controls`, pa se prevodi — ali `ProvjeriMrezu` se u testovima nikad ne poziva, jer testovi zovu samo `Primijeni`. Ako se ispostavi da se ne prevodi, prijavi to umjesto da mijenjaš dizajn.

- [ ] **Step 8: Commit**

```bash
cd "C:/Users/soviljn/Desktop/EPiljaraVozac"
git add -A
git commit -m "feat: map-first route screen with pins and current stop"
```

---

### Task 4: Popup na stanici, za sada na dugme

Popup se pravi prije GPS-a namjerno: tako se može vidjeti i isprobati bez ijedne dozvole, a Task 5 mu samo doda automatski okidač.

**Files:**
- Modify: `EPiljara.Driver/ViewModels/RouteViewModel.cs`
- Modify: `EPiljara.Driver/Views/RoutePage.xaml`

**Interfaces:**
- Consumes: `DriverService.UpdateStopStatusAsync` (već postoji)
- Produces:
  - `RouteViewModel.PopupStanica` → `RouteStopDto?`
  - `RouteViewModel.PopupVidljiv` → `bool`
  - `RouteViewModel.PrikaziPopup(RouteStopDto)` — poziva ga i dugme i (u Tasku 5) detektor
  - komande `PozoviCommand`, `PrimopredajaCommand`, `NeuspjelaCommand`, `ZatvoriPopupCommand`, `StigaoSamCommand`

- [ ] **Step 1: Dodaj stanje i komande popup-a**

U `RouteViewModel` dodaj polja i metode:

```csharp
    [ObservableProperty] private RouteStopDto? _popupStanica;
    [ObservableProperty] private bool _popupVidljiv;
    [ObservableProperty] private bool _popupZauzet;

    /// <summary>Otvara popup za datu stanicu. Ne otvara drugi preko prvog.</summary>
    public void PrikaziPopup(RouteStopDto stanica)
    {
        if (PopupVidljiv) return;
        PopupStanica = stanica;
        PopupVidljiv = true;
    }

    [RelayCommand]
    private void ZatvoriPopup()
    {
        PopupVidljiv = false;
        PopupStanica = null;
    }

    /// <summary>Rezervni ručni okidač — koristi se kad nema dozvole za lokaciju ili GPS ne radi.</summary>
    [RelayCommand]
    private void StigaoSam()
    {
        if (TrenutnaStanica is { } s) PrikaziPopup(s);
    }

    [RelayCommand]
    private void Pozovi()
    {
        if (string.IsNullOrWhiteSpace(PopupStanica?.CustomerPhone))
        {
            Error = "Kupac nema upisan telefon.";
            return;
        }

        Error = null;
        try { PhoneDialer.Open(PopupStanica.CustomerPhone); }
        catch (FeatureNotSupportedException) { Error = "Pozivanje nije podržano na ovom uređaju."; }
    }

    [RelayCommand]
    private Task Primopredaja() => ZavrsiIzPopupaAsync("Delivered");

    [RelayCommand]
    private Task Neuspjela() => ZavrsiIzPopupaAsync("FailedDelivery");

    private async Task ZavrsiIzPopupaAsync(string status)
    {
        if (PopupZauzet || PopupStanica is not { } s) return;

        PopupZauzet = true;
        Error = null;
        try
        {
            await driver.UpdateStopStatusAsync(s.OrderId, status);
            PopupVidljiv = false;
            PopupStanica = null;
            await UcitajAsync();
        }
        catch (ApiException e)
        {
            Error = e.Code == "invalid_transition" ? "Narudžba je već zatvorena." : e.Message;
        }
        finally
        {
            PopupZauzet = false;
        }
    }
```

`PopupZauzet` se postavlja **prije** mrežnog poziva, da dvostruki dodir ne pošalje dva zahtjeva.

- [ ] **Step 2: Dodaj popup u XAML**

U `EPiljara.Driver/Views/RoutePage.xaml`, unutar korijenskog `<Grid>`, kao **posljednje dijete** (da stoji iznad svega), dodaj:

```xml
        <Grid Grid.RowSpan="4" BackgroundColor="#99000000" IsVisible="{Binding PopupVidljiv}">
            <Border Stroke="#DDD" StrokeThickness="1" Padding="20" Margin="24"
                    BackgroundColor="White" VerticalOptions="Center">
                <VerticalStackLayout Spacing="10">

                    <Label Text="Stigli ste na adresu" FontSize="13" TextColor="Gray" />

                    <Label FontSize="20" FontAttributes="Bold">
                        <Label.Text>
                            <MultiBinding StringFormat="{}{0}. {1}">
                                <Binding Path="PopupStanica.Sequence" />
                                <Binding Path="PopupStanica.CustomerName" />
                            </MultiBinding>
                        </Label.Text>
                    </Label>
                    <Label Text="{Binding PopupStanica.Address}" FontSize="15" />

                    <Button Text="Pozovi" Command="{Binding PozoviCommand}" Margin="0,6,0,0" />
                    <Button Text="Primopredaja izvršena" BackgroundColor="Green"
                            Command="{Binding PrimopredajaCommand}"
                            IsEnabled="{Binding PopupZauzet, Converter={StaticResource InvertedBool}}" />
                    <Button Text="Neuspjela dostava" BackgroundColor="DarkRed"
                            Command="{Binding NeuspjelaCommand}"
                            IsEnabled="{Binding PopupZauzet, Converter={StaticResource InvertedBool}}" />
                    <Button Text="Zatvori" BackgroundColor="Gray" Command="{Binding ZatvoriPopupCommand}" />

                    <ActivityIndicator IsRunning="{Binding PopupZauzet}" IsVisible="{Binding PopupZauzet}" />

                </VerticalStackLayout>
            </Border>
        </Grid>
```

- [ ] **Step 3: Dodaj dugme „Stigao sam" na karticu trenutne stanice**

U istom fajlu, u `VerticalStackLayout` trenutne stanice, ispod dugmeta „Otvori stanicu":

```xml
                    <Button Text="Stigao sam" BackgroundColor="#512BD4"
                            Command="{Binding StigaoSamCommand}" />
```

- [ ] **Step 4: Testovi, build, commit**

```bash
cd "C:/Users/soviljn/Desktop/EPiljaraVozac"
dotnet test EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj
cd EPiljara.Driver && dotnet build -f net10.0-android -c Debug
cd "C:/Users/soviljn/Desktop/EPiljaraVozac"
git add -A
git commit -m "feat: arrival popup with handover, manually triggered"
```

Expected: 40 testova prolazi, build uspijeva.

---

### Task 5: GPS praćenje i automatski okidač

**Files:**
- Create: `EPiljara.Driver/Services/LocationTracker.cs`
- Modify: `EPiljara.Driver/ViewModels/RouteViewModel.cs`
- Modify: `EPiljara.Driver/MauiProgram.cs`
- Modify: `EPiljara.Driver/Platforms/Android/AndroidManifest.xml`

**Interfaces:**
- Consumes: `ArrivalDetector` (Task 1), `RouteViewModel.PrikaziPopup` (Task 4)
- Produces:
  - `LocationTracker.ZatraziDozvoluAsync() → Task<bool>`
  - `LocationTracker.PokreniAsync() → Task`, `Zaustavi()`
  - događaj `LocationTracker.Pomjeraj` → `event Action<double, double, double?>?` (širina, dužina, tačnost)
  - `RouteViewModel.ImaDozvolu` → `bool`

- [ ] **Step 1: Dodaj dozvole u manifest**

U `EPiljara.Driver/Platforms/Android/AndroidManifest.xml`, uz postojeće `<uses-permission>` redove:

```xml
	<uses-permission android:name="android.permission.ACCESS_COARSE_LOCATION" />
	<uses-permission android:name="android.permission.ACCESS_FINE_LOCATION" />
	<uses-permission android:name="android.permission.FOREGROUND_SERVICE" />
	<uses-permission android:name="android.permission.FOREGROUND_SERVICE_LOCATION" />
```

- [ ] **Step 2: Napiši LocationTracker**

`EPiljara.Driver/Services/LocationTracker.cs`:

```csharp
namespace EPiljara.Driver.Services;

/// <summary>
/// Osluškuje GPS dok traje tura. Pozicija se nigdje ne šalje — koristi se samo
/// za mapu i za računanje razdaljine do stanica.
/// </summary>
public class LocationTracker
{
    private bool _radi;

    /// <summary>Širina, dužina, tačnost u metrima (null kad je uređaj ne javlja).</summary>
    public event Action<double, double, double?>? Pomjeraj;

    public async Task<bool> ZatraziDozvoluAsync()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (status != PermissionStatus.Granted)
            status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        return status == PermissionStatus.Granted;
    }

    public async Task PokreniAsync()
    {
        if (_radi) return;

        var zahtjev = new GeolocationListeningRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(5));
        try
        {
            var pokrenuto = await Geolocation.StartListeningForegroundAsync(zahtjev);
            if (!pokrenuto) return;
        }
        catch (Exception e) when (e is FeatureNotSupportedException or FeatureNotEnabledException or PermissionException)
        {
            return;
        }

        Geolocation.LocationChanged += NaPromjenu;
        _radi = true;
    }

    public void Zaustavi()
    {
        if (!_radi) return;
        Geolocation.LocationChanged -= NaPromjenu;
        Geolocation.StopListeningForeground();
        _radi = false;
    }

    private void NaPromjenu(object? sender, GeolocationLocationChangedEventArgs e) =>
        Pomjeraj?.Invoke(e.Location.Latitude, e.Location.Longitude, e.Location.Accuracy);
}
```

- [ ] **Step 3: Poveži detektor u RouteViewModel**

U `RouteViewModel`, proširi konstruktor i dodaj obradu:

```csharp
public partial class RouteViewModel(DriverService driver, AuthService auth, LocationTracker tracker)
    : ObservableObject
{
    private readonly ArrivalDetector _detektor = new();

    [ObservableProperty] private bool _imaDozvolu = true;

    /// <summary>Javlja stranici gdje je vozač, da mapa pomjeri plavu tačku.</summary>
    public event Action<double, double>? PozicijaTreba;
```

Zatim dodaj metodu za pokretanje praćenja i obradu pomjeraja:

```csharp
    /// <summary>Traži dozvolu i pokreće osluškivanje. Zove se iz OnAppearing.</summary>
    public async Task PokreniPracenjeAsync()
    {
        ImaDozvolu = await tracker.ZatraziDozvoluAsync();
        if (!ImaDozvolu) return;

        tracker.Pomjeraj -= NaPomjeraj;
        tracker.Pomjeraj += NaPomjeraj;
        await tracker.PokreniAsync();
    }

    public void ZaustaviPracenje() => tracker.Zaustavi();

    private void NaPomjeraj(double lat, double lon, double? tacnost)
    {
        PozicijaTreba?.Invoke(lat, lon);

        var stigao = _detektor.Provjeri(lat, lon, tacnost, Aktivne);
        if (stigao is not null)
            MainThread.BeginInvokeOnMainThread(() => PrikaziPopup(stigao));
    }
```

I u `ZatvoriPopup` dodaj, prije gašenja, da se stanica zaboravi — inače se popup više nikad ne javi za nju:

```csharp
        if (PopupStanica is { } s) _detektor.Zaboravi(s.OrderId);
```

- [ ] **Step 4: Registruj tracker i pokreni praćenje sa stranice**

U `EPiljara.Driver/MauiProgram.cs`, uz ostale servise:

```csharp
		builder.Services.AddSingleton<LocationTracker>();
```

U `EPiljara.Driver/Views/RoutePage.xaml.cs`, u konstruktoru poslije `_vm.MapaTreba += PosaljiStanice;`:

```csharp
        _vm.PozicijaTreba += (lat, lon) => MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await Mapa.EvaluateJavaScriptAsync(
                    $"postaviPoziciju({lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{lon.ToString(System.Globalization.CultureInfo.InvariantCulture)})");
            }
            catch (Exception) { /* mapa još nije spremna */ }
        });
```

`InvariantCulture` je obavezan: na srpskoj lokalizaciji `44,7722` sa zarezom razbio bi JavaScript poziv na dva argumenta.

Zatim proširi `OnAppearing` i dodaj `OnDisappearing`:

```csharp
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _vm.UcitajCommand.Execute(null);
        await _vm.PokreniPracenjeAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.ZaustaviPracenje();
    }
```

- [ ] **Step 5: Sakrij „Stigao sam" kad automatika radi**

U `RoutePage.xaml`, na dugmetu „Stigao sam" dodaj:

```xml
                            IsVisible="{Binding ImaDozvolu, Converter={StaticResource InvertedBool}}"
```

Time dugme ostaje samo kao rezerva kad dozvola nije data — tačno kako spec traži.

- [ ] **Step 6: Testovi, build, commit**

```bash
cd "C:/Users/soviljn/Desktop/EPiljaraVozac"
dotnet test EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj
cd EPiljara.Driver && dotnet build -f net10.0-android -c Debug
cd "C:/Users/soviljn/Desktop/EPiljaraVozac"
git add -A
git commit -m "feat: gps tracking and automatic arrival popup"
```

Expected: 40 testova i dalje prolazi, build uspijeva.

**Obavezna izmjena testova:** `RouteViewModel` dobija treći konstruktorski parametar, a on je linkovan u test projekat. `RouteViewModelTests.cs` i `RouteViewModelUcitavanjeTests.cs` grade ViewModel sa dva argumenta — svako `new RouteViewModel(null!, null!)` mora postati `new RouteViewModel(null!, null!, null!)`, a gdje se prosljeđuje pravi `DriverService`, treći argument ostaje `null!`. Testovi zovu samo `Primijeni` i `UcitajAsync`, koji tracker ne diraju, pa `null!` nije problem. Pretraži oba fajla i ispravi sve pozive — build testova pada dok ijedan ostane.

---

### Task 6: Foreground servis, da preživi zaključan ekran

**Files:**
- Create: `EPiljara.Driver/Platforms/Android/LocationForegroundService.cs`
- Modify: `EPiljara.Driver/Services/LocationTracker.cs`

**Interfaces:**
- Consumes: `LocationTracker` (Task 5)
- Produces: ništa što kasniji taskovi koriste

Bez ovoga Android uspava aplikaciju čim vozač zaključa ekran, i popup se ne javi usred vožnje.

- [ ] **Step 1: Napiši servis**

`EPiljara.Driver/Platforms/Android/LocationForegroundService.cs`:

```csharp
using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;

namespace EPiljara.Driver.Platforms.Android;

/// <summary>
/// Drži aplikaciju budnom dok traje tura, da GPS osluškivanje ne prestane kad se
/// ekran zaključa. Trajno obavještenje je uslov sistema — i pošteno prema vozaču,
/// jer vidi da aplikacija prati poziciju.
/// </summary>
[Service(ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeLocation, Exported = false)]
public class LocationForegroundService : Service
{
    private const string KanalId = "epiljara_ruta";
    private const int ObavjestenjeId = 1001;

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        NapraviKanal();

        var obavjestenje = new NotificationCompat.Builder(this, KanalId)
            .SetContentTitle("E piljara — tura u toku")
            .SetContentText("Pratimo lokaciju da bismo javili kad stignete na adresu.")
            .SetSmallIcon(global::Android.Resource.Drawable.IcMenuMylocation)
            .SetOngoing(true)
            .Build();

        StartForeground(ObavjestenjeId, obavjestenje);
        return StartCommandResult.Sticky;
    }

    private void NapraviKanal()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O) return;

        var kanal = new NotificationChannel(KanalId, "Tura u toku", NotificationImportance.Low);
        var menadzer = (NotificationManager?)GetSystemService(NotificationService);
        menadzer?.CreateNotificationChannel(kanal);
    }
}
```

- [ ] **Step 2: Pokreni i zaustavi servis iz trackera**

U `EPiljara.Driver/Services/LocationTracker.cs`, u `PokreniAsync`, poslije uspješnog `StartListeningForegroundAsync` i prije `_radi = true;`:

```csharp
#if ANDROID
        var intent = new global::Android.Content.Intent(
            global::Android.App.Application.Context,
            typeof(Platforms.Android.LocationForegroundService));
        global::Android.App.Application.Context.StartForegroundService(intent);
#endif
```

a u `Zaustavi`, poslije `Geolocation.StopListeningForeground();`:

```csharp
#if ANDROID
        var intent = new global::Android.Content.Intent(
            global::Android.App.Application.Context,
            typeof(Platforms.Android.LocationForegroundService));
        global::Android.App.Application.Context.StopService(intent);
#endif
```

`#if ANDROID` je obavezan: `LocationTracker.cs` je linkovan u test projekat koji cilja običan `net10.0` i ne poznaje Android tipove.

- [ ] **Step 3: Testovi, build, commit**

```bash
cd "C:/Users/soviljn/Desktop/EPiljaraVozac"
dotnet test EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj
cd EPiljara.Driver && dotnet build -f net10.0-android -c Debug
cd "C:/Users/soviljn/Desktop/EPiljaraVozac"
git add -A
git commit -m "feat: android foreground service keeps tracking alive"
```

---

### Task 7: Provjera na emulatoru sa lažnim GPS-om

**Files:** nijedan — ovo je provjera.

**Interfaces:**
- Consumes: sve iz taskova 1–6
- Produces: ništa

- [ ] **Step 1: Podigni okruženje**

```bash
docker ps --format "{{.Names}}" | grep epiljara-postgres
cd "C:/Users/soviljn/Desktop/epiljara-backend-wt/backend"
dotnet run --project src/EPiljara.API --urls http://0.0.0.0:5080
```

U drugom terminalu pokreni emulator, pa instaliraj aplikaciju:

```bash
"$LOCALAPPDATA/Android/Sdk/emulator/emulator.exe" -avd epiljara_api35
cd "C:/Users/soviljn/Desktop/EPiljaraVozac/EPiljara.Driver"
dotnet build -f net10.0-android -c Debug -t:Run
```

**Redoslijed je bitan: prvo Docker, pa emulator.** Docker restart prekonfiguriše Hyper-V svičeve i obori emulatorov NAT; ako se to desi, emulator ima adresu ali nema podrazumijevani gateway i aplikacija javlja „Nema veze sa serverom". Lijek je hladno pokretanje: `emulator.exe -avd epiljara_api35 -no-snapshot-load`.

- [ ] **Step 2: Napravi rutu sa poznatim koordinatama**

Kroz admin panel (`admin@epiljara.ba` / `admin123`) na `/admin/routes` dodijeli 2–3 narudžbe vozaču **Mladen (kombi 1)** za današnji datum. Bar jedna mora imati koordinate — ako nemaju, postavi ih dugmetom „Bez tačne lokacije — postavi".

Pročitaj tačne koordinate stanica:

```bash
docker exec epiljara-postgres psql -U epiljara -d epiljara -c "SELECT \"Code\",\"Latitude\",\"Longitude\" FROM \"Orders\" WHERE \"Latitude\" IS NOT NULL ORDER BY \"CreatedAt\" DESC LIMIT 5;"
```

- [ ] **Step 3: Prijavi se i provjeri mapu**

Prijava: `vozac@epiljara.ba` / `vozac123`.

Provjeri:
- mapa se iscrta i pinovi stoje na stanicama sa koordinatama
- stanica bez koordinata **nema** pin, ali stoji u žutoj traci na dnu
- kartica trenutne stanice pokazuje prvu nezatvorenu po redoslijedu
- dodir na pin otvara ekran stanice

- [ ] **Step 4: Odglumi dolazak**

Aplikacija će pri prvom otvaranju rute tražiti dozvolu za lokaciju — **dozvoli je**.

Zatim postavi lažnu poziciju na koordinate prve stanice (**redoslijed je dužina pa širina**, obrnuto od svega ostalog):

```bash
"$LOCALAPPDATA/Android/Sdk/platform-tools/adb.exe" emu geo fix 17.1910 44.7722
```

Expected: u roku od nekoliko sekundi iskoči popup sa imenom kupca te stanice.

Provjeri redom:
- popup pokazuje **tačnu** stanicu (onu na čije si koordinate stao)
- „Zatvori" ga skloni, a ponovno slanje iste pozicije ga vrati
- „Primopredaja izvršena" zatvori stanicu, popup nestane, brojač poraste, pin posivi
- pomjeri poziciju daleko (`adb emu geo fix 17.30 44.90`) pa nazad — popup se javi ponovo samo ako stanica nije zatvorena

- [ ] **Step 5: Provjeri rezervni režim**

Odbij dozvolu za lokaciju (u podešavanjima aplikacije na emulatoru ukloni dozvolu i pokreni je ponovo).

Expected: mapa i dalje prikazuje pinove, plave tačke nema, i na kartici trenutne stanice se pojavljuje dugme **„Stigao sam"** koje otvara isti popup.

- [ ] **Step 6: Provjeri da tura preživi zaključan ekran**

Uz dozvolu i pokrenutu turu, zaključaj ekran emulatora na 30 sekundi, pa postavi lažnu poziciju na sljedeću stanicu i otključaj.

Expected: obavještenje „E piljara — tura u toku" stoji u statusnoj traci, i popup je iskočio ili iskače odmah po otključavanju.

- [ ] **Step 7: Zabilježi nalaze**

Zapiši šta je prošlo a šta nije. Ako neka provjera padne, to je nalaz — ne prepravljaj plan, nego javi.

---

## Šta ovaj plan namjerno ne radi

Prava optimizacija rute, slanje pozicije vozača na server, utovar i push notifikacije (Faza 3), naplata, navigacija unutar aplikacije (i dalje se otvara Google Maps sa ekrana stanice), iOS, rad bez interneta.
