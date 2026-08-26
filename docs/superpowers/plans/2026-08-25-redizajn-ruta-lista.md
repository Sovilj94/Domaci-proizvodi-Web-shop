# Redizajn ekrana rute u listu stanica — plan implementacije

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ekran rute u aplikaciji vozača postaje lista kartica stanica bez mape; navigaciju u cjelosti preuzima Google Maps, a dolazak se javlja notifikacijom koja probija preko njega.

**Architecture:** Leaflet mapa u `WebView`-u se briše zajedno sa mostom u oba smjera. `RouteViewModel` već drži `Aktivne` i `Zavrsene`; dodaje im se `Prikaz` — kolekcija omotača `StanicaPrikaz` koja nosi stanje kartice. `AppShell` dobija `TabBar` sa tri taba, od kojih su dva prazna do svojih specova. Nov Android kanal notifikacija visokog prioriteta javlja dolazak dok je aplikacija u pozadini.

**Tech Stack:** .NET MAUI (net10.0-android), CommunityToolkit.Mvvm, xUnit (test projekat cilja plain `net10.0`), ASP.NET Core 9 na strani backenda.

**Spec:** `docs/superpowers/specs/2026-08-25-redizajn-ruta-lista-design.md`

## Global Constraints

- **Dva repozitorijuma.** Backend: `C:\Users\soviljn\Desktop\epiljara-backend-wt\backend`. Aplikacija: `C:\Users\soviljn\Desktop\EPiljaraVozac`. Dijele samo HTTP ugovor — nikad ne dodavati referencu preko te granice. Commituje se odvojeno, u svom repozitorijumu.
- **Svi korisnički stringovi na srpskom, ijekavica sa dijakritikom.** „Sljedeća", „Isporučeno", „Podešavanja" — ne po mockupima, koji su ekavica bez dijakritike.
- **DTO-ovi u `EPiljara.Driver/Models/Dtos.cs` su ručno preslikani 1:1 sa backendom.** Izmjena na jednoj strani traži istu izmjenu na drugoj, u istom zadatku.
- **Brojevi i datumi se formatiraju kulturom `sr-Latn-RS`, nikad `CurrentCulture`.** Vozačev telefon može biti na engleskom.
- **Test projekat cilja `net10.0` i povlači izvore aplikacije kao `<Compile Include/Link>`.** Svaki nov `.cs` fajl koji linkovani fajl referencira **mora dobiti svoj `<Compile Include/Link>`** u `EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj`, inače test projekat prestaje da se prevodi. Platformski kod ide iza `#if ANDROID`. MAUI tipovi (`Color`, `Colors`) ne smiju ući u linkovan fajl.
- **Testovi grade `new RouteViewModel(null!, null!, null!)`.** Svaki nov poziv na parametar konstruktora iz `Primijeni` ili `UcitajAsync` mora trpjeti `null` — koristiti `?.`.
- **`RoutePage` mora ostati keširan `ShellContent`** (konstruktor tačno jednom za vijek aplikacije). **Ne dodavati `OnDisappearing` teardown** — `Disappearing` puca i pri otvaranju stanice i pri prebacivanju taba.
- **XAML veze se provjeravaju pri prevođenju** (`x:DataType` + `MauiXamlInflator=SourceGen`) — pogrešno ime obara build. Literalni `"` u XML atributu ide kao `&quot;`.
- **Build aplikacije traje ~2 minuta.** Nije zaglavljen.
- Komande:
  - testovi: `dotnet test EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj` (iz `EPiljaraVozac`)
  - build: `cd EPiljara.Driver && dotnet build -f net10.0-android -c Debug`
  - na uređaj: `cd EPiljara.Driver && dotnet build -f net10.0-android -c Debug -t:Run`
  - backend: `dotnet run --project src/EPiljara.API --urls http://0.0.0.0:5080` (iz `epiljara-backend-wt/backend`)

---

## Pregled fajlova

**Backend (`epiljara-backend-wt/backend/src/EPiljara.Application`)**
- `DTOs/AdminDtos.cs` — `RouteStopDto` dobija `TotalAmount`
- `Mapping/MappingExtensions.cs` — puni ga iz `Order.TotalAmount`

**Aplikacija (`EPiljaraVozac/EPiljara.Driver`)**
- `Models/Dtos.cs` — ogledalo `TotalAmount`
- `Services/Formatiranje.cs` *(novo)* — jedno mjesto za srpski ispis brojeva i datuma
- `ViewModels/StanicaPrikaz.cs` *(novo)* — jedna kartica; bez MAUI tipova, da se testira
- `ViewModels/RouteViewModel.cs` — `Prikaz`, brojevi trake, brisanje mosta ka mapi
- `Views/RoutePage.xaml` + `.xaml.cs` — lista umjesto mape
- `Views/IstorijaPage.xaml` + `.cs`, `Views/ProfilPage.xaml` + `.cs` *(novo)* — prazni tabovi
- `Converters.cs` — tri konvertera za karticu
- `AppShell.xaml` — `TabBar`
- `MauiProgram.cs` — registracija novih stranica
- `Platforms/Android/DolazakObavjestenje.cs` *(novo)* — kanal i slanje notifikacije
- `Resources/Raw/map.html`, `leaflet.js`, `leaflet.css` — **brišu se**

**Testovi (`EPiljaraVozac/EPiljara.Driver.Tests`)**
- `FormatiranjeTests.cs` *(novo)*, `NavigacijaNatpisTests.cs` *(novo)*, `StanicaPrikazTests.cs` *(novo)*
- `RouteViewModelTests.cs` — brišu se testovi mosta ka mapi, mijenjaju se testovi `Sazetak`
- `EPiljara.Driver.Tests.csproj` — dva nova `Compile Include`

---

## Task 1: Pouzeće kroz HTTP ugovor

**Files:**
- Modify: `epiljara-backend-wt/backend/src/EPiljara.Application/DTOs/AdminDtos.cs:24`
- Modify: `epiljara-backend-wt/backend/src/EPiljara.Application/Mapping/MappingExtensions.cs:139`
- Modify: `EPiljaraVozac/EPiljara.Driver/Models/Dtos.cs`
- Test: `EPiljaraVozac/EPiljara.Driver.Tests/RouteViewModelTests.cs:502` (pomoćnik `Stanica`)

**Interfaces:**
- Produces: `RouteStopDto.TotalAmount` tipa `decimal`, na obje strane. Task 4 ga čita u `StanicaPrikaz.Detalj`.

Backend nema test projekat (*„There is no automated test suite"*), pa se ta strana provjerava pozivom na živi API.

- [ ] **Step 1: Dodaj polje u backend DTO**

U `DTOs/AdminDtos.cs`, na kraj `RouteStopDto` (poslije `Longitude`):

```csharp
    /// <summary>Iznos koji vozač naplaćuje pri dostavi. Platforma nema drugi način
    /// plaćanja — „Plaćanje pouzećem pri dostavi" je zakucano u OrderEmailTemplate i
    /// ViberMessage — pa je ukupan iznos narudžbe ujedno i pouzeće. Ako se ikad doda
    /// online plaćanje, kartica u aplikaciji vozača će tiho pokazivati pogrešan iznos
    /// za naplatu i ovo polje mora dobiti uslov.</summary>
    public decimal TotalAmount { get; init; }
```

- [ ] **Step 2: Popuni ga u mapiranju**

U `Mapping/MappingExtensions.cs`, u `ToDto(this RouteStop s)`, poslije `Longitude = s.Order?.Longitude,`:

```csharp
        TotalAmount = s.Order?.TotalAmount ?? 0m,
```

- [ ] **Step 3: Podigni backend i provjeri da polje stiže**

```bash
cd /c/Users/soviljn/Desktop/epiljara-backend-wt/backend
dotnet run --project src/EPiljara.API --urls http://0.0.0.0:5080
```

U drugom terminalu:

```bash
TOKEN=$(curl -s -X POST http://localhost:5080/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"vozac@epiljara.ba","password":"vozac123"}' \
  | node -e "let s='';process.stdin.on('data',d=>s+=d).on('end',()=>console.log(JSON.parse(s).data.token))")
curl -s "http://localhost:5080/driver/route?date=2026-08-25" -H "Authorization: Bearer $TOKEN" \
  | node -e "let s='';process.stdin.on('data',d=>s+=d).on('end',()=>console.log(JSON.parse(s).data.stops.map(x=>x.sequence+': '+x.totalAmount).join('\n')))"
```

Očekivano: svaka stanica ispiše iznos veći od nule, npr. `1: 33.00`. Ako je `undefined`, mapiranje nije povezano.

- [ ] **Step 4: Commituj backend**

```bash
cd /c/Users/soviljn/Desktop/epiljara-backend-wt
git add backend/src/EPiljara.Application/DTOs/AdminDtos.cs backend/src/EPiljara.Application/Mapping/MappingExtensions.cs
git commit -m "feat: posalji iznos pouzeca vozacu uz svaku stanicu"
```

- [ ] **Step 5: Preslikaj polje u aplikaciju**

U `EPiljara.Driver/Models/Dtos.cs`, `RouteStopDto` je pozicioni record — dodaj parametar **na kraj**, poslije `Longitude`:

```csharp
public record RouteStopDto(
    Guid OrderId,
    string OrderCode,
    string CustomerName,
    string Address,
    string ZoneName,
    int Sequence,
    string? CustomerPhone,
    string Status,
    double? Latitude,
    double? Longitude,
    decimal TotalAmount);
```

- [ ] **Step 6: Popravi pomoćnik u testovima**

`RouteViewModelTests.cs:502` gradi `RouteStopDto` imenovanim argumentima; dodavanje parametra bez podrazumijevane vrijednosti obara prevođenje. Dopuni ga:

```csharp
    private static RouteStopDto Stanica(int redni, double lat, double lon) => new(
        OrderId: Guid.NewGuid(),
        OrderCode: $"EP-2608-{redni:D4}",
        CustomerName: $"Kupac {redni}",
        Address: $"Ulica {redni}",
        ZoneName: "Centar",
        Sequence: redni,
        CustomerPhone: null,
        Status: "Confirmed",
        Latitude: lat,
        Longitude: lon,
        TotalAmount: 24.50m);
```

Ako `dotnet build` prijavi još koje mjesto gdje se `RouteStopDto` gradi, dopuni i njega istom vrijednošću.

- [ ] **Step 7: Testovi moraju proći nepromijenjeni**

```bash
cd /c/Users/soviljn/Desktop/EPiljaraVozac
dotnet test EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj
```

Očekivano: svi prolaze. Ovaj korak samo dokazuje da dodavanje polja nije ništa polomilo.

- [ ] **Step 8: Commituj aplikaciju**

```bash
cd /c/Users/soviljn/Desktop/EPiljaraVozac
git add EPiljara.Driver/Models/Dtos.cs EPiljara.Driver.Tests/RouteViewModelTests.cs
git commit -m "feat: preslikaj iznos pouzeca u DTO aplikacije"
```

---

## Task 2: Zajedničko formatiranje brojeva i datuma

**Files:**
- Create: `EPiljaraVozac/EPiljara.Driver/Services/Formatiranje.cs`
- Modify: `EPiljaraVozac/EPiljara.Driver/ViewModels/RouteViewModel.cs:23` (privatno polje `Srpski`)
- Modify: `EPiljaraVozac/EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj`
- Test: `EPiljaraVozac/EPiljara.Driver.Tests/FormatiranjeTests.cs`

**Interfaces:**
- Produces: `EPiljara.Driver.Services.Formatiranje` sa članovima `Srpski` (`CultureInfo`), `Datum(DateOnly) → string`, `Novac(decimal) → string`, `Kilometri(double?) → string`, `Minuti(double?) → string`. Task 4 koristi sva četiri; Task 5 nijedan direktno.

- [ ] **Step 1: Napiši testove koji padaju**

Novi fajl `EPiljara.Driver.Tests/FormatiranjeTests.cs`:

```csharp
using System.Globalization;
using EPiljara.Driver.Services;

namespace EPiljara.Driver.Tests;

/// <summary>
/// Svaki test namjerno postavlja englesku kulturu na nit prije poziva. Poenta ovih
/// funkcija nije da formatiraju, nego da formatiraju ISTO bez obzira na jezik uređaja —
/// vozačev telefon zna biti na engleskom, gdje bi CurrentCulture dala decimalnu tačku
/// i engleske nazive dana.
/// </summary>
public class FormatiranjeTests
{
    private static void UEngleskoj(Action test)
    {
        var staro = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        try { test(); }
        finally { CultureInfo.CurrentCulture = staro; }
    }

    [Fact]
    public void Novac_pise_dvije_decimale_sa_zarezom() =>
        UEngleskoj(() => Assert.Equal("24,50 KM", Formatiranje.Novac(24.5m)));

    [Fact]
    public void Novac_pise_i_cijeli_iznos_sa_dvije_decimale() =>
        UEngleskoj(() => Assert.Equal("33,00 KM", Formatiranje.Novac(33m)));

    [Fact]
    public void Kilometri_zaokruzuju_na_jednu_decimalu() =>
        UEngleskoj(() => Assert.Equal("15,3 km", Formatiranje.Kilometri(15312)));

    [Fact]
    public void Kilometri_bez_vrijednosti_daju_crticu_a_ne_nulu() =>
        UEngleskoj(() => Assert.Equal("—", Formatiranje.Kilometri(null)));

    [Fact]
    public void Minuti_zaokruzuju_na_cio_broj() =>
        UEngleskoj(() => Assert.Equal("27 min", Formatiranje.Minuti(1617.8)));

    [Fact]
    public void Minuti_bez_vrijednosti_daju_crticu_a_ne_nulu() =>
        UEngleskoj(() => Assert.Equal("—", Formatiranje.Minuti(null)));

    [Fact]
    public void Datum_pise_srpski_naziv_dana_i_mjeseca() =>
        UEngleskoj(() => Assert.Equal("Utorak, 25. avgust", Formatiranje.Datum(new DateOnly(2026, 8, 25))));

    [Fact]
    public void Datum_koristi_ijekavske_nazive_dana() =>
        UEngleskoj(() =>
        {
            Assert.Equal("Srijeda, 26. avgust", Formatiranje.Datum(new DateOnly(2026, 8, 26)));
            Assert.Equal("Četvrtak, 27. avgust", Formatiranje.Datum(new DateOnly(2026, 8, 27)));
            Assert.Equal("Nedjelja, 30. avgust", Formatiranje.Datum(new DateOnly(2026, 8, 30)));
        });
}
```

- [ ] **Step 2: Pokreni testove i provjeri da padaju**

Run: `dotnet test EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj --filter "FullyQualifiedName~FormatiranjeTests"`
Expected: FAIL sa greškom prevođenja — `Formatiranje` ne postoji.

- [ ] **Step 3: Napiši `Formatiranje.cs`**

Novi fajl `EPiljara.Driver/Services/Formatiranje.cs`:

```csharp
using System.Globalization;

namespace EPiljara.Driver.Services;

/// <summary>
/// Jedno mjesto za ispis brojeva i datuma na srpskom. Kultura je ovdje EKSPLICITNA,
/// nikad ambijentalna: vozačev telefon može biti podešen na engleski, a CurrentCulture
/// bi tad dala decimalnu tačku („3.7 km") i engleske nazive dana.
/// Nema MAUI tipova — ovaj fajl se linkuje u test projekat koji cilja plain net10.0.
/// </summary>
public static class Formatiranje
{
    public static readonly CultureInfo Srpski = CultureInfo.GetCultureInfo("sr-Latn-RS");

    /// <summary>Ijekavica, indeksirano po DayOfWeek (0 = nedjelja).</summary>
    private static readonly string[] Dani =
        ["Nedjelja", "Ponedjeljak", "Utorak", "Srijeda", "Četvrtak", "Petak", "Subota"];

    private static readonly string[] Mjeseci =
        ["januar", "februar", "mart", "april", "maj", "jun",
         "jul", "avgust", "septembar", "oktobar", "novembar", "decembar"];

    /// <summary>„Utorak, 25. avgust". Nazivi iz ugrađene tabele, ne iz kulture —
    /// sr-Latn-RS nije uvijek prisutna na uređaju u punom obliku.</summary>
    public static string Datum(DateOnly d) =>
        $"{Dani[(int)d.DayOfWeek]}, {d.Day}. {Mjeseci[d.Month - 1]}";

    /// <summary>„24,50 KM" — uvijek dvije decimale, jer je iznos novca.</summary>
    public static string Novac(decimal iznos) => $"{iznos.ToString("0.00", Srpski)} KM";

    /// <summary>„15,3 km", ili „—" kad kilometraže nema. Nikad „0 km": null znači
    /// „nije izračunato" (optimizacija je pala na rezervu po zonama), a nula bi bila
    /// tvrdnja, i to netačna.</summary>
    public static string Kilometri(double? metara) =>
        metara is { } m ? $"{(m / 1000).ToString("0.#", Srpski)} km" : "—";

    /// <summary>„27 min", ili „—" kad trajanja nema. Isti razlog kao za kilometre.</summary>
    public static string Minuti(double? sekundi) =>
        sekundi is { } s ? $"{Math.Round(s / 60).ToString(Srpski)} min" : "—";
}
```

- [ ] **Step 4: Linkuj nov fajl u test projekat**

Ovo je obavezno — bez toga test projekat ne vidi tip. U `EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj`, uz ostale `Compile Include` stavke:

```xml
    <Compile Include="..\EPiljara.Driver\Services\Formatiranje.cs" Link="Services\Formatiranje.cs" />
```

- [ ] **Step 5: Pokreni testove i provjeri da prolaze**

Run: `dotnet test EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj --filter "FullyQualifiedName~FormatiranjeTests"`
Expected: PASS, 8 testova.

- [ ] **Step 6: Prebaci `RouteViewModel` na zajedničku kulturu**

U `RouteViewModel.cs` obriši privatno polje `Srpski` sa svojim komentarom (linije 19–23):

```csharp
    /// <summary>Sazetak mora ispisati decimalni zarez (npr. "3,7 km") bez obzira na
    /// jezik uređaja — vozačev telefon može biti podešen na engleski, a
    /// CurrentCulture bi tad dala tačku ("3.7 km"). Zato je kultura ovdje
    /// eksplicitna, ne ambijentalna.</summary>
    private static readonly CultureInfo Srpski = CultureInfo.GetCultureInfo("sr-Latn-RS");
```

i zamijeni preostale upotrebe `Srpski` u tom fajlu sa `Formatiranje.Srpski`. Fajl već ima `using EPiljara.Driver.Services;`. Ako `using System.Globalization;` ostane neiskorišćen, ostavi ga — ukloniće ga Task 4 ako zatreba.

- [ ] **Step 7: Cijela svita mora proći**

Run: `dotnet test EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj`
Expected: PASS, svi postojeći testovi plus 8 novih.

- [ ] **Step 8: Commituj**

```bash
git add EPiljara.Driver/Services/Formatiranje.cs EPiljara.Driver.Tests/FormatiranjeTests.cs \
        EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj EPiljara.Driver/ViewModels/RouteViewModel.cs
git commit -m "feat: jedno mjesto za srpski ispis brojeva i datuma"
```

---

## Task 3: Pokrij natpis serijske navigacije testovima

**Files:**
- Test: `EPiljaraVozac/EPiljara.Driver.Tests/NavigacijaNatpisTests.cs` *(novo)*

**Interfaces:**
- Consumes: `RouteViewModel.Primijeni`, `RouteViewModel.NavigacijaNatpis`, `RouteStopDto` iz Task 1.
- Produces: ništa — ovo je zaštitna mreža, ne nov kod.

Review je našao da `NavigacijaNatpis` nema **nijedan** test i da je već jednom otišao pokvaren na uređaj. Kad se mapa obriše, Google Maps postaje jedina navigacija i ovaj natpis postaje kritični put. Testovi idu **prije** brisanja mape, da uhvate ako brisanje nešto povuče.

U test hostu `GoogleMapsInstaliran()` vraća `false` (nema `PackageManager`-a van Androida), pa je `NavigacijaLink.MaxStanica(false) == 4`. Testovi se oslanjaju na to.

- [ ] **Step 1: Napiši testove**

Novi fajl `EPiljara.Driver.Tests/NavigacijaNatpisTests.cs`:

```csharp
using EPiljara.Driver.Models;
using EPiljara.Driver.ViewModels;

namespace EPiljara.Driver.Tests;

/// <summary>
/// Natpis dugmeta za serijsku navigaciju. U test hostu GoogleMapsInstaliran() vraća
/// false (nema PackageManager-a van Androida), pa je maksimum serije 4.
/// Ovi testovi postoje jer je natpis već jednom otišao pokvaren na uređaj, a od
/// uklanjanja mape je Google Maps jedina navigacija koju vozač ima.
/// </summary>
public class NavigacijaNatpisTests
{
    private static RouteViewModel Vm() => new(null!, null!, null!);

    private static RouteStopDto Stanica(int redni) => new(
        OrderId: Guid.NewGuid(),
        OrderCode: $"EP-2608-{redni:D4}",
        CustomerName: $"Kupac {redni}",
        Address: $"Ulica {redni}",
        ZoneName: "Centar",
        Sequence: redni,
        CustomerPhone: null,
        Status: "Confirmed",
        Latitude: 44.77 + redni * 0.001,
        Longitude: 17.19 + redni * 0.001,
        TotalAmount: 20m);

    private static DeliveryRouteDto Ruta(int koliko) => new(
        Id: Guid.NewGuid(),
        Date: "2026-08-25",
        DriverId: Guid.NewGuid(),
        DriverName: "Mladen",
        Status: "Optimized",
        Stops: Enumerable.Range(1, koliko).Select(Stanica).ToList(),
        DuzinaMetara: 9200,
        TrajanjeSekundi: 2100,
        Geometrija: null);

    [Fact]
    public void Kad_serija_staje_u_jedan_link_natpis_je_obican()
    {
        var vm = Vm();
        vm.Primijeni(Ruta(4));
        Assert.Equal("Navigacija", vm.NavigacijaNatpis);
    }

    [Fact]
    public void Kad_ima_vise_stanica_nego_sto_staje_natpis_javlja_odsjecanje()
    {
        var vm = Vm();
        vm.Primijeni(Ruta(7));
        Assert.Equal("Navigacija (4 od 7)", vm.NavigacijaNatpis);
    }

    [Fact]
    public void Bez_rute_natpis_je_obican()
    {
        var vm = Vm();
        vm.Primijeni(null);
        Assert.Equal("Navigacija", vm.NavigacijaNatpis);
    }

    [Fact]
    public void Natpis_se_smanji_kad_stanice_budu_zatvorene()
    {
        var vm = Vm();
        vm.Primijeni(Ruta(7));
        Assert.Equal("Navigacija (4 od 7)", vm.NavigacijaNatpis);

        // Ista ruta, ali su prve tri isporučene — ostaju četiri otvorene, što staje u link.
        var stanice = Ruta(7).Stops
            .Select((s, i) => i < 3 ? s with { Status = "Delivered" } : s)
            .ToList();
        vm.Primijeni(Ruta(7) with { Stops = stanice });

        Assert.Equal("Navigacija", vm.NavigacijaNatpis);
    }
}
```

- [ ] **Step 2: Pokreni ih i provjeri da prolaze**

Run: `dotnet test EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj --filter "FullyQualifiedName~NavigacijaNatpisTests"`
Expected: PASS, 4 testa. Ovo su karakterizacioni testovi — pišu se za kod koji već radi, pa **prolaze odmah**.

- [ ] **Step 3: Dokaži da stvarno drže**

Privremeno pokvari `OsvjeziNavigacijuNatpis` u `RouteViewModel.cs:303` — zamijeni tijelo sa `NavigacijaNatpis = "Navigacija";` i pokreni testove ponovo.
Expected: `Kad_ima_vise_stanica_nego_sto_staje_natpis_javlja_odsjecanje` PADA. Zatim **vrati original** i pokreni opet — svi prolaze.

Bez ovog koraka nemaš dokaz da test išta pribija; svita od 87 testova je već jednom bila zelena preko stvarnog kvara.

- [ ] **Step 4: Commituj**

```bash
git add EPiljara.Driver.Tests/NavigacijaNatpisTests.cs
git commit -m "test: pokrij natpis serijske navigacije prije uklanjanja mape"
```

---

## Task 4: Stanje kartice i brojevi trake u ViewModel-u

**Files:**
- Create: `EPiljaraVozac/EPiljara.Driver/ViewModels/StanicaPrikaz.cs`
- Modify: `EPiljaraVozac/EPiljara.Driver/ViewModels/RouteViewModel.cs` (`Primijeni`, nova svojstva, brisanje `Sazetak`)
- Modify: `EPiljaraVozac/EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj`
- Modify: `EPiljaraVozac/EPiljara.Driver.Tests/RouteViewModelTests.cs:450-470` (testovi `Sazetak`)
- Test: `EPiljaraVozac/EPiljara.Driver.Tests/StanicaPrikazTests.cs` *(novo)*

**Interfaces:**
- Consumes: `RouteStopDto.TotalAmount` (Task 1), `Formatiranje.*` (Task 2).
- Produces: `StanicaStanje` (enum: `Sljedeca`, `NaCekanju`, `Isporucena`, `Neuspjela`, `Otkazana`), `StanicaPrikaz` record sa svojstvima `Stanica`, `Stanje`, `Redni`, `Ime`, `Adresa`, `Detalj`, `Oznaka`, `ImaOznaku`, `JeSljedeca`, `JeZatvorena`, `BezKoordinata`, `PrikaziUpozorenje`. `RouteViewModel.Prikaz` (`ObservableCollection<StanicaPrikaz>`), `RouteViewModel.UkupnoStanica` (`int`), `UkupnoKm`, `UkupnoVrijeme`, `DatumNaslov` (`string`). Task 5 veže sve to u XAML-u.

`StanicaPrikaz` **ne smije nositi MAUI tipove** (`Color`, `Colors`) — linkuje se u test projekat koji cilja plain `net10.0`. Boje se rješavaju konverterima u Task 5.

- [ ] **Step 1: Napiši testove koji padaju**

Novi fajl `EPiljara.Driver.Tests/StanicaPrikazTests.cs`:

```csharp
using EPiljara.Driver.Models;
using EPiljara.Driver.ViewModels;

namespace EPiljara.Driver.Tests;

public class StanicaPrikazTests
{
    private static RouteStopDto Stanica(
        int redni, string status = "Confirmed", double? lat = 44.77, double? lon = 17.19,
        string zona = "Borik", decimal iznos = 24.50m) => new(
        OrderId: Guid.NewGuid(),
        OrderCode: $"EP-2608-{redni:D4}",
        CustomerName: $"Kupac {redni}",
        Address: $"Ulica {redni}",
        ZoneName: zona,
        Sequence: redni,
        CustomerPhone: null,
        Status: status,
        Latitude: lat,
        Longitude: lon,
        TotalAmount: iznos);

    private static DeliveryRouteDto Ruta(
        IReadOnlyList<RouteStopDto> stanice, double? metara = 15312, double? sekundi = 1617.8) => new(
        Id: Guid.NewGuid(),
        Date: "2026-08-25",
        DriverId: Guid.NewGuid(),
        DriverName: "Mladen",
        Status: "Optimized",
        Stops: stanice,
        DuzinaMetara: metara,
        TrajanjeSekundi: sekundi,
        Geometrija: null);

    private static RouteViewModel Vm() => new(null!, null!, null!);

    [Fact]
    public void Prva_otvorena_stanica_je_sljedeca_ostale_cekaju()
    {
        var vm = Vm();
        vm.Primijeni(Ruta([Stanica(1), Stanica(2), Stanica(3)]));

        Assert.Equal(StanicaStanje.Sljedeca, vm.Prikaz[0].Stanje);
        Assert.Equal(StanicaStanje.NaCekanju, vm.Prikaz[1].Stanje);
        Assert.Equal(StanicaStanje.NaCekanju, vm.Prikaz[2].Stanje);
        Assert.Equal("Sljedeća", vm.Prikaz[0].Oznaka);
        Assert.Null(vm.Prikaz[1].Oznaka);
    }

    [Fact]
    public void Neuspjela_dostava_se_razlikuje_od_isporucene()
    {
        var vm = Vm();
        vm.Primijeni(Ruta([
            Stanica(1, "Delivered"),
            Stanica(2, "FailedDelivery"),
            Stanica(3, "Cancelled"),
        ]));

        var po = vm.Prikaz.ToDictionary(p => p.Redni, p => p);
        Assert.Equal(StanicaStanje.Isporucena, po[1].Stanje);
        Assert.Equal(StanicaStanje.Neuspjela, po[2].Stanje);
        Assert.Equal(StanicaStanje.Otkazana, po[3].Stanje);

        Assert.Null(po[1].Oznaka);
        Assert.Equal("Neuspjela dostava", po[2].Oznaka);
        Assert.Equal("Otkazana", po[3].Oznaka);
        Assert.All(new[] { po[1], po[2], po[3] }, p => Assert.True(p.JeZatvorena));
    }

    [Fact]
    public void Otvorene_stanice_dolaze_prije_zatvorenih()
    {
        var vm = Vm();
        vm.Primijeni(Ruta([Stanica(1, "Delivered"), Stanica(2), Stanica(3)]));

        Assert.Equal([2, 3, 1], vm.Prikaz.Select(p => p.Redni).ToArray());
    }

    [Fact]
    public void Detalj_spaja_zonu_i_pouzece()
    {
        var vm = Vm();
        vm.Primijeni(Ruta([Stanica(1, zona: "Borik", iznos: 24.50m)]));

        Assert.Equal("Borik · Pouzeće 24,50 KM", vm.Prikaz[0].Detalj);
    }

    [Fact]
    public void Detalj_bez_zone_nosi_samo_pouzece()
    {
        var vm = Vm();
        vm.Primijeni(Ruta([Stanica(1, zona: "", iznos: 18m)]));

        Assert.Equal("Pouzeće 18,00 KM", vm.Prikaz[0].Detalj);
    }

    [Fact]
    public void Stanica_bez_koordinata_nosi_upozorenje()
    {
        var vm = Vm();
        vm.Primijeni(Ruta([Stanica(1, lat: null, lon: null), Stanica(2)]));

        Assert.True(vm.Prikaz[0].BezKoordinata);
        Assert.True(vm.Prikaz[0].PrikaziUpozorenje);
        Assert.False(vm.Prikaz[1].PrikaziUpozorenje);
    }

    [Fact]
    public void Zatvorena_stanica_bez_koordinata_ne_nosi_upozorenje()
    {
        // Upozorenje govori „ne može se navigirati i dolazak se ne detektuje".
        // Na zatvorenoj stanici nema šta da se navigira, pa bi bilo samo šum.
        var vm = Vm();
        vm.Primijeni(Ruta([Stanica(1, "Delivered", lat: null, lon: null)]));

        Assert.True(vm.Prikaz[0].BezKoordinata);
        Assert.False(vm.Prikaz[0].PrikaziUpozorenje);
    }

    [Fact]
    public void Traka_nosi_ukupan_broj_stanica_ne_preostali()
    {
        var vm = Vm();
        vm.Primijeni(Ruta([Stanica(1, "Delivered"), Stanica(2), Stanica(3)]));

        Assert.Equal(3, vm.UkupnoStanica);
    }

    [Fact]
    public void Traka_pise_kilometre_i_minute_cijele_ture()
    {
        var vm = Vm();
        vm.Primijeni(Ruta([Stanica(1)], metara: 15312, sekundi: 1617.8));

        Assert.Equal("15,3 km", vm.UkupnoKm);
        Assert.Equal("27 min", vm.UkupnoVrijeme);
    }

    [Fact]
    public void Traka_pise_crticu_kad_optimizacija_nije_dala_kilometrazu()
    {
        // Rezerva po zonama vraća redoslijed ali ne i kilometražu. „0 km" bi bila
        // tvrdnja, i to netačna.
        var vm = Vm();
        vm.Primijeni(Ruta([Stanica(1)], metara: null, sekundi: null));

        Assert.Equal("—", vm.UkupnoKm);
        Assert.Equal("—", vm.UkupnoVrijeme);
    }

    [Fact]
    public void Naslov_nosi_datum_rute_na_srpskom()
    {
        var vm = Vm();
        vm.Primijeni(Ruta([Stanica(1)]));

        Assert.Equal("Utorak, 25. avgust", vm.DatumNaslov);
    }

    [Fact]
    public void Prikaz_se_isprazni_kad_rute_nema()
    {
        var vm = Vm();
        vm.Primijeni(Ruta([Stanica(1), Stanica(2)]));
        Assert.Equal(2, vm.Prikaz.Count);

        vm.Primijeni(null);
        Assert.Empty(vm.Prikaz);
        Assert.Equal(0, vm.UkupnoStanica);
    }
}
```

- [ ] **Step 2: Pokreni testove i provjeri da padaju**

Run: `dotnet test EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj --filter "FullyQualifiedName~StanicaPrikazTests"`
Expected: FAIL sa greškom prevođenja — `StanicaStanje`, `StanicaPrikaz`, `vm.Prikaz`, `vm.UkupnoStanica` ne postoje.

- [ ] **Step 3: Napiši `StanicaPrikaz.cs`**

Novi fajl `EPiljara.Driver/ViewModels/StanicaPrikaz.cs`:

```csharp
using EPiljara.Driver.Models;
using EPiljara.Driver.Services;

namespace EPiljara.Driver.ViewModels;

/// <summary>Kako kartica stanice izgleda. Domen ima pet ishoda, ne dva —
/// u Zavrsene padaju i neuspjele i otkazane, pa bez zasebnih stanja neuspjela
/// dostava izgleda kao uspješna.</summary>
public enum StanicaStanje
{
    Sljedeca,
    NaCekanju,
    Isporucena,
    Neuspjela,
    Otkazana,
}

/// <summary>
/// Jedna kartica u listi stanica. Omotač oko <see cref="RouteStopDto"/>, a ne polja na
/// njemu: taj DTO je ručno preslikan 1:1 sa backendom i ne smije nositi stanje prikaza.
/// NAMJERNO bez MAUI tipova (Color i slično) — ovaj fajl se linkuje u test projekat
/// koji cilja plain net10.0. Boje se rješavaju konverterima u XAML-u.
/// </summary>
public record StanicaPrikaz(RouteStopDto Stanica, StanicaStanje Stanje)
{
    public int Redni => Stanica.Sequence;
    public string Ime => Stanica.CustomerName;
    public string Adresa => Stanica.Address;

    public bool JeSljedeca => Stanje == StanicaStanje.Sljedeca;

    public bool JeZatvorena => Stanje
        is StanicaStanje.Isporucena or StanicaStanje.Neuspjela or StanicaStanje.Otkazana;

    /// <summary>Bez koordinata se ne može navigirati i dolazak se ne detektuje.</summary>
    public bool BezKoordinata => Stanica.Latitude is null || Stanica.Longitude is null;

    /// <summary>Upozorenje ima smisla samo dok stanica čeka — na zatvorenoj nema
    /// šta da se navigira, pa bi bilo samo šum.</summary>
    public bool PrikaziUpozorenje => BezKoordinata && !JeZatvorena;

    /// <summary>„Borik · Pouzeće 24,50 KM". Bez zone ostaje samo pouzeće.</summary>
    public string Detalj => string.IsNullOrWhiteSpace(Stanica.ZoneName)
        ? $"Pouzeće {Formatiranje.Novac(Stanica.TotalAmount)}"
        : $"{Stanica.ZoneName} · Pouzeće {Formatiranje.Novac(Stanica.TotalAmount)}";

    /// <summary>Isporučena nema oznaku — zelena kvačica sve kaže. Neuspjela i otkazana
    /// je moraju imati, inače se ne razlikuju od uspješne.</summary>
    public string? Oznaka => Stanje switch
    {
        StanicaStanje.Sljedeca => "Sljedeća",
        StanicaStanje.Neuspjela => "Neuspjela dostava",
        StanicaStanje.Otkazana => "Otkazana",
        _ => null,
    };

    public bool ImaOznaku => Oznaka is not null;
}
```

- [ ] **Step 4: Linkuj nov fajl u test projekat**

U `EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj`, uz ostale `Compile Include` stavke:

```xml
    <Compile Include="..\EPiljara.Driver\ViewModels\StanicaPrikaz.cs" Link="ViewModels\StanicaPrikaz.cs" />
```

- [ ] **Step 5: Dodaj svojstva u `RouteViewModel`**

Uz ostale kolekcije (blizu `BezLokacije`, oko linije 41):

```csharp
    /// <summary>Lista kartica koju crta ekran rute: otvorene stanice pa zatvorene,
    /// obje već poređane po Sequence u Primijeni.</summary>
    public ObservableCollection<StanicaPrikaz> Prikaz { get; } = [];
```

Uz ostala `[ObservableProperty]` polja (blizu `_sazetak`):

```csharp
    /// <summary>Brojevi trake — UKUPNA tura, ne preostala. DuzinaMetara i
    /// TrajanjeSekundi se ionako odnose na cijelu turu, uključujući povratak u magacin.
    /// Napredak („3 od 7 završeno") stoji odvojeno, u zaglavlju: traka govori koliko je
    /// tura velika, napredak gdje si u njoj, i pomiješani tjeraju vozača da čita dvaput.</summary>
    [ObservableProperty] private int _ukupnoStanica;
    [ObservableProperty] private string _ukupnoKm = "—";
    [ObservableProperty] private string _ukupnoVrijeme = "—";

    /// <summary>„Utorak, 25. avgust" — datum rute, ne uređaja.</summary>
    [ObservableProperty] private string _datumNaslov = string.Empty;
```

- [ ] **Step 6: Popuni ih u `Primijeni`**

U `RouteViewModel.Primijeni`, odmah poslije bloka koji puni `BezLokacije` i postavlja `ImaBezLokacije` (oko linije 147), dodaj:

```csharp
        Prikaz.Clear();
        for (var i = 0; i < Aktivne.Count; i++)
            Prikaz.Add(new StanicaPrikaz(
                Aktivne[i], i == 0 ? StanicaStanje.Sljedeca : StanicaStanje.NaCekanju));
        foreach (var s in Zavrsene)
            Prikaz.Add(new StanicaPrikaz(s, StanjeZatvorene(s.Status)));

        UkupnoStanica = stops.Count;
        UkupnoKm = Formatiranje.Kilometri(route?.DuzinaMetara);
        UkupnoVrijeme = Formatiranje.Minuti(route?.TrajanjeSekundi);
        DatumNaslov = Formatiranje.Datum(
            DateOnly.TryParse(route?.Date, out var datum) ? datum : DateOnly.FromDateTime(DateTime.Now));
```

i dodaj pomoćnu metodu odmah ispod `Primijeni`:

```csharp
    /// <summary>Preslikava zatvoreni status u stanje kartice. Sve što nije isporučeno
    /// ni neuspjelo je otkazano — Zatvoreni skup ima tačno ta tri člana.</summary>
    private static StanicaStanje StanjeZatvorene(string status) => status.ToLowerInvariant() switch
    {
        "delivered" => StanicaStanje.Isporucena,
        "faileddelivery" => StanicaStanje.Neuspjela,
        _ => StanicaStanje.Otkazana,
    };
```

- [ ] **Step 7: Obriši `Sazetak`**

Traka sa brojevima ga zamjenjuje. U `RouteViewModel.cs` obriši `[ObservableProperty] private string _sazetak = string.Empty;` sa komentarom iznad, i blok koji ga postavlja u `Primijeni`:

```csharp
        Sazetak = route?.DuzinaMetara is { } m && route.TrajanjeSekundi is { } sek
            ? $"Cijela tura: {(m / 1000).ToString("0.#", Formatiranje.Srpski)} km · {Math.Round(sek / 60).ToString(Formatiranje.Srpski)} min"
            : string.Empty;
```

Komentar iznad tog bloka (o tome da brojevi pokrivaju cijelu turu uključujući povratak u magacin) **zadrži** — preseli ga na `UkupnoKm` blok iz Step 6, jer ta činjenica i dalje važi.

U `RouteViewModelTests.cs` zamijeni dva testa `Sazetak` (linije 450–470) — njihovo ponašanje sad pokrivaju `Traka_pise_kilometre_i_minute_cijele_ture` i `Traka_pise_crticu_kad_optimizacija_nije_dala_kilometrazu` iz Step 1, pa ih **obriši**.

- [ ] **Step 8: Pokreni testove i provjeri da prolaze**

Run: `dotnet test EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj`
Expected: PASS. Novih 12 iz `StanicaPrikazTests`, dva obrisana `Sazetak` testa, ostatak nepromijenjen.

- [ ] **Step 9: Commituj**

```bash
git add EPiljara.Driver/ViewModels/StanicaPrikaz.cs EPiljara.Driver/ViewModels/RouteViewModel.cs \
        EPiljara.Driver.Tests/StanicaPrikazTests.cs EPiljara.Driver.Tests/RouteViewModelTests.cs \
        EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj
git commit -m "feat: stanje kartice stanice i brojevi trake u ViewModel-u"
```

---

## Task 5: Ekran rute postaje lista, mapa se briše

**Files:**
- Rewrite: `EPiljaraVozac/EPiljara.Driver/Views/RoutePage.xaml`
- Rewrite: `EPiljaraVozac/EPiljara.Driver/Views/RoutePage.xaml.cs`
- Modify: `EPiljaraVozac/EPiljara.Driver/ViewModels/RouteViewModel.cs` (brisanje mosta ka mapi)
- Modify: `EPiljaraVozac/EPiljara.Driver/Converters.cs`
- Modify: `EPiljaraVozac/EPiljara.Driver/App.xaml`
- Delete: `EPiljaraVozac/EPiljara.Driver/Resources/Raw/map.html`, `leaflet.js`, `leaflet.css`
- Modify: `EPiljaraVozac/EPiljara.Driver.Tests/RouteViewModelTests.cs` (brisanje testova mosta)

**Interfaces:**
- Consumes: `RouteViewModel.Prikaz`, `UkupnoStanica`, `UkupnoKm`, `UkupnoVrijeme`, `DatumNaslov`, `StanicaPrikaz.*` (Task 4).
- Produces: konverteri `StanjeUBoju`, `ZatvorenaUProvidnost`, `SljedecaUDebljinu` u `EPiljara.Driver` prostoru imena, registrovani u `App.xaml`.

Ovaj zadatak briše mapu i gradi listu **zajedno**, jer oba mijenjaju isti XAML — razdvojeno bi ostavilo ekran bez ijednog prikaza rute.

- [ ] **Step 1: Obriši testove mosta ka mapi**

U `RouteViewModelTests.cs` obriši testove koji koriste `MapaTreba` (oko linija 350 i 372), `RutaTreba` (oko 416 i 432) i `GeoJsonULeaflet` (oko 390 i 402). To uključuje i provjeru imena polja `id`, `redni`, `lat`, `lon`, `gotova` — taj test je pisan da glasno pukne ako se most pokvari, a most nestaje.

- [ ] **Step 2: Pokreni testove — moraju proći bez obrisanih**

Run: `dotnet test EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj`
Expected: PASS. Kolekcija je manja; ništa ne pada.

- [ ] **Step 3: Obriši most ka mapi iz `RouteViewModel`**

Obriši iz `RouteViewModel.cs`:

- `public event Action<double, double>? PozicijaTreba;` sa komentarom
- `public event Action<string>? MapaTreba;` sa komentarom
- `public event Action<string>? RutaTreba;` sa komentarom
- `private record MapaStanica(string id, int redni, double? lat, double? lon, bool gotova);` sa komentarom
- cijelu metodu `OsvjeziMapu` i njen poziv `OsvjeziMapu(stops);` u `Primijeni`
- cijelu metodu `GeoJsonULeaflet` sa komentarom
- u `Primijeni`, blok koji šalje liniju rute:

```csharp
        var linija = GeoJsonULeaflet(route?.Geometrija);
        RutaTreba?.Invoke(Convert.ToBase64String(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(linija))));
```

- u `ProvjeriDolazak`, red `PozicijaTreba?.Invoke(lat, lon);`

`using System.Text;` i `using System.Text.Json;` postaju neiskorišćeni — obriši ih. **`_zadnjaSirina` i `_zadnjaDuzina` ostaju** (dugme „Preračunaj od moje lokacije" zavisi od njih).

- [ ] **Step 4: Dodaj konvertere za karticu**

Na kraj `EPiljara.Driver/Converters.cs` (prostor imena `EPiljara.Driver`):

```csharp
/// <summary>Boja kruga sa rednim brojem, po stanju stanice. Boje su ovdje, a ne na
/// StanicaPrikaz, jer se taj tip linkuje u test projekat koji nema MAUI tipove.</summary>
public class StanjeUBojuConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is ViewModels.StanicaStanje stanje
            ? stanje switch
            {
                ViewModels.StanicaStanje.Sljedeca => Color.FromArgb("#512BD4"),
                ViewModels.StanicaStanje.Isporucena => Color.FromArgb("#2E7D32"),
                ViewModels.StanicaStanje.Neuspjela => Color.FromArgb("#B00020"),
                ViewModels.StanicaStanje.Otkazana => Color.FromArgb("#9E9E9E"),
                _ => Color.FromArgb("#BDBDBD"),
            }
            : Color.FromArgb("#BDBDBD");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Zatvorena stanica se prigušuje — vozač na njoj nema šta da radi.</summary>
public class ZatvorenaUProvidnostConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? 0.55 : 1.0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Sljedeća stanica dobija deblji okvir.</summary>
public class SljedecaUDebljinuConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? 2.0 : 0.5;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
```

Ako `Converters.cs` još nema `using System.Globalization;`, dodaj ga.

- [ ] **Step 5: Registruj konvertere**

U `App.xaml`, uz postojeće:

```xml
            <local:StanjeUBojuConverter x:Key="StanjeUBoju" />
            <local:ZatvorenaUProvidnostConverter x:Key="ZatvorenaUProvidnost" />
            <local:SljedecaUDebljinuConverter x:Key="SljedecaUDebljinu" />
```

- [ ] **Step 6: Napiši nov `RoutePage.xaml`**

Zamijeni cijeli sadržaj:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:vm="clr-namespace:EPiljara.Driver.ViewModels"
             x:DataType="vm:RouteViewModel"
             x:Class="EPiljara.Driver.Views.RoutePage"
             Title="Moja ruta">

    <Grid RowDefinitions="Auto,Auto,Auto,Auto,*">

        <VerticalStackLayout Grid.Row="0" Padding="16,12,16,8" Spacing="2">
            <Label Text="{Binding DatumNaslov}" FontSize="12" TextColor="Gray" />
            <Label Text="Moja ruta" FontSize="17" FontAttributes="Bold" />
            <Label Text="{Binding Napredak}" FontSize="12" TextColor="Gray"
                   IsVisible="{Binding Napredak, Converter={StaticResource IsStringNotEmpty}}" />
        </VerticalStackLayout>

        <Grid Grid.Row="1" ColumnDefinitions="*,*,*" ColumnSpacing="8" Padding="16,0,16,12">
            <Border Grid.Column="0" Stroke="#EEE" StrokeThickness="0.5" Padding="8" BackgroundColor="#F7F7F7">
                <VerticalStackLayout>
                    <Label Text="{Binding UkupnoStanica}" FontSize="18" FontAttributes="Bold"
                           HorizontalOptions="Center" />
                    <Label Text="Stanica" FontSize="11" TextColor="Gray" HorizontalOptions="Center" />
                </VerticalStackLayout>
            </Border>
            <Border Grid.Column="1" Stroke="#EEE" StrokeThickness="0.5" Padding="8" BackgroundColor="#F7F7F7">
                <VerticalStackLayout>
                    <Label Text="{Binding UkupnoKm}" FontSize="18" FontAttributes="Bold"
                           HorizontalOptions="Center" />
                    <Label Text="Cijela tura" FontSize="11" TextColor="Gray" HorizontalOptions="Center" />
                </VerticalStackLayout>
            </Border>
            <Border Grid.Column="2" Stroke="#EEE" StrokeThickness="0.5" Padding="8" BackgroundColor="#F7F7F7">
                <VerticalStackLayout>
                    <Label Text="{Binding UkupnoVrijeme}" FontSize="18" FontAttributes="Bold"
                           HorizontalOptions="Center" />
                    <Label Text="Procjena" FontSize="11" TextColor="Gray" HorizontalOptions="Center" />
                </VerticalStackLayout>
            </Border>
        </Grid>

        <VerticalStackLayout Grid.Row="2" Spacing="0">
            <Label Text="{Binding Error}" TextColor="White" BackgroundColor="#B00020" Padding="12,8"
                   IsVisible="{Binding Error, Converter={StaticResource IsStringNotEmpty}}" />
            <Label Text="Nema veze sa internetom — ruta se ne može osvježiti."
                   IsVisible="{Binding NemaInterneta}"
                   BackgroundColor="#FFF8E1" Padding="12,8" FontSize="13" />
            <Label Text="Lokacija je isključena, pa vam ne možemo sami javiti kad stignete. Koristite dugme „Stigao sam&quot;."
                   IsVisible="{Binding ImaDozvolu, Converter={StaticResource InvertedBool}}"
                   BackgroundColor="#FFF8E1" Padding="12,8" FontSize="13" />
            <Label Text="Nemate rutu za danas." IsVisible="{Binding NemaRute}"
                   Padding="16,12" FontSize="15" HorizontalOptions="Center" />
            <Label Text="Tura je završena. Svaka čast!" IsVisible="{Binding TuraGotova}"
                   Padding="16,12" FontAttributes="Bold" HorizontalOptions="Center" />
        </VerticalStackLayout>

        <VerticalStackLayout Grid.Row="3" Padding="16,0,16,8" Spacing="6">
            <Button Text="{Binding NavigacijaNatpis}" BackgroundColor="#512BD4" TextColor="White"
                    Command="{Binding NavigacijaSerijaCommand}" />
            <Button Text="Preračunaj od moje lokacije" FontSize="13"
                    Command="{Binding PreracunajCommand}"
                    IsVisible="{Binding PracenjeRadi}"
                    IsEnabled="{Binding IsRefreshing, Converter={StaticResource InvertedBool}}" />
            <Grid ColumnDefinitions="*,Auto,Auto">
                <ActivityIndicator Grid.Column="1" IsRunning="{Binding IsRefreshing}"
                                   IsVisible="{Binding IsRefreshing}" Margin="0,0,8,0" />
                <Button Grid.Column="2" Text="Osvježi" FontSize="13"
                        Command="{Binding UcitajCommand}"
                        IsEnabled="{Binding IsRefreshing, Converter={StaticResource InvertedBool}}" />
            </Grid>
        </VerticalStackLayout>

        <CollectionView Grid.Row="4" ItemsSource="{Binding Prikaz}" Margin="16,0,16,0">
            <CollectionView.ItemTemplate>
                <DataTemplate x:DataType="vm:StanicaPrikaz">
                    <Border Margin="0,0,0,10" Padding="12" StrokeShape="RoundRectangle 12"
                            Stroke="#512BD4"
                            StrokeThickness="{Binding JeSljedeca, Converter={StaticResource SljedecaUDebljinu}}"
                            Opacity="{Binding JeZatvorena, Converter={StaticResource ZatvorenaUProvidnost}}">
                        <Grid ColumnDefinitions="Auto,*,Auto" ColumnSpacing="10">

                            <Border Grid.Column="0" WidthRequest="26" HeightRequest="26" Padding="0"
                                    StrokeThickness="0" StrokeShape="RoundRectangle 13"
                                    VerticalOptions="Start"
                                    BackgroundColor="{Binding Stanje, Converter={StaticResource StanjeUBoju}}">
                                <Label Text="{Binding Redni}" TextColor="White" FontSize="12"
                                       HorizontalOptions="Center" VerticalOptions="Center" />
                            </Border>

                            <VerticalStackLayout Grid.Column="1" Spacing="2">
                                <Label Text="{Binding Ime}" FontSize="14" FontAttributes="Bold" />
                                <Label Text="{Binding Adresa}" FontSize="12" TextColor="#555" />
                                <Label Text="{Binding Detalj}" FontSize="11" TextColor="Gray" />
                                <Label Text="{Binding Oznaka}" FontSize="11" TextColor="#512BD4"
                                       IsVisible="{Binding ImaOznaku}" />
                                <Label Text="Bez tačne lokacije — navigacija i automatski dolazak ne rade."
                                       FontSize="11" TextColor="#8A6D00" BackgroundColor="#FFF8E1"
                                       Padding="6,3" IsVisible="{Binding PrikaziUpozorenje}" />
                            </VerticalStackLayout>

                            <Label Grid.Column="2" Text="›" FontSize="20" TextColor="Gray"
                                   VerticalOptions="Center" />
                        </Grid>

                        <Border.GestureRecognizers>
                            <TapGestureRecognizer
                                Command="{Binding Source={RelativeSource AncestorType={x:Type vm:RouteViewModel}}, Path=OtvoriStanicuCommand}"
                                CommandParameter="{Binding Stanica}" />
                        </Border.GestureRecognizers>
                    </Border>
                </DataTemplate>
            </CollectionView.ItemTemplate>
        </CollectionView>

        <Grid Grid.RowSpan="5" BackgroundColor="#99000000" IsVisible="{Binding PopupVidljiv}">
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

                    <Label Text="{Binding PopupError}" TextColor="#B00020" FontSize="13"
                           IsVisible="{Binding PopupError, Converter={StaticResource IsStringNotEmpty}}" />

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

    </Grid>
</ContentPage>
```

Dugme „Stigao sam" iz starog panela **ostaje** — ono je rezervni okidač popupa kad GPS ne radi, pa ne smije nestati sa ekrana. Dodaj ga u `Grid.Row="3"`, kao posljednje dugme ispod „Preračunaj od moje lokacije":

```xml
            <Button Text="Stigao sam" BackgroundColor="#512BD4" TextColor="White" FontSize="13"
                    Command="{Binding StigaoSamCommand}"
                    IsVisible="{Binding PracenjeRadi, Converter={StaticResource InvertedBool}}" />
```

- [ ] **Step 7: Napiši nov `RoutePage.xaml.cs`**

Zamijeni cijeli sadržaj:

```csharp
using EPiljara.Driver.Services;
using EPiljara.Driver.ViewModels;

namespace EPiljara.Driver.Views;

public partial class RoutePage : ContentPage
{
    private readonly RouteViewModel _vm;

    public RoutePage(RouteViewModel vm, AuthService auth)
    {
        InitializeComponent();
        BindingContext = _vm = vm;

        // RoutePage je keširan ShellContent (konstruktor se izvrši tačno jednom za
        // cijeli vijek aplikacije), pa ova pretplata živi i dok stranica nije
        // vidljiva. Zato ovo, a ne OnDisappearing: kad token istekne dok je vozač na
        // ekranu rute, AuthHeaderHandler odjavi i navigira na prijavu, ali Primijeni
        // se poslije toga više nikad ne izvrši, pa jedini dosadašnji uslov za gašenje
        // praćenja nikad ne stigne da se provjeri.
        auth.SesijaZavrsena += _vm.ZaustaviPracenje;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _vm.UcitajCommand.Execute(null);
        await _vm.PokreniPracenjeAsync();
    }

    // NAMJERNO nema OnDisappearing koji gasi praćenje. Disappearing okine i kad se
    // StopPage gurne na stek, I kad vozač prebaci tab — gašenje tu je obaralo
    // foreground servis svaki put. Praćenje prestaje kad tura stane
    // (RouteViewModel.Primijeni), što je jedini uslov iz spec-a.
}
```

- [ ] **Step 8: Obriši Leaflet i HTML mape**

```bash
cd /c/Users/soviljn/Desktop/EPiljaraVozac
rm EPiljara.Driver/Resources/Raw/map.html \
   EPiljara.Driver/Resources/Raw/leaflet.js \
   EPiljara.Driver/Resources/Raw/leaflet.css
```

`appsettings.Debug.json` i `appsettings.Release.json` u istom folderu **ostaju**.

- [ ] **Step 9: Testovi i build**

```bash
dotnet test EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj
cd EPiljara.Driver && dotnet build -f net10.0-android -c Debug
```

Expected: testovi PASS; build uspije sa 0 grešaka. XAML veze se provjeravaju pri prevođenju, pa zeleni build znači da svako `{Binding}` ime postoji.

- [ ] **Step 10: Pokreni na emulatoru i pogledaj ekran**

```bash
cd EPiljara.Driver && dotnet build -f net10.0-android -c Debug -t:Run
```

Prijavi se kao `vozac@epiljara.ba` / `vozac123`. Snimi ekran i **pogledaj sliku**:

```bash
ADB="C:/Users/soviljn/AppData/Local/Android/Sdk/platform-tools/adb.exe"
"$ADB" exec-out screencap -p > /tmp/ruta.png
```

Očekivano: zaglavlje sa datumom i napretkom, tri broja u traci, dugmad, pa lista kartica — prva sa debljim okvirom i oznakom „Sljedeća". Nigdje mape. Prazan ekran znači da se stranica nije podigla.

- [ ] **Step 11: Commituj**

```bash
git add -A EPiljara.Driver EPiljara.Driver.Tests
git commit -m "feat: ekran rute je lista stanica; mapa uklonjena"
```

---

## Task 6: Tab bar sa tri taba

**Files:**
- Modify: `EPiljaraVozac/EPiljara.Driver/AppShell.xaml`
- Create: `EPiljaraVozac/EPiljara.Driver/Views/IstorijaPage.xaml` + `.xaml.cs`
- Create: `EPiljaraVozac/EPiljara.Driver/Views/ProfilPage.xaml` + `.xaml.cs`
- Modify: `EPiljaraVozac/EPiljara.Driver/MauiProgram.cs:53`

**Interfaces:**
- Consumes: `Views.RoutePage` (Task 5).
- Produces: `Views.IstorijaPage` i `Views.ProfilPage` — Spec 2 i Spec 3 im mijenjaju sadržaj, ali zadržavaju imena tipova i rute `istorija` / `profil`.

**Ruta `route` mora ostati `route`.** `LoginViewModel` navigira na `//route` (dva mjesta, linije 27 i 46), a `AuthHeaderHandler` na `//login`. Ako se ruta preimenuje, prijava vodi u prazno.

- [ ] **Step 1: Napravi prazne stranice**

`Views/IstorijaPage.xaml`:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             x:Class="EPiljara.Driver.Views.IstorijaPage"
             Title="Istorija">
    <VerticalStackLayout Padding="24" Spacing="8" VerticalOptions="Center">
        <Label Text="Istorija ruta" FontSize="17" FontAttributes="Bold" HorizontalOptions="Center" />
        <Label Text="Uskoro." FontSize="14" TextColor="Gray" HorizontalOptions="Center" />
    </VerticalStackLayout>
</ContentPage>
```

`Views/IstorijaPage.xaml.cs`:

```csharp
namespace EPiljara.Driver.Views;

public partial class IstorijaPage : ContentPage
{
    public IstorijaPage() => InitializeComponent();
}
```

`Views/ProfilPage.xaml` — isto, sa `x:Class="EPiljara.Driver.Views.ProfilPage"`, `Title="Profil"` i naslovom `Profil`:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             x:Class="EPiljara.Driver.Views.ProfilPage"
             Title="Profil">
    <VerticalStackLayout Padding="24" Spacing="8" VerticalOptions="Center">
        <Label Text="Profil" FontSize="17" FontAttributes="Bold" HorizontalOptions="Center" />
        <Label Text="Uskoro." FontSize="14" TextColor="Gray" HorizontalOptions="Center" />
    </VerticalStackLayout>
</ContentPage>
```

`Views/ProfilPage.xaml.cs`:

```csharp
namespace EPiljara.Driver.Views;

public partial class ProfilPage : ContentPage
{
    public ProfilPage() => InitializeComponent();
}
```

- [ ] **Step 2: Registruj ih u DI**

U `MauiProgram.cs`, poslije registracije `Views.StopPage`:

```csharp
		builder.Services.AddTransient<Views.IstorijaPage>();
		builder.Services.AddTransient<Views.ProfilPage>();
```

- [ ] **Step 3: Napiši nov `AppShell.xaml`**

```xml
<?xml version="1.0" encoding="utf-8" ?>
<Shell xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
       xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
       xmlns:views="clr-namespace:EPiljara.Driver.Views"
       x:Class="EPiljara.Driver.AppShell"
       FlyoutBehavior="Disabled">

    <ShellContent Route="login" ContentTemplate="{DataTemplate views:LoginPage}" />

    <!-- Ruta ShellContent-a ostaje "route": LoginViewModel navigira na "//route". -->
    <TabBar>
        <ShellContent Title="Rute" Route="route"
                      ContentTemplate="{DataTemplate views:RoutePage}" />
        <ShellContent Title="Istorija" Route="istorija"
                      ContentTemplate="{DataTemplate views:IstorijaPage}" />
        <ShellContent Title="Profil" Route="profil"
                      ContentTemplate="{DataTemplate views:ProfilPage}" />
    </TabBar>

</Shell>
```

- [ ] **Step 4: Testovi i build**

```bash
dotnet test EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj
cd EPiljara.Driver && dotnet build -f net10.0-android -c Debug
```

Expected: oboje prolazi.

- [ ] **Step 5: Provjeri na uređaju da je `RoutePage` i dalje keširan**

Ovo je najozbiljniji rizik cijelog plana i ne provjerava se testom nego na uređaju. Postavi privremeni ispis u `RoutePage` konstruktoru, odmah poslije `InitializeComponent();`:

```csharp
        System.Diagnostics.Debug.WriteLine("[RoutePage] KONSTRUKTOR");
```

```bash
cd EPiljara.Driver && dotnet build -f net10.0-android -c Debug -t:Run
```

Prijavi se, pa **pet puta** prebaci tab Rute → Istorija → Rute → Profil → Rute i pročitaj log:

```bash
ADB="C:/Users/soviljn/AppData/Local/Android/Sdk/platform-tools/adb.exe"
"$ADB" logcat -d | grep "RoutePage] KONSTRUKTOR"
```

Expected: **tačno jedan** red. Više redova znači da se stranica presozdaje po tabu — praćenje i detekcija dolaska bi se tiho lomili, i tab shell treba drugačije složiti prije nastavka.

Kad potvrdiš, **obriši privremeni ispis**.

- [ ] **Step 6: Provjeri da praćenje preživi prebacivanje taba**

Sa istim buildom, dok je praćenje upaljeno (vidiš obavještenje „E piljara — tura u toku" u statusnoj traci), prebaci tab na Profil pa nazad na Rute.

Expected: obavještenje **ostaje** cijelo vrijeme. Ako nestane pri prebacivanju, negdje je ušao `OnDisappearing` teardown.

- [ ] **Step 7: Provjeri da prijava i dalje vodi na rutu**

Odjavi se nasilno — restartuj aplikaciju i prijavi se iznova.

Expected: poslije prijave otvara se tab Rute sa listom stanica. Prazan ekran znači da `//route` više ne pogađa cilj.

- [ ] **Step 8: Commituj**

```bash
git add EPiljara.Driver/AppShell.xaml EPiljara.Driver/MauiProgram.cs EPiljara.Driver/Views/IstorijaPage.xaml \
        EPiljara.Driver/Views/IstorijaPage.xaml.cs EPiljara.Driver/Views/ProfilPage.xaml EPiljara.Driver/Views/ProfilPage.xaml.cs
git commit -m "feat: tab bar sa rutama, istorijom i profilom"
```

---

## Task 7: Notifikacija na dolazak

**Files:**
- Create: `EPiljaraVozac/EPiljara.Driver/Platforms/Android/DolazakObavjestenje.cs`
- Modify: `EPiljaraVozac/EPiljara.Driver/ViewModels/RouteViewModel.cs` (`ProvjeriDolazak`)

**Interfaces:**
- Consumes: `RouteStopDto` (Task 1).
- Produces: `DolazakObavjestenje.Posalji(string adresa)` — statička, `void`, guta svoje izuzetke.

Bez ovoga automatska detekcija dolaska javlja samo vozaču koji već gleda u aplikaciju — a od Task 5 vozač vozi u Google Mapama i naša aplikacija je u pozadini.

- [ ] **Step 1: Napiši kanal i slanje**

Novi fajl `EPiljara.Driver/Platforms/Android/DolazakObavjestenje.cs`:

```csharp
using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;

namespace EPiljara.Driver.Platforms.Android;

/// <summary>
/// Javlja vozaču da je stigao, i onda kad je aplikacija u pozadini. Otkad Google Maps
/// vozi cijelu rutu, to je uobičajen slučaj, a ne izuzetak — popup unutar aplikacije
/// se tada otvori iza tuđeg ekrana i niko ga ne vidi.
///
/// Kanal je ODVOJEN od onog koji koristi LocationForegroundService: taj je namjerno
/// NotificationImportance.Low (trajno obavještenje koje ne smije da iskače), a ovaj
/// mora biti High da probije preko Google Mapa.
/// </summary>
public static class DolazakObavjestenje
{
    private const string KanalId = "epiljara_dolazak";
    private const int ObavjestenjeId = 1002;

    /// <summary>
    /// Guta sve izuzetke NAMJERNO. Poziva se sa GPS toka koji vodi do `async void`
    /// lifecycle metode bez ijednog catch-a, a aplikacija nema globalni hvatač
    /// neuhvaćenih izuzetaka — neuspjelo obavještenje ne smije oboriti proces usred
    /// ture. Isti razlog zbog kojeg ZatraziObavjestenjaAsync hvata široko.
    /// </summary>
    public static void Posalji(string adresa)
    {
        try
        {
            var kontekst = global::Android.App.Application.Context;
            NapraviKanal(kontekst);

            // Dodir vraća u aplikaciju; ruta se ionako osvježava u OnAppearing.
            var namjera = kontekst.PackageManager?.GetLaunchIntentForPackage(kontekst.PackageName!);
            namjera?.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);

            var dodir = namjera is null
                ? null
                : PendingIntent.GetActivity(
                    kontekst, 0, namjera,
                    PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            var obavjestenje = new NotificationCompat.Builder(kontekst, KanalId)
                .SetContentTitle("Stigli ste na adresu")
                .SetContentText(adresa)
                .SetSmallIcon(global::Android.Resource.Drawable.IcMenuMyLocation)
                .SetPriority(NotificationCompat.PriorityHigh)
                .SetAutoCancel(true)
                .SetContentIntent(dodir)
                .Build();

            NotificationManagerCompat.From(kontekst).Notify(ObavjestenjeId, obavjestenje);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DolazakObavjestenje] slanje nije uspjelo: {ex}");
        }
    }

    private static void NapraviKanal(Context kontekst)
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O) return;

        var kanal = new NotificationChannel(KanalId, "Dolazak na stanicu", NotificationImportance.High);
        var menadzer = (NotificationManager?)kontekst.GetSystemService(Context.NotificationService);
        menadzer?.CreateNotificationChannel(kanal);
    }
}
```

- [ ] **Step 2: Pozovi je iz GPS toka**

U `RouteViewModel.ProvjeriDolazak`, izmijeni granu dolaska:

```csharp
    public void ProvjeriDolazak(double lat, double lon, double? tacnost)
    {
        _zadnjaSirina = lat;
        _zadnjaDuzina = lon;

        var stigao = _detektor.Provjeri(lat, lon, tacnost, Aktivne);
        if (stigao is null) return;

        // Notifikacija ide UVIJEK, bez provjere da li je aplikacija u prvom planu.
        // Takva provjera je krhka pri prelasku iz pozadine, a šteta je nesimetrična:
        // suvišna notifikacija je smetnja, izostala je propuštena dostava. Kad je
        // aplikacija u prvom planu, vozač ionako vidi popup ispod nje.
#if ANDROID
        Platforms.Android.DolazakObavjestenje.Posalji(stigao.Address);
#endif

        PrikaziPopup(stigao);
    }
```

Poziv je iza `#if ANDROID` jer `RouteViewModel.cs` se linkuje u test projekat koji cilja plain `net10.0` i nema Android tipova.

**Namjerno se ne zove iz `PrikaziPopup`**, nego samo odavde: `StigaoSam` (rezervno ručno dugme) takođe zove `PrikaziPopup`, a vozač koji je upravo stisnuo dugme u aplikaciji ne treba notifikaciju o tome.

- [ ] **Step 3: Testovi i build**

```bash
dotnet test EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj
cd EPiljara.Driver && dotnet build -f net10.0-android -c Debug
```

Expected: oboje prolazi. Testovi ne diraju notifikaciju — poziv je iza `#if ANDROID`, pa u test hostu ne postoji.

- [ ] **Step 4: Provjeri kanal na uređaju**

```bash
cd EPiljara.Driver && dotnet build -f net10.0-android -c Debug -t:Run
```

Prijavi se i sačekaj da praćenje krene (obavještenje „tura u toku" u statusnoj traci — znači da je dozvola data). Zatim provjeri kanale:

```bash
ADB="C:/Users/soviljn/AppData/Local/Android/Sdk/platform-tools/adb.exe"
"$ADB" shell dumpsys notification --noredact | grep -A 3 "epiljara_dolazak"
```

Expected: kanal postoji sa `importance=4` (High). Ako ga nema, `Posalji` nije nijednom pozvana — dolazak se još nije desio, što je normalno prije sljedećeg koraka.

- [ ] **Step 5: Odglumi dolazak dok je aplikacija u pozadini**

Ruta za danas ima stanicu 1 na `44.7722, 17.191`. Prebaci aplikaciju u pozadinu (dugme Home), pa pošalji poziciju — **dužina prva**:

```bash
"$ADB" emu geo fix 17.1910 44.7722
```

Expected: za najviše 5 sekundi iskoči notifikacija „Stigli ste na adresu / Reljina 18, Banja Luka" **preko početnog ekrana**. Dodir na nju vraća u aplikaciju, gdje je popup već otvoren.

Snimi ekran i **pogledaj sliku** da potvrdiš:

```bash
"$ADB" exec-out screencap -p > /tmp/dolazak.png
```

Ako notifikacije nema: provjeri da je dozvola za obavještenja data (`"$ADB" shell dumpsys package ba.epiljara.driver | grep POST_NOTIFICATIONS`) i da praćenje uopšte radi.

- [ ] **Step 6: Provjeri da ručno dugme ne šalje notifikaciju**

Vrati se u aplikaciju, zatvori popup, pa stisni „Stigao sam" (vidljivo je samo kad praćenje ne radi — po potrebi isključi lokaciju na emulatoru).

Expected: popup se otvori, **notifikacije nema**.

- [ ] **Step 7: Commituj**

```bash
git add EPiljara.Driver/Platforms/Android/DolazakObavjestenje.cs EPiljara.Driver/ViewModels/RouteViewModel.cs
git commit -m "feat: javi dolazak notifikacijom kad je aplikacija u pozadini"
```

---

## Task 8: Popravka pada pri hladnom startu — ODLOŽEN, NIJE IZVRŠEN

> **Ishod: pokušan, izmjeren kao nedovoljan, odbačen iz ove grane.** Kod ispod je napisan i
> `dotnet test` je davao 103/103, ali je mjerenje na uređaju pokazalo da **pad ostaje** —
> `ForegroundServiceDidNotStartInTimeException` se ponovio dva puta sa instaliranom popravkom.
> Izmjene su vraćene; grana se završava bez njih.
>
> **Zašto nije upalilo, za onoga ko ovo bude nastavljao:** polazna pretpostavka ovog zadatka je
> bila da `OnStartCommand` treba da bude *brz*. Pogrešno postavljeno. `OnStartCommand`
> MAUI-vezanog servisa izvršava se **na glavnoj niti**; ako je glavna nit zauzeta duže od ~5 s
> poslije `startForegroundService()`, tijelo te metode uopšte ne dođe na red — pa je nebitno
> koliko je kratko. Odgoda od 2 s u koraku 2 nije dovoljna.
>
> **Smjer koji vjerovatno vrijedi probati:** ne dizati foreground servis tokom hladnog starta
> uopšte, nego tek kad vozač stvarno krene turu (prvi GPS zahtjev ili izričit dodir). To je
> izmjena ponašanja, ne popravka, pa traži svoj spec.
>
> **Kontekst:** kvar je **pre-postojeći na `main`** (izmjereno 2/4 naspram 4/4 na ovoj grani),
> dakle nije regresija ovog posla. Mjerenja su rađena na emulatoru koji je bio vidno spor i sa
> satom gosta koji odstupa ~1 h od hosta — sljedeći pokušaj traži stabilan uređaj.

**Dodat poslije Task 6**, kad je provjera na uređaju otkrila reproducibilan pad. Nije bio u
prvobitnom planu.

**Files:**
- Modify: `EPiljaraVozac/EPiljara.Driver/Platforms/Android/LocationForegroundService.cs`
- Modify: `EPiljaraVozac/EPiljara.Driver/Services/LocationTracker.cs:75-83`
- Modify: `EPiljaraVozac/EPiljara.Driver/Views/RoutePage.xaml.cs` (`OnAppearing`)

**Interfaces:**
- Consumes: `RouteViewModel.PokreniPracenjeAsync()` (postoji), `LocationForegroundService` (postoji).
- Produces: ništa novo — mijenja se samo redoslijed i zaštita postojećih poziva.

### Izmjereno stanje

Hladan start sa važećom sesijom, 11 s čekanja, po 4 pokušaja:

| Grana | Padova |
|---|---|
| `redizajn-ruta-lista` | 4 / 4 |
| `main` | 2 / 4 |

```
FATAL EXCEPTION: main
android.app.RemoteServiceException$ForegroundServiceDidNotStartInTimeException:
Context.startForegroundService() did not then call Service.startForeground()
```

Kvar je **pre-postojeći na `main`**, ali ga ova grana pogoršava: bez `WebView`-a se ekran diže
brže, pa praćenje kreće ranije u prozoru hladnog starta, a `CollectionView` u istom trenutku mjeri
sedam kartica na glavnoj niti — tačno dok servis treba da pozove `startForeground()`.

**Ključna činjenica o ovom izuzetku:** `ForegroundServiceDidNotStartInTimeException` **ne baca se
na mjestu poziva** `StartForegroundService`. Android ga isporučuje naknadno, na glavni looper
aplikacije. Zato `try/catch` oko `StartForegroundService` **neće** uhvatiti ovaj pad — hvata samo
srodni `ForegroundServiceStartNotAllowedException`. Popravka mora ukloniti uzrok, ne samo ogradu.

- [ ] **Step 1: Neka servis pozove `StartForeground` kao prvu radnju**

Android daje ~5 sekundi od `startForegroundService()` do `startForeground()`. Trenutno
`OnStartCommand` prvo pravi kanal, pa gradi obavještenje, pa tek onda zove `StartForeground`.
Kanal se pravi na svaki poziv, iako se mijenja samo jednom.

U `LocationForegroundService.cs`, prebaci pravljenje kanala u `OnCreate` i skrati `OnStartCommand`:

```csharp
    /// <summary>Kanal se pravi jednom, pri stvaranju servisa — ne na svakom OnStartCommand.
    /// Android daje ~5 s od startForegroundService() do startForeground(); sve što stoji
    /// ispred tog poziva troši taj budžet.</summary>
    public override void OnCreate()
    {
        base.OnCreate();
        NapraviKanal();
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        // StartForeground je PRVA radnja, bez ičega ispred sebe. Ako se ovaj poziv ne desi
        // na vrijeme, Android ubija proces sa ForegroundServiceDidNotStartInTimeException —
        // izuzetkom koji se ne baca ovdje nego naknadno, na glavni looper, pa ga nikakav
        // try/catch oko poziva ne može uhvatiti.
        var obavjestenje = new NotificationCompat.Builder(this, KanalId)
            .SetContentTitle("E piljara — tura u toku")
            .SetContentText("Pratimo lokaciju da bismo javili kad stignete na adresu.")
            .SetSmallIcon(global::Android.Resource.Drawable.IcMenuMyLocation)
            .SetOngoing(true)
            .Build();

        StartForeground(ObavjestenjeId, obavjestenje);
        return StartCommandResult.NotSticky;
    }
```

`NapraviKanal` ostaje nepromijenjen, samo se više ne zove iz `OnStartCommand`.

- [ ] **Step 2: Skloni pokretanje praćenja sa kritičnog puta hladnog starta**

`OnStartCommand` se izvršava na **glavnoj niti**. Ako je glavna nit zauzeta podizanjem MAUI-ja,
učitavanjem rute i mjerenjem sedam kartica, servis ne stigne na red. Zato pokretanje praćenja ne
smije padati u isti trenutak kad i prvo iscrtavanje.

U `RoutePage.xaml.cs`, zamijeni `OnAppearing`:

```csharp
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.UcitajCommand.Execute(null);

        // Praćenje se NE pokreće odmah. OnStartCommand foreground servisa ide na glavnu nit,
        // a ona je pri hladnom startu zauzeta podizanjem MAUI-ja i mjerenjem liste stanica —
        // servis tada ne stigne pozvati startForeground() u roku i Android ubije proces.
        // Mjereno: bez ove odgode pad se javljao na svakom hladnom startu.
        // Nije `await` jer OnAppearing ne smije biti `async void` bez hvatača: aplikacija
        // nema globalni handler neuhvaćenih izuzetaka.
        Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(2), () => _ = PokreniPracenjeSigurno());
    }

    /// <summary>Guta izuzetke namjerno — poziva se iz odgođenog povratnog poziva koji nema
    /// nikoga iznad sebe da uhvati grešku, a neuspjelo praćenje ne smije oboriti aplikaciju.</summary>
    private async Task PokreniPracenjeSigurno()
    {
        try { await _vm.PokreniPracenjeAsync(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[RoutePage] praćenje nije pokrenuto: {ex}"); }
    }
```

Metoda više nije `async void` — to usput uklanja jedan od `async void` lanaca bez hvatača.

- [ ] **Step 3: Zaštiti `StartForegroundService` od srodnog izuzetka**

Ovo je nalaz I4 iz ranijeg review-a: `StartForegroundService` je jedini nezaštićen platformski
poziv na lancu bez ijednog `catch`, dok su susjedna dva zaštićena. Ne rješava pad iz Step 1-2
(vidi napomenu iznad), ali zatvara `ForegroundServiceStartNotAllowedException` na API 31+.

U `LocationTracker.cs`, umotaj blok iz linija 75-83:

```csharp
#if ANDROID
        // ForegroundServiceStartNotAllowedException (API 31+) leti kad se servis pokuša dići
        // iz pozadine. Susjedni platformski pozivi su zaštićeni iz istog razloga.
        // NAPOMENA: ovo NE hvata ForegroundServiceDidNotStartInTimeException — taj Android
        // isporučuje naknadno na glavni looper, ne na mjestu poziva.
        try
        {
            var intent = new global::Android.Content.Intent(
                global::Android.App.Application.Context,
                typeof(Platforms.Android.LocationForegroundService));
            if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.O)
                global::Android.App.Application.Context.StartForegroundService(intent);
            else
                global::Android.App.Application.Context.StartService(intent);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LocationTracker] foreground servis nije pokrenut: {ex}");
            return false;
        }
#endif
```

`return false` jer praćenje tada stvarno ne radi — `PracenjeRadi` mora ostati u skladu sa
stvarnošću, inače rezervno dugme „Stigao sam" nestane sa ekrana bez razloga.

- [ ] **Step 4: Testovi**

Run: `dotnet test EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj`
Expected: 103/103 PASS. Ovaj zadatak ne dodaje testove — pad se ne može reprodukovati u test
hostu (nema Androida), pa je kapija mjerenje na uređaju u sljedećem koraku.

- [ ] **Step 5: Kapija — osam hladnih startova bez pada**

```bash
cd EPiljara.Driver && dotnet build -f net10.0-android -c Debug -t:Run
```

Prijavi se kao `vozac@epiljara.ba` / `vozac123` da sesija ostane sačuvana, pa mjeri:

```bash
ADB="C:/Users/soviljn/AppData/Local/Android/Sdk/platform-tools/adb.exe"
pao=0
for i in $(seq 1 8); do
  "$ADB" logcat -c
  "$ADB" shell am force-stop ba.epiljara.driver >/dev/null 2>&1; sleep 2
  "$ADB" shell monkey -p ba.epiljara.driver -c android.intent.category.LAUNCHER 1 >/dev/null 2>&1
  sleep 11
  pid=$("$ADB" shell "pidof ba.epiljara.driver" 2>/dev/null | tr -d '\r')
  fat=$("$ADB" logcat -d 2>&1 | grep -icE "ForegroundServiceDidNotStartInTime|FATAL EXCEPTION")
  if [ -z "$pid" ] || [ "$fat" -gt 0 ]; then pao=$((pao+1)); echo "pokusaj $i: PAO"; else echo "pokusaj $i: ok"; fi
done
echo "=== palo $pao od 8 ==="
```

Expected: **0 od 8**. Bilo koji pad znači da popravka nije potpuna — prijavi BLOCKED sa ispisom,
ne commituj.

- [ ] **Step 6: Potvrdi da praćenje i dalje radi**

Odgoda od 2 s ne smije ubiti funkciju koju štiti. Poslije uspješnog starta sačekaj ~15 s, pa:

```bash
"$ADB" shell dumpsys notification --noredact | grep -c "tura u toku"
```

Expected: rezultat veći od nule — obavještenje „E piljara — tura u toku" stoji u statusnoj traci,
dakle foreground servis radi. Snimi i ekran, pa **pogledaj sliku** da potvrdiš da je lista stanica
tu i da nema žutog bannera o isključenoj lokaciji.

- [ ] **Step 7: Commit**

```bash
git add EPiljara.Driver/Platforms/Android/LocationForegroundService.cs \
        EPiljara.Driver/Services/LocationTracker.cs \
        EPiljara.Driver/Views/RoutePage.xaml.cs
git commit -m "fix: ne obaraj aplikaciju kad foreground servis ne stigne na vrijeme pri hladnom startu"
```

---

## Završna provjera

- [ ] Cijela svita: `dotnet test EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj` — sve zeleno
- [ ] Build: `cd EPiljara.Driver && dotnet build -f net10.0-android -c Debug` — 0 grešaka, 0 upozorenja
- [ ] Na uređaju, cio tok: prijava → lista stanica → dodir na karticu otvara stanicu → „Primopredaja izvršena" → kartica prelazi u zelenu i precrtanu, traka i napredak se osvježe
- [ ] `git status` čist u oba repozitorijuma
- [ ] Dopuni `EPiljaraVozac/CLAUDE.md`: sekcija „The map is a WebView, not a native control" više ne važi i treba je zamijeniti opisom liste; dodaj kanal notifikacije uz sekciju o dozvolama; ukloni napomenu o OSM pločicama iz privatnih otvorenih stavki jer je zatvorena
