# Mobilna aplikacija za vozače — Faza 1, plan implementacije

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Vozač na Android telefonu se prijavi, vidi svoje stanice za danas i poslije svake isporuke potvrdi primopredaju — nakon čega stanica nestaje iz aktivne liste.

**Architecture:** .NET MAUI aplikacija (samo Android) razgovara sa postojećim `EPiljara.API` isključivo preko HTTP-a. DTO-ovi se preslikavaju, ne dijele — mobilna aplikacija ne referencira nijedan backend projekat. MVVM sa `CommunityToolkit.Mvvm`; tri stranice povezane `Shell` navigacijom. Backend se dira na dva mjesta: `RouteStopDto` dobija `Status`, a `UpdateStopStatusHandler` sam pregura narudžbu u `OutForDelivery`.

**Tech Stack:** .NET 10 SDK (10.0.300), .NET MAUI (`net10.0-android`), `CommunityToolkit.Mvvm` 8.x, `Microsoft.Extensions.Configuration.Json`, xUnit za testove logike, ASP.NET Core 9 backend (postojeći).

**Spec:** `docs/superpowers/specs/2026-08-17-driver-mobile-app-design.md`

## Global Constraints

- **Jezik svih korisnički vidljivih stringova je srpski.** Bez izuzetka — dugmad, poruke o greškama, prazna stanja.
- **Target framework je `net10.0-android` i ništa drugo.** Spec je pisao `net9.0-android`; na ovoj mašini su instalirani workload manifesti za SDK band 10.0.100 (`android 36.1.43/10.0.100`, `maui-windows 10.0.20/10.0.100`) i `dotnet new maui` generiše `net10.0`. TFM-ove za `ios`, `maccatalyst` i `windows` **obrisati** — bez iOS TFM-a build ne traži Mac.
- **Mobilni repo ne dobija `global.json`.** Backend repo pinuje SDK 9.0.310 u svom `backend/global.json`, ali to važi samo tamo; mobilni repo mora ostati na SDK 10.
- **Svaki HTTP odgovor je envelope `{ data, error, meta }`** — i uspjeh i greška. Nikad ne čitati tijelo odgovora mimo `ApiClient`-a.
- **DTO-ovi se prepisuju ručno i drže 1:1 sa backendom.** Zabranjeno je referencirati `EPiljara.Application` ili bilo koji backend projekat iz mobilnog repoa.
- **Bazni URL API-ja nije hardkodiran** — čita se iz `Resources/Raw/appsettings.json`.
- **Commit poslije svakog taska.**

## Dva repozitorija

Posao se dijeli na dva odvojena git repozitorija. Svaki task pripada tačno jednom
— dispatch svakog taska nosi punu putanju, nikad ne pretpostavljaj radni folder.

| Repo | Putanja | Grana | Taskovi |
|---|---|---|---|
| **Mobilni** (novi) | `C:\Users\soviljn\Desktop\EPiljaraVozac` | `main` | 1, 2, 5, 6, 7 |
| **Backend** (worktree) | `C:\Users\soviljn\Desktop\epiljara-backend-wt` | `driver-app-backend` | 3, 4, 8 |

Backend worktree je izdvojena kopija repoa `Kumov Web Shop`; njegov glavni radni
folder ima nekommitovane izmjene koje **se ne diraju**.

Mobilni repo već sadrži `README.md` i `.gitignore` iz inicijalnog commita —
`.gitignore` već hvata `bin/` i `obj/`.

## Struktura fajlova

U mobilnom repou (`EPiljaraVozac/`), sve u korijenu — nema `mobile/` omotača:

```
  EPiljara.Driver.sln                       solution za mobilni dio
  EPiljara.Driver/
    EPiljara.Driver.csproj                  net10.0-android
    MauiProgram.cs                          DI: konfiguracija, HttpClient, servisi, ViewModels
    App.xaml(.cs), AppShell.xaml(.cs)       Shell rute
    Platforms/Android/AndroidManifest.xml   INTERNET (već postoji) + cleartext samo u Debug-u
    Resources/Raw/appsettings.json          { "apiBaseUrl": "http://10.0.2.2:5080" }
    Models/Dtos.cs                          envelope + AuthResultDto + DeliveryRouteDto + RouteStopDto
    Services/
      ApiClient.cs                          raspakuje envelope, baca ApiException(code, message)
      AuthHeaderHandler.cs                  Bearer token na svaki zahtjev
      AuthService.cs                        login, token u SecureStorage, odjava
      DriverService.cs                      GetRouteAsync / UpdateStopStatusAsync
    ViewModels/
      LoginViewModel.cs
      RouteViewModel.cs                     dijeli stanice na aktivne i završene
      StopViewModel.cs
    Views/
      LoginPage.xaml(.cs)
      RoutePage.xaml(.cs)
      StopPage.xaml(.cs)
  EPiljara.Driver.Tests/
    EPiljara.Driver.Tests.csproj            net10.0, xUnit
    ApiClientTests.cs
    RouteViewModelTests.cs
```

U backend repou (`epiljara-backend-wt/`) se dira samo ovo:
- `backend/src/EPiljara.Application/DTOs/AdminDtos.cs` — `RouteStopDto.Status`
- `backend/src/EPiljara.Application/Mapping/MappingExtensions.cs` — mapiranje tog polja
- `backend/src/EPiljara.Application/Features/Driver/DriverFeature.cs` — preguravanje statusa
- `CLAUDE.md` — jezik i pomen mobilnog repoa

---

### Task 1: Skeleton MAUI projekta koji se builda

**Files:**
- Create: `EPiljara.Driver/` (iz `dotnet new maui`)
- Create: `EPiljara.Driver.sln`
- Modify: `EPiljara.Driver/EPiljara.Driver.csproj`

**Interfaces:**
- Consumes: ništa
- Produces: projekat `EPiljara.Driver` sa root namespace-om `EPiljara.Driver`, koji se builda za `net10.0-android`

- [ ] **Step 1: Provjeri da su alati na mjestu**

```bash
dotnet --version          # očekivano: 10.0.300 ili novije
dotnet workload list      # u listi moraju biti: android, maui-windows
dotnet new list maui      # mora postojati template ".NET MAUI App" (short name: maui)
```

Ako `dotnet new list maui` ne vrati ništa: `dotnet workload install maui`.

- [ ] **Step 2: Napravi projekat**

Repo već postoji i ima `README.md` i `.gitignore` — projekat ide u njegov korijen,
bez ikakvog `mobile/` podfoldera.

```bash
cd "C:/Users/soviljn/Desktop/EPiljaraVozac"
dotnet new maui -n EPiljara.Driver
dotnet new sln -n EPiljara.Driver
dotnet sln EPiljara.Driver.sln add EPiljara.Driver/EPiljara.Driver.csproj
```

- [ ] **Step 3: Svedi na samo Android i postavi identitet aplikacije**

U `EPiljara.Driver/EPiljara.Driver.csproj` **obriši** ova tri reda:

```xml
<TargetFrameworks Condition="!$([MSBuild]::IsOSPlatform('linux'))">$(TargetFrameworks);net10.0-ios;net10.0-maccatalyst</TargetFrameworks>
<TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('windows'))">$(TargetFrameworks);net10.0-windows10.0.19041.0</TargetFrameworks>
<WindowsPackageType>None</WindowsPackageType>
```

Ostaje samo `<TargetFrameworks>net10.0-android</TargetFrameworks>`.

Zatim zamijeni vrijednosti:

```xml
<ApplicationTitle>E piljara — vozač</ApplicationTitle>
<ApplicationId>ba.epiljara.driver</ApplicationId>
```

- [ ] **Step 4: Dodaj MVVM i konfiguracione pakete**

```bash
cd "C:/Users/soviljn/Desktop/EPiljaraVozac/EPiljara.Driver"
dotnet add package CommunityToolkit.Mvvm
dotnet add package Microsoft.Extensions.Configuration.Json
dotnet add package Microsoft.Extensions.Http
```

- [ ] **Step 5: Build i potvrdi da prolazi**

```bash
cd "C:/Users/soviljn/Desktop/EPiljaraVozac/EPiljara.Driver"
dotnet build -f net10.0-android -c Debug
```

Expected: `Build succeeded. 0 Error(s)`. Prvi build traje oko 2 minuta.

Ovaj build je unaprijed provjeren na ovoj mašini i prolazi za oko 2 minuta, čak i na dužoj putanji sa razmakom u imenu. Putanja mobilnog repoa (`Desktop\EPiljaraVozac`) je kratka, pa problema ne bi trebalo biti. Ako ipak pukne sa **`error APT2098: ... failed to open file`**, to je Windows MAX_PATH ograničenje u `aapt2`, ne greška u kodu. Rješenje: skrati međuputanju dodavanjem u `csproj` unutar glavnog `<PropertyGroup>`:

```xml
<IntermediateOutputPath>C:\o\epd\</IntermediateOutputPath>
```

- [ ] **Step 6: Ne commituj build izlaz**

`.gitignore` iz inicijalnog commita već hvata `bin/` i `obj/`. Potvrdi to:

```bash
cd "C:/Users/soviljn/Desktop/EPiljaraVozac"
git status --short
```

Expected: nema nijednog `EPiljara.Driver/bin/...` ni `obj/...` reda. Ako ih ima, dodaj u `.gitignore`:

```
**/bin/
**/obj/
```

- [ ] **Step 7: Commit**

```bash
cd "C:/Users/soviljn/Desktop/EPiljaraVozac"
git add -A
git commit -m "feat: scaffold MAUI driver app (Android only)"
```

---

### Task 2: DTO-ovi, konfiguracija i ApiClient koji raspakuje envelope

**Files:**
- Create: `EPiljara.Driver/Models/Dtos.cs`
- Create: `EPiljara.Driver/Services/ApiClient.cs`
- Create: `EPiljara.Driver/Resources/Raw/appsettings.json`
- Create: `EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj`
- Create: `EPiljara.Driver.Tests/ApiClientTests.cs`
- Modify: `EPiljara.Driver/Platforms/Android/AndroidManifest.xml`

**Interfaces:**
- Consumes: projekat iz Taska 1
- Produces:
  - `EPiljara.Driver.Models.ApiEnvelope<T>(T? Data, ApiErrorBody? Error)`
  - `EPiljara.Driver.Models.ApiErrorBody(string Code, string Message)`
  - `EPiljara.Driver.Models.AuthUserDto(Guid Id, string Name, string Email, string? Phone, string Role)`
  - `EPiljara.Driver.Models.AuthResultDto(string Token, AuthUserDto User)`
  - `EPiljara.Driver.Models.RouteStopDto(Guid OrderId, string OrderCode, string CustomerName, string Address, string ZoneName, int Sequence, string? CustomerPhone, string Status)`
  - `EPiljara.Driver.Models.DeliveryRouteDto(Guid Id, string Date, Guid DriverId, string? DriverName, string Status, IReadOnlyList<RouteStopDto> Stops)`
  - `EPiljara.Driver.Services.ApiException(string Code, string message)` — `Code` je javno svojstvo
  - `EPiljara.Driver.Services.ApiClient.SendAsync<T>(HttpRequestMessage, CancellationToken) → Task<T?>`

- [ ] **Step 1: Napravi test projekat i zakači ga na solution**

```bash
cd "C:/Users/soviljn/Desktop/EPiljaraVozac"
dotnet new xunit -n EPiljara.Driver.Tests -f net10.0
dotnet sln EPiljara.Driver.sln add EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj
```

Test projekat **ne** referencira MAUI projekat (MAUI cilja Android, test cilja `net10.0` — reference se ne slažu). Umjesto toga, `Dtos.cs` i `ApiClient.cs` se u test projekat uvlače kao linkovani fajlovi. Dodaj u `EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj` unutar novog `<ItemGroup>`:

```xml
<ItemGroup>
  <Compile Include="..\EPiljara.Driver\Models\Dtos.cs" Link="Models\Dtos.cs" />
  <Compile Include="..\EPiljara.Driver\Services\ApiClient.cs" Link="Services\ApiClient.cs" />
  <Compile Include="..\EPiljara.Driver\ViewModels\RouteViewModel.cs" Link="ViewModels\RouteViewModel.cs" />
</ItemGroup>
```

`RouteViewModel.cs` još ne postoji — nastaje u Tasku 6. Do tada zakomentariši taj red, inače restore puca.

- [ ] **Step 2: Napiši testove koji padaju**

`EPiljara.Driver.Tests/ApiClientTests.cs`:

```csharp
using System.Net;
using System.Text;
using EPiljara.Driver.Models;
using EPiljara.Driver.Services;

namespace EPiljara.Driver.Tests;

/// <summary>Vraća unaprijed zadati odgovor, bez ijednog mrežnog poziva.</summary>
file class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });
}

file class ThrowingHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) =>
        throw new HttpRequestException("connection refused");
}

public class ApiClientTests
{
    private static ApiClient Client(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5080") });

    private static HttpRequestMessage Get() => new(HttpMethod.Get, "driver/route");

    [Fact]
    public async Task Raspakuje_data_iz_envelope_a()
    {
        const string body = """
        {"data":{"token":"abc","user":{"id":"11111111-1111-1111-1111-111111111111","name":"Mladen","email":"vozac@epiljara.ba","phone":null,"role":"driver"}},"meta":null}
        """;
        var client = Client(new StubHandler(HttpStatusCode.OK, body));

        var result = await client.SendAsync<AuthResultDto>(Get());

        Assert.NotNull(result);
        Assert.Equal("abc", result!.Token);
        Assert.Equal("driver", result.User.Role);
    }

    [Fact]
    public async Task Baca_ApiException_sa_kodom_kad_envelope_nosi_error()
    {
        const string body = """
        {"data":null,"error":{"code":"invalid_credentials","message":"Pogrešan email ili lozinka."}}
        """;
        var client = Client(new StubHandler(HttpStatusCode.Unauthorized, body));

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.SendAsync<AuthResultDto>(Get()));

        Assert.Equal("invalid_credentials", ex.Code);
        Assert.Equal("Pogrešan email ili lozinka.", ex.Message);
    }

    [Fact]
    public async Task Vraca_null_kad_vozac_nema_rutu_za_danas()
    {
        const string body = """{"data":null,"meta":null}""";
        var client = Client(new StubHandler(HttpStatusCode.OK, body));

        var result = await client.SendAsync<DeliveryRouteDto>(Get());

        Assert.Null(result);
    }

    [Fact]
    public async Task Mrezna_greska_postaje_network_error()
    {
        var client = Client(new ThrowingHandler());

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.SendAsync<DeliveryRouteDto>(Get()));

        Assert.Equal("network_error", ex.Code);
        Assert.Equal("Nema veze sa serverom.", ex.Message);
    }

    [Fact]
    public async Task Neispravan_json_ne_rusi_aplikaciju()
    {
        var client = Client(new StubHandler(HttpStatusCode.InternalServerError, "<html>502</html>"));

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.SendAsync<DeliveryRouteDto>(Get()));

        Assert.Equal("server_error", ex.Code);
    }

    [Fact]
    public async Task Prazan_401_iz_auth_middleware_a_znaci_istekla_sesija()
    {
        // Istekao JWT ne prolazi kroz ExceptionHandlingMiddleware — tijelo je prazno.
        var client = Client(new StubHandler(HttpStatusCode.Unauthorized, ""));

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.SendAsync<DeliveryRouteDto>(Get()));

        Assert.Equal("unauthorized", ex.Code);
        Assert.Equal("Sesija je istekla. Prijavite se ponovo.", ex.Message);
    }
}
```

- [ ] **Step 3: Pokreni testove i potvrdi da padaju**

```bash
cd "C:/Users/soviljn/Desktop/EPiljaraVozac"
dotnet test EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj
```

Expected: FAIL — greška prevođenja, `ApiClient` i `ApiEnvelope` ne postoje.

- [ ] **Step 4: Napiši DTO-ove**

`EPiljara.Driver/Models/Dtos.cs`:

```csharp
namespace EPiljara.Driver.Models;

/// <summary>
/// Omotač koji backend vraća na svaki poziv — i uspjeh i grešku.
/// Preslikano iz EPiljara.Application.Common.Models.ApiResponse&lt;T&gt;.
/// </summary>
public record ApiEnvelope<T>(T? Data, ApiErrorBody? Error);

public record ApiErrorBody(string Code, string Message);

public record AuthUserDto(
    Guid Id,
    string Name,
    string Email,
    string? Phone,
    string Role);

public record AuthResultDto(string Token, AuthUserDto User);

/// <summary>Jedna stanica na ruti. 1:1 sa EPiljara.Application.DTOs.RouteStopDto.</summary>
public record RouteStopDto(
    Guid OrderId,
    string OrderCode,
    string CustomerName,
    string Address,
    string ZoneName,
    int Sequence,
    string? CustomerPhone,
    string Status);

public record DeliveryRouteDto(
    Guid Id,
    string Date,
    Guid DriverId,
    string? DriverName,
    string Status,
    IReadOnlyList<RouteStopDto> Stops);
```

- [ ] **Step 5: Napiši ApiClient**

`EPiljara.Driver/Services/ApiClient.cs`:

```csharp
using System.Text.Json;
using EPiljara.Driver.Models;

namespace EPiljara.Driver.Services;

/// <summary>Greška koju je backend vratio u envelope-u, ili mrežni problem.</summary>
public class ApiException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>
/// Jedina tačka kroz koju aplikacija priča sa backendom. Raspakuje
/// { data, error, meta } i pretvara svaki neuspjeh u ApiException sa kodom.
/// </summary>
public class ApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<T?> SendAsync<T>(HttpRequestMessage request, CancellationToken ct = default)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            throw new ApiException("network_error", "Nema veze sa serverom.");
        }

        var body = await response.Content.ReadAsStringAsync(ct);

        // Istekao ili neispravan JWT ne prolazi kroz ExceptionHandlingMiddleware —
        // auth middleware vraća goli 401 bez tijela, pa envelope ne postoji.
        if (string.IsNullOrWhiteSpace(body))
        {
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                throw new ApiException("unauthorized", "Sesija je istekla. Prijavite se ponovo.");
            if (response.IsSuccessStatusCode) return default;
            throw new ApiException("server_error", "Došlo je do neočekivane greške.");
        }

        ApiEnvelope<T>? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<ApiEnvelope<T>>(body, Json);
        }
        catch (JsonException)
        {
            throw new ApiException("server_error", "Došlo je do neočekivane greške.");
        }

        if (envelope?.Error is { } error)
            throw new ApiException(error.Code, error.Message);

        if (!response.IsSuccessStatusCode)
            throw new ApiException("server_error", "Došlo je do neočekivane greške.");

        return envelope is null ? default : envelope.Data;
    }
}
```

- [ ] **Step 6: Pokreni testove i potvrdi da prolaze**

```bash
cd "C:/Users/soviljn/Desktop/EPiljaraVozac"
dotnet test EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj
```

Expected: `Passed! - Failed: 0, Passed: 6`

- [ ] **Step 7: Dodaj konfiguraciju baznog URL-a**

`EPiljara.Driver/Resources/Raw/appsettings.json`:

```json
{
  "apiBaseUrl": "http://10.0.2.2:5080"
}
```

`10.0.2.2` je adresa host računara **viđena iz Android emulatora** — `localhost` iz emulatora pokazuje na sam emulator. Za pravi telefon na istoj Wi-Fi mreži zamijeni IP-om računara, npr. `http://192.168.1.10:5080`.

- [ ] **Step 8: Dozvoli običan HTTP samo u Debug build-u**

U `EPiljara.Driver/Platforms/Android/AndroidManifest.xml`, dodaj `usesCleartextTraffic` na `<application>` element:

```xml
<?xml version="1.0" encoding="utf-8"?>
<manifest xmlns:android="http://schemas.android.com/apk/res/android">
	<application android:allowBackup="true" android:icon="@mipmap/appicon" android:roundIcon="@mipmap/appicon_round" android:supportsRtl="true" android:usesCleartextTraffic="${cleartextTraffic}"></application>
	<uses-permission android:name="android.permission.ACCESS_NETWORK_STATE" />
	<uses-permission android:name="android.permission.INTERNET" />
</manifest>
```

ISPRAVKA (utvrdjeno tokom implementacije): `AndroidUseCleartextTraffic` NIJE stvarno MSBuild svojstvo u ovom SDK-u — tiho nema efekta, a eksplicitan atribut iz izvornog manifesta pobjeduje u obje konfiguracije. Koristi `AndroidManifestPlaceholders`: u manifestu upisi `android:usesCleartextTraffic="${cleartextTraffic}"`, a u `EPiljara.Driver.csproj` dodaj:

```xml
<PropertyGroup Condition="'$(Configuration)' == 'Debug'">
  <AndroidManifestPlaceholders>cleartextTraffic=true</AndroidManifestPlaceholders>
</PropertyGroup>
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <AndroidManifestPlaceholders>cleartextTraffic=false</AndroidManifestPlaceholders>
</PropertyGroup>
```

Provjeri tako sto build-ujes obje konfiguracije i citas SPOJENI manifest u `obj/<Config>/net10.0-android/android/AndroidManifest.xml` — Debug mora biti `true`, Release `false`.

Bez ovoga svaki poziv na `http://` pukne bez jasne poruke — Android blokira običan HTTP od verzije 9.

- [ ] **Step 9: Poveži konfiguraciju i HttpClient u DI**

Zamijeni sadržaj `EPiljara.Driver/MauiProgram.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using EPiljara.Driver.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EPiljara.Driver;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		using var stream = FileSystem.OpenAppPackageFileAsync("appsettings.json")
			.GetAwaiter().GetResult();
		builder.Configuration.AddJsonStream(stream);

		var baseUrl = builder.Configuration["apiBaseUrl"]
			?? throw new InvalidOperationException("apiBaseUrl nedostaje u appsettings.json");

		builder.Services.AddSingleton(_ => new HttpClient { BaseAddress = new Uri(baseUrl) });
		builder.Services.AddSingleton<ApiClient>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
```

- [ ] **Step 10: Build i commit**

```bash
cd "C:/Users/soviljn/Desktop/EPiljaraVozac/EPiljara.Driver"
dotnet build -f net10.0-android -c Debug
cd "C:/Users/soviljn/Desktop/EPiljaraVozac"
git add -A
git commit -m "feat(mobile): DTOs, envelope-unwrapping ApiClient, config + tests"
```

Expected: build prolazi, testovi prolaze.

---

### Task 3: Backend — `RouteStopDto` dobija `Status`

**Files:**
- Modify: `backend/src/EPiljara.Application/DTOs/AdminDtos.cs:10-20`
- Modify: `backend/src/EPiljara.Application/Mapping/MappingExtensions.cs:128-137`

**Interfaces:**
- Consumes: ništa iz ranijih taskova
- Produces: `RouteStopDto.Status` (string, npr. `"Confirmed"`, `"OutForDelivery"`, `"Delivered"`, `"FailedDelivery"`) u odgovoru na `GET /driver/route` i `GET /admin/routes`

- [ ] **Step 1: Dodaj polje u DTO**

U `backend/src/EPiljara.Application/DTOs/AdminDtos.cs`, u `RouteStopDto`, poslije `CustomerPhone`:

```csharp
    /// <summary>Order status, so the driver app knows which stops are done.</summary>
    public string Status { get; init; } = "Confirmed";
```

- [ ] **Step 2: Popuni ga u mapiranju**

U `backend/src/EPiljara.Application/Mapping/MappingExtensions.cs`, u `ToDto(this RouteStop s)`, poslije `CustomerPhone = s.Order?.CustomerPhone,`:

```csharp
        Status = s.Order?.Status.ToString() ?? "Confirmed",
```

- [ ] **Step 3: Build backend**

```bash
cd "C:/Users/soviljn/Desktop/epiljara-backend-wt/backend"
dotnet build
```

Expected: `Build succeeded`. Migracija **nije** potrebna — `Status` već postoji na `Order` entitetu, mijenja se samo DTO.

- [ ] **Step 4: Provjeri kroz stvarni poziv**

Pokreni bazu i API:

```bash
cd "C:/Users/soviljn/Desktop/epiljara-backend-wt/backend"
docker compose up -d postgres
dotnet run --project src/EPiljara.API
```

U drugom terminalu, prijavi se kao admin i dohvati rute za danas:

```bash
TOKEN=$(curl -s -X POST http://localhost:5080/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@epiljara.ba","password":"admin123"}' \
  | sed -n 's/.*"token":"\([^"]*\)".*/\1/p')
curl -s "http://localhost:5080/admin/routes?date=$(date +%F)" \
  -H "Authorization: Bearer $TOKEN"
```

Expected: ako rute postoje, svaka stanica ima `"status"` polje. Ako je lista prazna, to je uredu — rute se prave u Tasku 8; dovoljno je da poziv vrati `200` sa `{"data":[],"meta":{"total":0}}`.

- [ ] **Step 5: Commit**

```bash
cd "C:/Users/soviljn/Desktop/epiljara-backend-wt"
git add backend/src/EPiljara.Application
git commit -m "feat(api): expose order status on RouteStopDto for the driver app"
```

---

### Task 4: Backend — vozačeva potvrda pregura narudžbu u `OutForDelivery`

**Files:**
- Modify: `backend/src/EPiljara.Application/Features/Driver/DriverFeature.cs:45-68`

**Interfaces:**
- Consumes: ništa iz ranijih taskova
- Produces: `PATCH /driver/orders/{id}/status` više ne vraća `invalid_transition` za narudžbu u statusu `Confirmed` ili `Packing`

**Zašto:** mašina stanja dozvoljava `Delivered` samo iz `OutForDelivery` (`Order.Transitions`), a admin panel dozvoljava dodjelu rute narudžbama u statusu `Confirmed`, `Packing` ili `OutForDelivery` (`ASSIGNABLE` u `frontend/src/app/admin/routes/page.tsx:15`). Bez ove izmjene vozač dobije `400` na potpuno normalnom toku.

- [ ] **Step 1: Izmijeni handler**

U `backend/src/EPiljara.Application/Features/Driver/DriverFeature.cs`, u `UpdateStopStatusHandler.Handle`, zamijeni red `order.TransitionTo(status);` sa:

```csharp
        // Ruta se može dodijeliti narudžbi koja je još Confirmed/Packing (vidi ASSIGNABLE
        // u admin panelu). Mašina stanja dozvoljava Delivered samo iz OutForDelivery, pa je
        // ovdje prevodimo kroz međukorake — svaki pojedinačni prelaz je dozvoljen.
        if (order.Status is OrderStatus.Confirmed) order.TransitionTo(OrderStatus.Packing);
        if (order.Status is OrderStatus.Packing) order.TransitionTo(OrderStatus.OutForDelivery);
        order.TransitionTo(status);
```

- [ ] **Step 2: Build**

```bash
cd "C:/Users/soviljn/Desktop/epiljara-backend-wt/backend"
dotnet build
```

Expected: `Build succeeded`.

- [ ] **Step 3: Provjeri na stvarnoj narudžbi**

Uz pokrenut API, kao admin napravi rutu za vozača Mladen sa narudžbom koja je u statusu `Confirmed`, pa kao vozač potvrdi primopredaju:

```bash
DRIVER=$(curl -s -X POST http://localhost:5080/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"vozac@epiljara.ba","password":"vozac123"}' \
  | sed -n 's/.*"token":"\([^"]*\)".*/\1/p')
curl -s http://localhost:5080/driver/route -H "Authorization: Bearer $DRIVER"
# uzmi orderId prve stanice iz odgovora, pa:
curl -s -X PATCH http://localhost:5080/driver/orders/<ORDER_ID>/status \
  -H "Authorization: Bearer $DRIVER" -H "Content-Type: application/json" \
  -d '{"status":"Delivered"}'
```

Expected: `200` i `"status":"Delivered"` u odgovoru. Prije ove izmjene isti poziv bi vratio `400 invalid_transition`.

Ako nemaš rutu, prvo je napravi kroz web admin na `http://localhost:3000/admin/routes` (vidi Task 8, Step 2).

- [ ] **Step 4: Commit**

```bash
cd "C:/Users/soviljn/Desktop/epiljara-backend-wt"
git add backend/src/EPiljara.Application/Features/Driver/DriverFeature.cs
git commit -m "fix(api): advance order through OutForDelivery on driver confirmation"
```

---

### Task 5: Prijava vozača

**Files:**
- Create: `EPiljara.Driver/Services/AuthService.cs`
- Create: `EPiljara.Driver/Services/AuthHeaderHandler.cs`
- Create: `EPiljara.Driver/ViewModels/LoginViewModel.cs`
- Create: `EPiljara.Driver/Views/LoginPage.xaml`, `Views/LoginPage.xaml.cs`
- Modify: `EPiljara.Driver/MauiProgram.cs`
- Modify: `EPiljara.Driver/AppShell.xaml`, `AppShell.xaml.cs`
- Delete: `EPiljara.Driver/MainPage.xaml`, `MainPage.xaml.cs`

**Interfaces:**
- Consumes: `ApiClient.SendAsync<T>`, `AuthResultDto`, `ApiException` (Task 2)
- Produces:
  - `AuthService.LoginAsync(string email, string password) → Task` — baca `ApiException` na neuspjeh
  - `AuthService.GetTokenAsync() → Task<string?>`
  - `AuthService.LogoutAsync() → Task`
  - `AuthService.DriverName` → `string?`
  - Shell rute: `"login"`, `"route"`

- [ ] **Step 1: Napiši AuthService**

`EPiljara.Driver/Services/AuthService.cs`:

```csharp
using System.Net.Http.Json;
using EPiljara.Driver.Models;

namespace EPiljara.Driver.Services;

/// <summary>
/// Prijava vozača i čuvanje tokena. Token ide u SecureStorage (Android Keystore),
/// da se vozač ne prijavljuje svaki dan.
/// </summary>
public class AuthService(ApiClient api)
{
    private const string TokenKey = "epiljara.driver.token";
    private const string NameKey = "epiljara.driver.name";

    public string? DriverName { get; private set; }

    public async Task LoginAsync(string email, string password)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "auth/login")
        {
            Content = JsonContent.Create(new { email, password }),
        };

        var result = await api.SendAsync<AuthResultDto>(request)
            ?? throw new ApiException("server_error", "Došlo je do neočekivane greške.");

        if (!string.Equals(result.User.Role, "driver", StringComparison.OrdinalIgnoreCase))
            throw new ApiException("not_a_driver", "Ovaj nalog nije vozački.");

        await SecureStorage.SetAsync(TokenKey, result.Token);
        await SecureStorage.SetAsync(NameKey, result.User.Name);
        DriverName = result.User.Name;
    }

    public Task<string?> GetTokenAsync() => SecureStorage.GetAsync(TokenKey);

    public async Task<bool> RestoreAsync()
    {
        var token = await SecureStorage.GetAsync(TokenKey);
        DriverName = await SecureStorage.GetAsync(NameKey);
        return !string.IsNullOrEmpty(token);
    }

    public Task LogoutAsync()
    {
        SecureStorage.Remove(TokenKey);
        SecureStorage.Remove(NameKey);
        DriverName = null;
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Napiši AuthHeaderHandler**

`EPiljara.Driver/Services/AuthHeaderHandler.cs`:

```csharp
using System.Net.Http.Headers;

namespace EPiljara.Driver.Services;

/// <summary>Kači Bearer token na svaki zahtjev i na 401/403 izbacuje vozača na prijavu.
/// AuthService se vadi lijeno iz kontejnera da se izbjegne kružna zavisnost
/// AuthService → ApiClient → HttpClient → handler → AuthService.</summary>
public class AuthHeaderHandler(IServiceProvider services) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var auth = services.GetRequiredService<AuthService>();
        var token = await auth.GetTokenAsync();
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await base.SendAsync(request, cancellationToken);

        // Istekao token (401) ili nalog koji nije vozački (403) — nema oporavka,
        // brišemo sesiju i vraćamo na prijavu. Poruku i dalje prikazuje ViewModel.
        if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized
                                or System.Net.HttpStatusCode.Forbidden)
        {
            await auth.LogoutAsync();
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (Shell.Current is not null)
                    await Shell.Current.GoToAsync("//login");
            });
        }

        return response;
    }
}
```

- [ ] **Step 3: Napiši LoginViewModel**

`EPiljara.Driver/ViewModels/LoginViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EPiljara.Driver.Services;

namespace EPiljara.Driver.ViewModels;

public partial class LoginViewModel(AuthService auth) : ObservableObject
{
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private bool _isBusy;

    /// <summary>Ako token iz prošle sesije još stoji, preskoči prijavu.
    /// Zove se iz LoginPage.OnAppearing.</summary>
    public async Task PokusajAutomatskuPrijavuAsync()
    {
        if (await auth.RestoreAsync())
            await Shell.Current.GoToAsync("//route");
    }

    [RelayCommand]
    private async Task PrijaviSeAsync()
    {
        if (IsBusy) return;
        Error = null;

        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            Error = "Unesite email i lozinku.";
            return;
        }

        IsBusy = true;
        try
        {
            await auth.LoginAsync(Email.Trim(), Password);
            await Shell.Current.GoToAsync("//route");
        }
        catch (ApiException e)
        {
            Error = e.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
```

- [ ] **Step 4: Napiši LoginPage**

`EPiljara.Driver/Views/LoginPage.xaml`:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:vm="clr-namespace:EPiljara.Driver.ViewModels"
             x:DataType="vm:LoginViewModel"
             x:Class="EPiljara.Driver.Views.LoginPage"
             Title="Prijava">
    <VerticalStackLayout Padding="24" Spacing="16" VerticalOptions="Center">

        <Label Text="E piljara" FontSize="32" FontAttributes="Bold" HorizontalOptions="Center" />
        <Label Text="Prijava vozača" FontSize="16" HorizontalOptions="Center" />

        <Entry Placeholder="Email"
               Keyboard="Email"
               Text="{Binding Email}" />

        <Entry Placeholder="Lozinka"
               IsPassword="True"
               Text="{Binding Password}" />

        <Label Text="{Binding Error}"
               TextColor="Red"
               IsVisible="{Binding Error, Converter={StaticResource IsStringNotEmpty}}" />

        <Button Text="Prijavi se"
                Command="{Binding PrijaviSeCommand}"
                IsEnabled="{Binding IsBusy, Converter={StaticResource InvertedBool}}" />

        <ActivityIndicator IsRunning="{Binding IsBusy}" IsVisible="{Binding IsBusy}" />

    </VerticalStackLayout>
</ContentPage>
```

Konverteri `IsStringNotEmpty` i `InvertedBool` ne postoje u MAUI-ju — napravi ih u `EPiljara.Driver/Converters.cs`:

```csharp
using System.Globalization;

namespace EPiljara.Driver;

public class IsStringNotEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        !string.IsNullOrWhiteSpace(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class InvertedBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not true;
}
```

Registruj ih u `EPiljara.Driver/App.xaml`, unutar `<Application.Resources><ResourceDictionary>`:

```xml
<local:IsStringNotEmptyConverter x:Key="IsStringNotEmpty" />
<local:InvertedBoolConverter x:Key="InvertedBool" />
```

i dodaj `xmlns:local="clr-namespace:EPiljara.Driver"` na `<Application>` element.

`EPiljara.Driver/Views/LoginPage.xaml.cs`:

```csharp
using EPiljara.Driver.ViewModels;

namespace EPiljara.Driver.Views;

public partial class LoginPage : ContentPage
{
    private readonly LoginViewModel _vm;

    public LoginPage(LoginViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.PokusajAutomatskuPrijavuAsync();
    }
}
```

- [ ] **Step 5: Postavi Shell navigaciju**

Obriši `MainPage.xaml` i `MainPage.xaml.cs`.

`EPiljara.Driver/AppShell.xaml`:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<Shell xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
       xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
       xmlns:views="clr-namespace:EPiljara.Driver.Views"
       x:Class="EPiljara.Driver.AppShell"
       FlyoutBehavior="Disabled">

    <ShellContent Route="login" ContentTemplate="{DataTemplate views:LoginPage}" />
    <ShellContent Route="route" ContentTemplate="{DataTemplate views:RoutePage}" />

</Shell>
```

`RoutePage` nastaje u Tasku 6 — do tada zakomentariši njegov `ShellContent` red, inače build puca.

`EPiljara.Driver/AppShell.xaml.cs`:

```csharp
namespace EPiljara.Driver;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("stop", typeof(Views.StopPage));
    }
}
```

`StopPage` nastaje u Tasku 7 — do tada zakomentariši `Routing.RegisterRoute`.

- [ ] **Step 6: Registruj sve u DI**

U `EPiljara.Driver/MauiProgram.cs`, zamijeni blok registracije `HttpClient`/`ApiClient` sa:

```csharp
		builder.Services.AddSingleton<AuthService>();
		builder.Services.AddTransient<AuthHeaderHandler>();

		builder.Services.AddSingleton(sp =>
		{
			var handler = new AuthHeaderHandler(sp) { InnerHandler = new HttpClientHandler() };
			return new HttpClient(handler) { BaseAddress = new Uri(baseUrl) };
		});
		builder.Services.AddSingleton<ApiClient>();

		builder.Services.AddTransient<ViewModels.LoginViewModel>();
		builder.Services.AddTransient<Views.LoginPage>();
```

Dodaj `using EPiljara.Driver.Services;` na vrh ako već nije.

- [ ] **Step 7: Build i pokreni na emulatoru**

Pokreni Android emulator iz Visual Studia ili Android Studia, pa:

```bash
cd "C:/Users/soviljn/Desktop/EPiljaraVozac/EPiljara.Driver"
dotnet build -f net10.0-android -c Debug -t:Run
```

Uz pokrenut backend (`dotnet run --project src/EPiljara.API` u `backend/`), prijavi se sa `vozac@epiljara.ba` / `vozac123`.

Expected: prijava prolazi. Sa pogrešnom lozinkom se pojavljuje `"Pogrešan email ili lozinka."` — poruka dolazi sa backenda kroz envelope. Sa `admin@epiljara.ba` / `admin123` se pojavljuje `"Ovaj nalog nije vozački."`

Ugasi i ponovo pokreni aplikaciju: vozač **ne** vidi ekran za prijavu jer `PokusajAutomatskuPrijavuAsync` nađe token u `SecureStorage`. (Ruta `//route` još ne postoji do Taska 6, pa će navigacija dotle pući — provjeri ovaj korak tek na kraju Taska 6.)

- [ ] **Step 8: Commit**

```bash
cd "C:/Users/soviljn/Desktop/EPiljaraVozac"
git add -A
git commit -m "feat(mobile): driver login with token in SecureStorage"
```

---

### Task 6: Ekran „Moja ruta"

**Files:**
- Create: `EPiljara.Driver/Services/DriverService.cs`
- Create: `EPiljara.Driver/ViewModels/RouteViewModel.cs`
- Create: `EPiljara.Driver/Views/RoutePage.xaml`, `Views/RoutePage.xaml.cs`
- Modify: `EPiljara.Driver/MauiProgram.cs`, `AppShell.xaml`
- Modify: `EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj` (odkomentariši link na `RouteViewModel.cs`)
- Test: `EPiljara.Driver.Tests/RouteViewModelTests.cs`

**Interfaces:**
- Consumes: `ApiClient`, `DeliveryRouteDto`, `RouteStopDto`, `ApiException` (Task 2); `AuthService` (Task 5)
- Produces:
  - `DriverService.GetRouteAsync(string? date = null) → Task<DeliveryRouteDto?>`
  - `DriverService.UpdateStopStatusAsync(Guid orderId, string status) → Task`
  - `RouteViewModel.Aktivne` → `ObservableCollection<RouteStopDto>`
  - `RouteViewModel.Zavrsene` → `ObservableCollection<RouteStopDto>`
  - `RouteViewModel.Napredak` → `string` (npr. `"3 od 7 isporučeno"`)
  - `RouteViewModel.NemaRute` → `bool` (ruta ne postoji ili nema nijednu stanicu)
  - `RouteViewModel.TuraGotova` → `bool` (stanica ima, ali nijedna nije aktivna)
  - `RouteViewModel.Primijeni(DeliveryRouteDto?)` — čista metoda, testabilna bez mreže

- [ ] **Step 1: Napiši test za podjelu stanica**

`EPiljara.Driver.Tests/RouteViewModelTests.cs`:

```csharp
using EPiljara.Driver.Models;
using EPiljara.Driver.ViewModels;

namespace EPiljara.Driver.Tests;

public class RouteViewModelTests
{
    private static RouteStopDto Stop(int sequence, string status) => new(
        OrderId: Guid.NewGuid(),
        OrderCode: $"EP-2608-{sequence:D4}",
        CustomerName: $"Kupac {sequence}",
        Address: $"Ulica {sequence}",
        ZoneName: "Centar",
        Sequence: sequence,
        CustomerPhone: "+387 65 000 000",
        Status: status);

    private static DeliveryRouteDto Route(params RouteStopDto[] stops) => new(
        Id: Guid.NewGuid(),
        Date: "2026-08-17",
        DriverId: Guid.NewGuid(),
        DriverName: "Mladen (kombi 1)",
        Status: "Optimized",
        Stops: stops);

    [Fact]
    public void Dijeli_stanice_na_aktivne_i_zavrsene()
    {
        var vm = new RouteViewModel(null!, null!);

        vm.Primijeni(Route(
            Stop(1, "Delivered"),
            Stop(2, "OutForDelivery"),
            Stop(3, "FailedDelivery"),
            Stop(4, "Confirmed")));

        Assert.Equal([2, 4], vm.Aktivne.Select(s => s.Sequence));
        Assert.Equal([1, 3], vm.Zavrsene.Select(s => s.Sequence));
    }

    [Fact]
    public void Sortira_aktivne_po_sequence_u()
    {
        var vm = new RouteViewModel(null!, null!);

        vm.Primijeni(Route(Stop(3, "Confirmed"), Stop(1, "Confirmed"), Stop(2, "Confirmed")));

        Assert.Equal([1, 2, 3], vm.Aktivne.Select(s => s.Sequence));
    }

    [Fact]
    public void Napredak_broji_zavrsene_od_ukupno()
    {
        var vm = new RouteViewModel(null!, null!);

        vm.Primijeni(Route(Stop(1, "Delivered"), Stop(2, "Delivered"), Stop(3, "Confirmed")));

        Assert.Equal("2 od 3 isporučeno", vm.Napredak);
    }

    [Fact]
    public void Bez_rute_liste_su_prazne_a_poruka_jasna()
    {
        var vm = new RouteViewModel(null!, null!);

        vm.Primijeni(null);

        Assert.Empty(vm.Aktivne);
        Assert.Empty(vm.Zavrsene);
        Assert.True(vm.NemaRute);
        Assert.False(vm.TuraGotova);
    }

    [Fact]
    public void Kad_je_sve_isporuceno_tura_je_gotova()
    {
        var vm = new RouteViewModel(null!, null!);

        vm.Primijeni(Route(Stop(1, "Delivered"), Stop(2, "FailedDelivery")));

        Assert.Empty(vm.Aktivne);
        Assert.True(vm.TuraGotova);
        Assert.False(vm.NemaRute);
    }
}
```

- [ ] **Step 2: Odkomentariši link i pokreni testove da padnu**

U `EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj` odkomentariši red za `RouteViewModel.cs`, pa:

```bash
cd "C:/Users/soviljn/Desktop/EPiljaraVozac"
dotnet test EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj
```

Expected: FAIL — `RouteViewModel` ne postoji.

- [ ] **Step 3: Napiši DriverService**

`EPiljara.Driver/Services/DriverService.cs`:

```csharp
using System.Net.Http.Json;
using EPiljara.Driver.Models;

namespace EPiljara.Driver.Services;

public class DriverService(ApiClient api)
{
    /// <summary>Ruta vozača za dati dan; bez datuma — današnji. null kad ruta ne postoji.</summary>
    public Task<DeliveryRouteDto?> GetRouteAsync(string? date = null)
    {
        var url = date is null ? "driver/route" : $"driver/route?date={date}";
        return api.SendAsync<DeliveryRouteDto>(new HttpRequestMessage(HttpMethod.Get, url));
    }

    /// <summary>status je "Delivered" ili "FailedDelivery" — backend odbija sve ostalo.</summary>
    public async Task UpdateStopStatusAsync(Guid orderId, string status)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, $"driver/orders/{orderId}/status")
        {
            Content = JsonContent.Create(new { status }),
        };
        await api.SendAsync<object>(request);
    }
}
```

- [ ] **Step 4: Napiši RouteViewModel**

`EPiljara.Driver/ViewModels/RouteViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EPiljara.Driver.Models;
using EPiljara.Driver.Services;

namespace EPiljara.Driver.ViewModels;

public partial class RouteViewModel(DriverService driver, AuthService auth) : ObservableObject
{
    /// <summary>Statusi poslije kojih vozač nema šta da radi sa stanicom.</summary>
    private static readonly HashSet<string> Zatvoreni =
        new(["Delivered", "FailedDelivery", "Cancelled"], StringComparer.OrdinalIgnoreCase);

    public ObservableCollection<RouteStopDto> Aktivne { get; } = [];
    public ObservableCollection<RouteStopDto> Zavrsene { get; } = [];

    [ObservableProperty] private string _napredak = string.Empty;
    [ObservableProperty] private bool _nemaRute;
    [ObservableProperty] private bool _turaGotova;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _error;

    public string VozacIme => auth?.DriverName ?? string.Empty;

    /// <summary>Čista transformacija odgovora u stanje ekrana — testira se bez mreže.</summary>
    public void Primijeni(DeliveryRouteDto? route)
    {
        Aktivne.Clear();
        Zavrsene.Clear();

        var stops = route?.Stops ?? [];
        NemaRute = route is null || stops.Count == 0;

        foreach (var stop in stops.OrderBy(s => s.Sequence))
        {
            if (Zatvoreni.Contains(stop.Status)) Zavrsene.Add(stop);
            else Aktivne.Add(stop);
        }

        Napredak = stops.Count == 0
            ? string.Empty
            : $"{Zavrsene.Count} od {stops.Count} isporučeno";

        TuraGotova = stops.Count > 0 && Aktivne.Count == 0;
    }

    [RelayCommand]
    public async Task UcitajAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        Error = null;
        try
        {
            Primijeni(await driver.GetRouteAsync());
        }
        catch (ApiException e)
        {
            Error = e.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OtvoriStanicuAsync(RouteStopDto stop) =>
        await Shell.Current.GoToAsync("stop", new Dictionary<string, object> { ["stop"] = stop });
}
```

- [ ] **Step 5: Pokreni testove i potvrdi da prolaze**

```bash
cd "C:/Users/soviljn/Desktop/EPiljaraVozac"
dotnet test EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj
```

Expected: `Passed! - Failed: 0, Passed: 11`

- [ ] **Step 6: Napiši RoutePage**

`EPiljara.Driver/Views/RoutePage.xaml`:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:vm="clr-namespace:EPiljara.Driver.ViewModels"
             xmlns:models="clr-namespace:EPiljara.Driver.Models"
             x:DataType="vm:RouteViewModel"
             x:Class="EPiljara.Driver.Views.RoutePage"
             Title="Moja ruta">

    <RefreshView IsRefreshing="{Binding IsBusy}" Command="{Binding UcitajCommand}">
        <ScrollView>
            <VerticalStackLayout Padding="16" Spacing="12">

                <Label Text="{Binding Napredak}" FontSize="18" FontAttributes="Bold" />

                <Label Text="{Binding Error}" TextColor="Red"
                       IsVisible="{Binding Error, Converter={StaticResource IsStringNotEmpty}}" />

                <Label Text="Nemate rutu za danas."
                       IsVisible="{Binding NemaRute}"
                       HorizontalOptions="Center" Margin="0,40" />

                <Label Text="Tura je završena. Svaka čast!"
                       IsVisible="{Binding TuraGotova}"
                       FontAttributes="Bold"
                       HorizontalOptions="Center" Margin="0,20" />

                <CollectionView ItemsSource="{Binding Aktivne}">
                    <CollectionView.ItemTemplate>
                        <DataTemplate x:DataType="models:RouteStopDto">
                            <Border Stroke="#DDD" StrokeThickness="1" Padding="12" Margin="0,4">
                                <Border.GestureRecognizers>
                                    <TapGestureRecognizer
                                        Command="{Binding Source={RelativeSource AncestorType={x:Type vm:RouteViewModel}}, Path=OtvoriStanicuCommand}"
                                        CommandParameter="{Binding .}" />
                                </Border.GestureRecognizers>
                                <VerticalStackLayout Spacing="2">
                                    <Label FontAttributes="Bold">
                                        <Label.Text>
                                            <MultiBinding StringFormat="{}{0}. {1}">
                                                <Binding Path="Sequence" />
                                                <Binding Path="CustomerName" />
                                            </MultiBinding>
                                        </Label.Text>
                                    </Label>
                                    <Label Text="{Binding Address}" FontSize="13" />
                                    <Label Text="{Binding ZoneName}" FontSize="12" TextColor="Gray" />
                                </VerticalStackLayout>
                            </Border>
                        </DataTemplate>
                    </CollectionView.ItemTemplate>
                </CollectionView>

                <Label Text="Završeno" FontAttributes="Bold" Margin="0,16,0,0"
                       IsVisible="{Binding Zavrsene.Count, Converter={StaticResource IsPositive}}" />

                <CollectionView ItemsSource="{Binding Zavrsene}">
                    <CollectionView.ItemTemplate>
                        <DataTemplate x:DataType="models:RouteStopDto">
                            <Grid Padding="12,6" Opacity="0.5">
                                <Label FontSize="14">
                                    <Label.Text>
                                        <MultiBinding StringFormat="{}{0}. {1} — {2}">
                                            <Binding Path="Sequence" />
                                            <Binding Path="CustomerName" />
                                            <Binding Path="Status" />
                                        </MultiBinding>
                                    </Label.Text>
                                </Label>
                            </Grid>
                        </DataTemplate>
                    </CollectionView.ItemTemplate>
                </CollectionView>

            </VerticalStackLayout>
        </ScrollView>
    </RefreshView>
</ContentPage>
```

Dodaj još jedan konverter u `EPiljara.Driver/Converters.cs`:

```csharp
public class IsPositiveConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int count && count > 0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
```

i registruj ga u `App.xaml`: `<local:IsPositiveConverter x:Key="IsPositive" />`

`EPiljara.Driver/Views/RoutePage.xaml.cs`:

```csharp
using EPiljara.Driver.ViewModels;

namespace EPiljara.Driver.Views;

public partial class RoutePage : ContentPage
{
    private readonly RouteViewModel _vm;

    public RoutePage(RouteViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.UcitajAsync();
    }
}
```

- [ ] **Step 7: Registruj u DI i Shell**

U `MauiProgram.cs` dodaj:

```csharp
		builder.Services.AddSingleton<DriverService>();
		builder.Services.AddTransient<ViewModels.RouteViewModel>();
		builder.Services.AddTransient<Views.RoutePage>();
```

U `AppShell.xaml` odkomentariši `<ShellContent Route="route" ... />`.

- [ ] **Step 8: Build, pokreni, commit**

```bash
cd "C:/Users/soviljn/Desktop/EPiljaraVozac/EPiljara.Driver"
dotnet build -f net10.0-android -c Debug -t:Run
```

Expected: poslije prijave se vidi lista stanica (ili `"Nemate rutu za danas."` ako ruta nije dodijeljena — vidi Task 8).

```bash
cd "C:/Users/soviljn/Desktop/EPiljaraVozac"
git add -A
git commit -m "feat(mobile): route screen with active/done stop split"
```

---

### Task 7: Ekran stanice i potvrda primopredaje

**Files:**
- Create: `EPiljara.Driver/ViewModels/StopViewModel.cs`
- Create: `EPiljara.Driver/Views/StopPage.xaml`, `Views/StopPage.xaml.cs`
- Modify: `EPiljara.Driver/MauiProgram.cs`, `AppShell.xaml.cs`

**Interfaces:**
- Consumes: `DriverService.UpdateStopStatusAsync` (Task 6), `RouteStopDto` (Task 2), `ApiException` (Task 2)
- Produces: Shell ruta `"stop"` koja prima parametar `"stop"` tipa `RouteStopDto`

- [ ] **Step 1: Napiši StopViewModel**

`EPiljara.Driver/ViewModels/StopViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EPiljara.Driver.Models;
using EPiljara.Driver.Services;

namespace EPiljara.Driver.ViewModels;

[QueryProperty(nameof(Stop), "stop")]
public partial class StopViewModel(DriverService driver) : ObservableObject
{
    [ObservableProperty] private RouteStopDto? _stop;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _error;

    [RelayCommand]
    private async Task PozoviAsync()
    {
        if (string.IsNullOrWhiteSpace(Stop?.CustomerPhone))
        {
            Error = "Kupac nema upisan telefon.";
            return;
        }

        try
        {
            PhoneDialer.Open(Stop.CustomerPhone);
        }
        catch (FeatureNotSupportedException)
        {
            Error = "Pozivanje nije podržano na ovom uređaju.";
        }
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task NavigacijaAsync()
    {
        if (Stop is null) return;

        var destination = Uri.EscapeDataString($"{Stop.Address}, Banja Luka");
        var url = $"https://www.google.com/maps/dir/?api=1&travelmode=driving&destination={destination}";
        await Launcher.OpenAsync(url);
    }

    [RelayCommand]
    private Task PrimopredajaAsync() => ZavrsiAsync(
        "Delivered",
        "Primopredaja izvršena",
        "Potvrdi da je roba predata kupcu?");

    [RelayCommand]
    private Task NeuspjelaAsync() => ZavrsiAsync(
        "FailedDelivery",
        "Neuspjela dostava",
        "Označiti ovu dostavu kao neuspjelu?");

    private async Task ZavrsiAsync(string status, string naslov, string pitanje)
    {
        if (IsBusy || Stop is null) return;

        var potvrda = await Shell.Current.DisplayAlert(naslov, pitanje, "Da", "Ne");
        if (!potvrda) return;

        IsBusy = true;
        Error = null;
        try
        {
            await driver.UpdateStopStatusAsync(Stop.OrderId, status);
            await Shell.Current.GoToAsync("..");
        }
        catch (ApiException e)
        {
            Error = e.Code == "invalid_transition"
                ? "Narudžba je već zatvorena."
                : e.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
```

Povratak na `".."` vraća na `RoutePage`, čiji `OnAppearing` ponovo učita rutu — pa stanica nestaje iz aktivnih bez ikakve dodatne sinhronizacije stanja.

- [ ] **Step 2: Napiši StopPage**

`EPiljara.Driver/Views/StopPage.xaml`:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:vm="clr-namespace:EPiljara.Driver.ViewModels"
             x:DataType="vm:StopViewModel"
             x:Class="EPiljara.Driver.Views.StopPage"
             Title="Stanica">

    <VerticalStackLayout Padding="20" Spacing="14">

        <Label Text="{Binding Stop.CustomerName}" FontSize="24" FontAttributes="Bold" />
        <Label Text="{Binding Stop.OrderCode}" FontSize="14" TextColor="Gray" />
        <Label Text="{Binding Stop.Address}" FontSize="17" />
        <Label Text="{Binding Stop.ZoneName}" FontSize="14" TextColor="Gray" />
        <Label Text="{Binding Stop.CustomerPhone}" FontSize="16" />

        <Label Text="{Binding Error}" TextColor="Red"
               IsVisible="{Binding Error, Converter={StaticResource IsStringNotEmpty}}" />

        <Grid ColumnDefinitions="*,*" ColumnSpacing="10" Margin="0,10">
            <Button Grid.Column="0" Text="Pozovi" Command="{Binding PozoviCommand}" />
            <Button Grid.Column="1" Text="Navigacija" Command="{Binding NavigacijaCommand}" />
        </Grid>

        <Button Text="Primopredaja izvršena"
                BackgroundColor="Green"
                Command="{Binding PrimopredajaCommand}"
                IsEnabled="{Binding IsBusy, Converter={StaticResource InvertedBool}}" />

        <Button Text="Neuspjela dostava"
                BackgroundColor="DarkRed"
                Command="{Binding NeuspjelaCommand}"
                IsEnabled="{Binding IsBusy, Converter={StaticResource InvertedBool}}" />

        <ActivityIndicator IsRunning="{Binding IsBusy}" IsVisible="{Binding IsBusy}" />

    </VerticalStackLayout>
</ContentPage>
```

`EPiljara.Driver/Views/StopPage.xaml.cs`:

```csharp
using EPiljara.Driver.ViewModels;

namespace EPiljara.Driver.Views;

public partial class StopPage : ContentPage
{
    public StopPage(StopViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
```

- [ ] **Step 3: Registruj u DI i Shell**

U `MauiProgram.cs`:

```csharp
		builder.Services.AddTransient<ViewModels.StopViewModel>();
		builder.Services.AddTransient<Views.StopPage>();
```

U `AppShell.xaml.cs` odkomentariši:

```csharp
        Routing.RegisterRoute("stop", typeof(Views.StopPage));
```

- [ ] **Step 4: Build i probaj cio tok na emulatoru**

```bash
cd "C:/Users/soviljn/Desktop/EPiljaraVozac/EPiljara.Driver"
dotnet build -f net10.0-android -c Debug -t:Run
```

Expected: dodir na stanicu otvara detalje; *Primopredaja izvršena* traži potvrdu, pa vraća na listu gdje je stanica prešla u „Završeno" i brojač je porastao.

- [ ] **Step 5: Commit**

```bash
cd "C:/Users/soviljn/Desktop/EPiljaraVozac"
git add -A
git commit -m "feat(mobile): stop detail screen with handover confirmation"
```

---

### Task 8: Ručna provjera cijelog toka i ispravka jezika u CLAUDE.md

**Ovaj task dira oba repoa.** Koraci 1–2 idu u **backend repo**
(`C:\Users\soviljn\Desktop\epiljara-backend-wt`, grana `driver-app-backend`),
koraci 3–6 u **mobilni repo** (`C:\Users\soviljn\Desktop\EPiljaraVozac`).

**Files:**
- Modify (backend repo): `CLAUDE.md` — oblast o jeziku i sekcija „What this is"
- Modify (mobilni repo): `README.md` — dopuna nakon što je aplikacija gotova

**Interfaces:**
- Consumes: sve iz taskova 1–7
- Produces: ništa novo u kodu

- [ ] **Step 1: Ispravi jezik u CLAUDE.md (backend repo)**

Zamijeni u `CLAUDE.md`:

```
UI text and API messages are in Bosnian (BHS); keep new user-facing strings in Bosnian.
```

sa:

```
UI text and API messages are in Serbian; keep new user-facing strings in Serbian.
```

Pretraži cio fajl za `Bosnian` da ne ostane nijedna pojava:

```bash
cd "C:/Users/soviljn/Desktop/epiljara-backend-wt"
grep -n "Bosnian" CLAUDE.md
```

Expected: bez izlaza.

- [ ] **Step 2: Dopuni CLAUDE.md pomenom mobilnog repoa (backend repo)**

U sekciju „What this is", ispod opisa `frontend/` i `backend/`, dodaj:

```
The Android driver app lives in a separate repository (`../EPiljaraVozac`, .NET MAUI,
net10.0-android). It consumes `/driver/*` over the same HTTP contract and shares no code.
```

Commituj backend dio prije nego pređeš na mobilni:

```bash
cd "C:/Users/soviljn/Desktop/epiljara-backend-wt"
git add CLAUDE.md
git commit -m "docs: correct UI language to Serbian, note the driver app repo"
```

- [ ] **Step 3: Dopuni README.md u mobilnom repou**

`README.md` već postoji iz inicijalnog commita i opisuje pokretanje, testove i
endpointe. **Nemoj ga prepisivati** — samo dodaj na kraj sekciju o statusu:

```markdown
## Status

Faza 1 je gotova: prijava, lista stanica za danas, potvrda primopredaje.

Van obima: mapa i pinovi lokacija, prava optimizacija rute, utovar i push
notifikacije, naplata, iOS verzija, rad bez interneta. Vidi Faze 2 i 3 u
spec dokumentu.
```

- [ ] **Step 4: Odradi cio scenario od nule**

Obriši bazu da kreneš od čistog seed-a:

```bash
cd "C:/Users/soviljn/Desktop/epiljara-backend-wt/backend"
docker compose up -d postgres
docker exec epiljara-postgres psql -U epiljara -d epiljara -c "DROP SCHEMA public CASCADE; CREATE SCHEMA public;"
dotnet run --project src/EPiljara.API
```

U drugom terminalu pokreni web admin:

```bash
cd "C:/Users/soviljn/Desktop/epiljara-backend-wt/frontend"
NEXT_PUBLIC_API_BASE_URL=http://localhost:5080 npm run dev
```

Napomena: `frontend/.env.local` pokazuje na port **5081**, a API sluša na **5080** — zato se varijabla postavlja u komandi.

Prođi kroz sve korake:

1. Admin (`admin@epiljara.ba` / `admin123`) na `http://localhost:3000/admin/routes` dodijeli 3 narudžbe vozaču **Mladen (kombi 1)**. Seed **ne sadrži nijednu rutu**, pa je ovo obavezno.
2. Prijava u aplikaciji: `vozac@epiljara.ba` / `vozac123` → vidi se 3 stanice, `"0 od 3 isporučeno"`.
3. Otvori prvu stanicu → *Navigacija* otvara Google Maps sa tačnom adresom.
4. *Primopredaja izvršena* → potvrda → povratak na listu, stanica je pod „Završeno", brojač `"1 od 3 isporučeno"`.
5. **Ugasi i ponovo pokreni aplikaciju** → vozač je i dalje prijavljen, a završena stanica je i dalje završena. *(Ovo provjerava i `SecureStorage` i `Status` polje iz Taska 3.)*
6. U admin panelu provjeri da je ta narudžba sada `Isporučeno`.
7. Zatvori i preostale dvije stanice → pojavi se `"Tura je završena. Svaka čast!"` i `"3 od 3 isporučeno"`.
8. Ugasi backend (`Ctrl+C`) → povuci listu nadolje → pojavi se `"Nema veze sa serverom."`, aplikacija ne puca.
9. Vrati backend → povuci nadolje → lista se vrati.
10. Provjeri istekao token: u aplikaciji nema dugmeta za odjavu, pa ovo simuliraj tako što u `appsettings.json` privremeno pokvariš `apiBaseUrl` u nepostojeći port, uvjeriš se da javlja grešku o vezi, pa ga vratiš. *(Puna provjera isteklog JWT-a traži čekanje da token istekne — ostavi za Fazu 3, kada dolazi odjava.)*

Zapiši svaki korak koji ne prođe i popravi prije commita.

- [ ] **Step 5: Pokreni sve testove još jednom**

```bash
cd "C:/Users/soviljn/Desktop/EPiljaraVozac"
dotnet test EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj
cd "C:/Users/soviljn/Desktop/epiljara-backend-wt/backend"
dotnet build
```

Expected: `Passed! - Failed: 0, Passed: 11` i `Build succeeded`.

- [ ] **Step 6: Commit u mobilnom repou**

Backend dio je već commitovan u Step 2. Ostaje samo `README.md`:

```bash
cd "C:/Users/soviljn/Desktop/EPiljaraVozac"
git add README.md
git commit -m "docs: mark Phase 1 complete in README"
```

Na kraju potvrdi da su oba repoa čista:

```bash
cd "C:/Users/soviljn/Desktop/EPiljaraVozac" && git status --short
cd "C:/Users/soviljn/Desktop/epiljara-backend-wt" && git status --short
```

Expected: oba bez izlaza.

---

## Šta ovaj plan namjerno ne radi

Iz spec-a, van obima Faze 1: mapa i pinovi lokacija, prava optimizacija rute, utovar i push notifikacije, naplata i dnevni obračun, iOS verzija, rad bez interneta. Faze 2 i 3 dobijaju svoje spec-ove.
