# Koordinate na narudžbi (Faza 2, dio 2a) — plan implementacije

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Svaka narudžba nosi geografsku širinu i dužinu adrese dostave, a vozačka aplikacija ih dobija kroz `RouteStopDto`.

**Architecture:** `Order` dobija dva nullable `double` polja, po uzoru na `Producer` koji ih već ima. Nove narudžbe ih donose iz `address-picker`-a, koji ih danas izračuna pa baci. Postojeće narudžbe se geokodiraju jednokratno, u pozadini, uz poštovanje Nominatim-ovog ograničenja od jednog zahtjeva u sekundi. Admin ih može ručno ispraviti.

**Tech Stack:** ASP.NET Core 9 (Clean Architecture, MediatR, EF Core + PostgreSQL), Next.js 15 + Leaflet + Nominatim, .NET MAUI (samo `Dtos.cs`).

**Spec:** `docs/superpowers/specs/2026-08-18-mapa-i-dolazak-design.md`

## Global Constraints

- **Svi korisnički vidljivi tekstovi i API poruke su na srpskom.**
- **Koordinate su nullable (`double?`).** Stanica bez koordinata je dozvoljena i ne smije blokirati dodjelu rute — mobilna aplikacija je prikazuje u listi ispod mape.
- **Nominatim: najviše jedan zahtjev u sekundi, uz `User-Agent` koji identifikuje aplikaciju.** Kršenje vodi u blokadu IP adrese. Geokodiranje ne smije teći unutar HTTP zahtjeva.
- **Backend nema automatske testove, po dogovoru repoa.** Ne uvoditi test projekat u backend; provjera je build + pozivanje API-ja. Mobilni repo ima xUnit i tamo se testira.
- **Mobilni DTO-ovi se prepisuju ručno i drže 1:1 sa backendom.** Zabranjeno je referencirati backend projekat iz mobilnog repoa.
- **`Order.Latitude`/`Longitude` NE ulaze u `OrderDto`** (kupcu ne trebaju) — samo u `RouteStopDto`.
- **Commit poslije svakog taska.**

## Dva repozitorija

| Repo | Putanja | Grana | Taskovi |
|---|---|---|---|
| **Backend** | `C:\Users\soviljn\Desktop\epiljara-backend-wt` | `driver-app-backend` | 1, 2 (dio), 3, 4, 5, 6 |
| **Mobilni** | `C:\Users\soviljn\Desktop\EPiljaraVozac` | `main` | 2 (dio) |

Backend repo je git worktree; njegov glavni radni folder ima nekommitovane izmjene koje se **ne diraju**.

## Struktura fajlova

Backend (`backend/`):

```
src/EPiljara.Domain/Entities/Order.cs                       + Latitude, Longitude
src/EPiljara.Infrastructure/Persistence/Migrations/          + nova migracija
src/EPiljara.Application/DTOs/AdminDtos.cs                   RouteStopDto + koordinate
src/EPiljara.Application/Mapping/MappingExtensions.cs        mapiranje
src/EPiljara.Application/Features/Admin/AdminRoutes.cs       optimizator dobija prave koordinate
src/EPiljara.Application/Features/Orders/CreateOrder.cs      komanda prima koordinate
src/EPiljara.Application/Common/Interfaces/IGeocodingService.cs   novi interfejs
src/EPiljara.Infrastructure/Services/NominatimGeocodingService.cs novi, sa 1 req/s
src/EPiljara.Application/Features/Admin/AdminGeocoding.cs    novi: geocode-missing + ručna ispravka
src/EPiljara.API/Controllers/AdminController.cs              dvije nove rute
```

Frontend (`frontend/`):

```
src/components/shop/address-picker.tsx      emituje koordinate
src/components/shop/address-chooser.tsx     prosljeđuje ih dalje
src/app/(shop)/checkout/page.tsx            šalje ih u narudžbu
src/app/admin/orders/new/page.tsx           isto
src/lib/api/contracts.ts                    CreateOrderRequest
 src/types/domain.ts                          RouteStop
src/lib/api/endpoints.ts                     adminApi.geocodeMissing, adminApi.setOrderCoordinates
```

Mobilni repo:

```
EPiljara.Driver/Models/Dtos.cs                        RouteStopDto + koordinate
EPiljara.Driver.Tests/ApiClientTests.cs               test deserijalizacije
```

---

### Task 1: `Order` dobija koordinate

**Files:**
- Modify: `backend/src/EPiljara.Domain/Entities/Order.cs`
- Create: `backend/src/EPiljara.Infrastructure/Persistence/Migrations/` (generisano)

**Interfaces:**
- Consumes: ništa
- Produces: `Order.Latitude` (`double?`), `Order.Longitude` (`double?`)

- [ ] **Step 1: Dodaj polja na entitet**

U `backend/src/EPiljara.Domain/Entities/Order.cs`, odmah ispod `DeliveryAddress` i `Note`:

```csharp
    /// <summary>
    /// Geografska širina i dužina adrese dostave. null kad geokodiranje nije uspjelo —
    /// takva narudžba se i dalje može dodijeliti ruti, samo nema pin na mapi vozača.
    /// </summary>
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
```

Nikakva EF konfiguracija nije potrebna — `Producer` ima ista dva polja bez ijednog reda u `OnModelCreating`.

- [ ] **Step 2: Napravi migraciju**

```bash
cd "C:/Users/soviljn/Desktop/epiljara-backend-wt/backend"
dotnet tool restore
dotnet dotnet-ef migrations add OrderCoordinates -p src/EPiljara.Infrastructure -s src/EPiljara.Infrastructure -o Persistence/Migrations
```

- [ ] **Step 3: Provjeri da migracija dodaje samo dvije nullable kolone**

Otvori novonastali `*_OrderCoordinates.cs`. Očekivano u `Up`:

```csharp
migrationBuilder.AddColumn<double>(name: "Latitude", table: "Orders", nullable: true);
migrationBuilder.AddColumn<double>(name: "Longitude", table: "Orders", nullable: true);
```

Ako migracija sadrži bilo šta drugo (brisanje kolone, izmjenu tipa, novu tabelu), **stani i javi** — znači da je model bio nesinhronizovan i to nije stvar ovog taska.

- [ ] **Step 4: Primijeni na bazu**

```bash
cd "C:/Users/soviljn/Desktop/epiljara-backend-wt/backend"
docker compose up -d postgres
dotnet dotnet-ef database update -p src/EPiljara.Infrastructure -s src/EPiljara.Infrastructure
```

Expected: `Done.` bez greške.

- [ ] **Step 5: Potvrdi da kolone postoje**

```bash
docker exec epiljara-postgres psql -U epiljara -d epiljara -c "\d \"Orders\"" | grep -i "latitude\|longitude"
```

Expected: dva reda, oba `double precision` i oba nullable.

- [ ] **Step 6: Build i commit**

```bash
cd "C:/Users/soviljn/Desktop/epiljara-backend-wt/backend"
dotnet build
cd "C:/Users/soviljn/Desktop/epiljara-backend-wt"
git add backend/src
git commit -m "feat(api): add nullable coordinates to Order"
```

---

### Task 2: Koordinate izlaze kroz `RouteStopDto` i stižu do optimizatora i mobilne aplikacije

**Files:**
- Modify: `backend/src/EPiljara.Application/DTOs/AdminDtos.cs`
- Modify: `backend/src/EPiljara.Application/Mapping/MappingExtensions.cs`
- Modify: `backend/src/EPiljara.Application/Features/Admin/AdminRoutes.cs`
- Modify (drugi repo): `EPiljara.Driver/Models/Dtos.cs`
- Test (drugi repo): `EPiljara.Driver.Tests/ApiClientTests.cs`

**Interfaces:**
- Consumes: `Order.Latitude`, `Order.Longitude` (Task 1)
- Produces: `RouteStopDto.Latitude` (`double?`), `RouteStopDto.Longitude` (`double?`) — i na backendu i u mobilnom `Models/Dtos.cs`

**Ovaj task dira oba repoa.** Koraci 1–4 u backend repou, koraci 5–8 u mobilnom.

- [ ] **Step 1: Dodaj polja u DTO**

U `backend/src/EPiljara.Application/DTOs/AdminDtos.cs`, u `RouteStopDto`, poslije `Status`:

```csharp
    /// <summary>Koordinate adrese dostave; null kad geokodiranje nije uspjelo.</summary>
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
```

- [ ] **Step 2: Popuni ih u mapiranju**

U `backend/src/EPiljara.Application/Mapping/MappingExtensions.cs`, u `ToDto(this RouteStop s)`, poslije reda sa `Status`:

```csharp
        Latitude = s.Order?.Latitude,
        Longitude = s.Order?.Longitude,
```

- [ ] **Step 3: Optimizator dobija prave koordinate umjesto null**

U `backend/src/EPiljara.Application/Features/Admin/AdminRoutes.cs`, u `OptimizeRouteHandler.Handle`, zamijeni gradnju `RouteStopInput`:

```csharp
        var inputs = route.Stops.Select(s => new RouteStopInput(
            s.OrderId,
            s.Order?.DeliveryAddress ?? string.Empty,
            s.Order?.DeliveryZone?.Name ?? string.Empty,
            s.Order?.Latitude,
            s.Order?.Longitude)).ToList();
```

`RouteStopInput` već prima ta dva parametra — do sada su im se slale `null` vrijednosti. `ZoneRouteOptimizer` ih ignoriše i to je u redu; prava optimizacija nije u obimu.

- [ ] **Step 4: Build i commit backend dijela**

```bash
cd "C:/Users/soviljn/Desktop/epiljara-backend-wt/backend"
dotnet build
cd "C:/Users/soviljn/Desktop/epiljara-backend-wt"
git add backend/src
git commit -m "feat(api): expose order coordinates on RouteStopDto and pass them to the optimizer"
```

Expected: `Build succeeded`.

- [ ] **Step 5: Napiši test koji pada u mobilnom repou**

U `C:/Users/soviljn/Desktop/EPiljaraVozac/EPiljara.Driver.Tests/ApiClientTests.cs`, dodaj na kraj klase `ApiClientTests`:

```csharp
    [Fact]
    public async Task Cita_koordinate_stanice_iz_envelope_a()
    {
        const string body = """
        {"data":{"id":"22222222-2222-2222-2222-222222222222","date":"2026-08-18","driverId":"33333333-3333-3333-3333-333333333333","driverName":"Mladen (kombi 1)","status":"Optimized","stops":[{"orderId":"44444444-4444-4444-4444-444444444444","orderCode":"EP-2608-0001","customerName":"Ana Kovač","address":"Reljina 18","zoneName":"Borik","sequence":1,"customerPhone":null,"status":"Confirmed","latitude":44.7722,"longitude":17.1910}]},"meta":null}
        """;
        var client = Client(new StubHandler(HttpStatusCode.OK, body));

        var result = await client.SendAsync<DeliveryRouteDto>(Get());

        Assert.NotNull(result);
        var stop = Assert.Single(result!.Stops);
        Assert.Equal(44.7722, stop.Latitude);
        Assert.Equal(17.1910, stop.Longitude);
    }

    [Fact]
    public async Task Stanica_bez_koordinata_daje_null_umjesto_nule()
    {
        const string body = """
        {"data":{"id":"22222222-2222-2222-2222-222222222222","date":"2026-08-18","driverId":"33333333-3333-3333-3333-333333333333","driverName":"Mladen (kombi 1)","status":"Planned","stops":[{"orderId":"44444444-4444-4444-4444-444444444444","orderCode":"EP-2608-0002","customerName":"Haso Begić","address":"Nepoznata 1","zoneName":"Lauš","sequence":1,"customerPhone":null,"status":"Confirmed","latitude":null,"longitude":null}]},"meta":null}
        """;
        var client = Client(new StubHandler(HttpStatusCode.OK, body));

        var result = await client.SendAsync<DeliveryRouteDto>(Get());

        var stop = Assert.Single(result!.Stops);
        Assert.Null(stop.Latitude);
        Assert.Null(stop.Longitude);
    }
```

Ovaj drugi test je važniji nego što izgleda: da su polja `double` umjesto `double?`, odsutna koordinata bi se deserijalizovala u `0,0` — tačku u Atlantskom okeanu — i mapa bi tiho crtala pin tamo.

- [ ] **Step 6: Pokreni testove i vidi da padaju**

```bash
cd "C:/Users/soviljn/Desktop/EPiljaraVozac"
dotnet test EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj
```

Expected: FAIL — `RouteStopDto` nema `Latitude`.

- [ ] **Step 7: Dodaj polja u mobilni DTO**

U `C:/Users/soviljn/Desktop/EPiljaraVozac/EPiljara.Driver/Models/Dtos.cs`, u `RouteStopDto`, dodaj dva parametra **na kraj** liste (redoslijed pozicionih parametara ne utiče na deserijalizaciju po imenu, ali čuvamo isti redoslijed kao backend):

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
    double? Longitude);
```

Ovo lomi sve postojeće pozive konstruktora u testovima — `RouteViewModelTests` gradi `RouteStopDto` sa imenovanim argumentima, pa dodaj i tamo `Latitude: null, Longitude: null` u pomoćnu metodu `Stop`.

- [ ] **Step 8: Pokreni testove, build, commit**

```bash
cd "C:/Users/soviljn/Desktop/EPiljaraVozac"
dotnet test EPiljara.Driver.Tests/EPiljara.Driver.Tests.csproj
cd "C:/Users/soviljn/Desktop/EPiljaraVozac/EPiljara.Driver"
dotnet build -f net10.0-android -c Debug
cd "C:/Users/soviljn/Desktop/EPiljaraVozac"
git add -A
git commit -m "feat: mirror order coordinates on RouteStopDto"
```

Expected: 25 testova prolazi (23 postojeća + 2 nova), build uspijeva. Build traje oko 2 minuta — koristi velikodušan timeout (600000 ms).

---

### Task 3: `CreateOrderCommand` prima i čuva koordinate

**Files:**
- Modify: `backend/src/EPiljara.Application/Features/Orders/CreateOrder.cs`

**Interfaces:**
- Consumes: `Order.Latitude`, `Order.Longitude` (Task 1)
- Produces: `CreateOrderCommand` sa dva nova parametra `double? Latitude, double? Longitude` na kraju

- [ ] **Step 1: Proširi komandu**

U `backend/src/EPiljara.Application/Features/Orders/CreateOrder.cs`:

```csharp
public record CreateOrderCommand(
    CustomerInput Customer,
    Guid DeliveryZoneId,
    Guid DeliverySlotId,
    string DeliveryAddress,
    string? Note,
    IReadOnlyList<OrderItemInput> Items,
    double? Latitude = null,
    double? Longitude = null) : IRequest<OrderDto>;
```

Podrazumijevane vrijednosti su bitne: bez njih bi svaki postojeći poziv (uključujući mock frontend) prestao da se prevodi, a stara ponašanja moraju da rade nepromijenjeno.

- [ ] **Step 2: Dodaj provjeru opsega u validator**

U istom fajlu, u `CreateOrderValidator`, poslije pravila za `DeliveryAddress`:

```csharp
        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90)
            .When(x => x.Latitude.HasValue)
            .WithMessage("Neispravna geografska širina.");
        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180)
            .When(x => x.Longitude.HasValue)
            .WithMessage("Neispravna geografska dužina.");
```

- [ ] **Step 3: Upiši ih na narudžbu**

U `CreateOrderHandler.Handle`, u inicijalizatoru `new Order { ... }`, poslije `DeliveryAddress = cmd.DeliveryAddress,`:

```csharp
            Latitude = cmd.Latitude,
            Longitude = cmd.Longitude,
```

- [ ] **Step 4: Build**

```bash
cd "C:/Users/soviljn/Desktop/epiljara-backend-wt/backend"
dotnet build
```

Expected: `Build succeeded`.

- [ ] **Step 5: Provjeri kroz stvarni poziv**

Pokreni API (`dotnet run --project src/EPiljara.API --urls http://0.0.0.0:5080`), pa u drugom terminalu napravi narudžbu sa koordinatama. Prvo uzmi važeći `deliveryZoneId`, `deliverySlotId` i `productId`:

```bash
API=http://localhost:5080
curl -s $API/delivery/zones | head -c 300; echo
curl -s $API/delivery/slots | head -c 300; echo
curl -s "$API/products" | head -c 300; echo
```

Zatim pošalji narudžbu, popunjavajući ID-jeve iz prethodnog koraka:

```bash
curl -s -X POST $API/orders -H "Content-Type: application/json" -d '{
  "customer": {"name":"Test Koordinate","phone":"+387 65 000 111","email":null},
  "deliveryZoneId":"<ZONE_ID>",
  "deliverySlotId":"<SLOT_ID>",
  "deliveryAddress":"Reljina 18, Banja Luka",
  "note":null,
  "items":[{"productId":"<PRODUCT_ID>","quantity":1}],
  "latitude":44.7722,
  "longitude":17.1910
}' | head -c 300
```

Expected: `201` sa narudžbom. Zatim potvrdi da su koordinate stvarno u bazi:

```bash
docker exec epiljara-postgres psql -U epiljara -d epiljara -c "SELECT \"Code\", \"Latitude\", \"Longitude\" FROM \"Orders\" WHERE \"Latitude\" IS NOT NULL ORDER BY \"CreatedAt\" DESC LIMIT 3;"
```

Expected: red sa `44.7722` i `17.191`.

Zatim potvrdi da narudžba **bez** koordinata i dalje prolazi — pošalji isti zahtjev bez `latitude`/`longitude` polja i očekuj `201`.

Ugasi API proces prije nastavka, da zaključan DLL ne obori sljedeći build.

- [ ] **Step 6: Commit**

```bash
cd "C:/Users/soviljn/Desktop/epiljara-backend-wt"
git add backend/src/EPiljara.Application/Features/Orders/CreateOrder.cs
git commit -m "feat(api): accept and persist delivery coordinates on order creation"
```

---

### Task 4: `AddressPicker` emituje koordinate, checkout i admin ih šalju

**Files:**
- Modify: `frontend/src/components/shop/address-picker.tsx`
- Modify: `frontend/src/components/shop/address-chooser.tsx`
- Modify: `frontend/src/app/(shop)/checkout/page.tsx`
- Modify: `frontend/src/app/admin/orders/new/page.tsx`
- Modify: `frontend/src/types/domain.ts`

**Interfaces:**
- Consumes: `CreateOrderCommand` sa `latitude`/`longitude` (Task 3)
- Produces: `AddressPicker` prop `onCoordinates?: (lat: number, lon: number) => void`; `CreateOrderRequest` sa `latitude?: number; longitude?: number`

- [ ] **Step 1: Dodaj prop i referencu u pickeru**

U `frontend/src/components/shop/address-picker.tsx`, u potpis komponente dodaj prop:

```tsx
export function AddressPicker({
  value,
  onChange,
  onLocalities,
  onCoordinates,
  id = "address",
}: {
  value: string;
  onChange: (value: string) => void;
  /** Emits candidate neighbourhood names for best-effort delivery-zone matching. */
  onLocalities?: (localities: string[]) => void;
  /** Emits the pin's coordinates so the order can be placed on the driver's map. */
  onCoordinates?: (lat: number, lon: number) => void;
  id?: string;
}) {
```

Odmah ispod postojećih `onChangeRef` / `onLocalitiesRef` dodaj i:

```tsx
  const onCoordinatesRef = useRef(onCoordinates);
  onCoordinatesRef.current = onCoordinates;
```

Referenca je obavezna iz istog razloga kao i za ostale — Leaflet handleri se registruju jednom i inače bi zadržali zastarjelu funkciju.

- [ ] **Step 2: Emituj koordinate kad se pin pomjeri**

U istom fajlu, u `commitFromMap`, poslije `onLocalitiesRef.current?.(res.localities);`:

```tsx
          onCoordinatesRef.current?.(lat, lon);
```

- [ ] **Step 3: Emituj koordinate i kad geokodiranje pogodi adresu**

Efekat koji radi debounced geokodiranje već pretvara koordinate u brojeve. U bloku `if (hit) { ... }`, poslije `onLocalitiesRef.current?.(localitiesOf(hit.address));`, dodaj jedan red:

```tsx
        onCoordinatesRef.current?.(lat, lon);
```

Promjenljive `lat` i `lon` već postoje nekoliko redova iznad (`const lat = Number(hit.lat);`) — ne uvodi nove.

- [ ] **Step 4: Provuci prop kroz `address-chooser`**

U `frontend/src/components/shop/address-chooser.tsx` dodaj `onCoordinates` u props tip i proslijedi ga:

```tsx
<AddressPicker value={value} onChange={onChange} onLocalities={onLocalities} onCoordinates={onCoordinates} />
```

- [ ] **Step 5: Proširi tip zahtjeva**

U `frontend/src/lib/api/contracts.ts`, u `CreateOrderRequest`, dodaj:

```ts
  latitude?: number;
  longitude?: number;
```

U `frontend/src/types/domain.ts`, u `RouteStop` interfejsu, dodaj i:

```ts
  status?: string;
  customerPhone?: string;
  latitude?: number;
  longitude?: number;
```

`status` i `customerPhone` su nedostajali i od ranije — backend ih šalje, TypeScript tip ih nije znao.

- [ ] **Step 6: Checkout pamti i šalje koordinate**

U `frontend/src/app/(shop)/checkout/page.tsx` dodaj stanje pored postojećeg stanja adrese:

```tsx
const [coords, setCoords] = useState<{ lat: number; lon: number } | null>(null);
```

Proslijedi `onCoordinates={(lat, lon) => setCoords({ lat, lon })}` komponenti koja renderuje picker, i u tijelo koje ide u `orderApi.create` dodaj:

```tsx
      latitude: coords?.lat,
      longitude: coords?.lon,
```

- [ ] **Step 7: Isto u admin formi za novu narudžbu**

U `frontend/src/app/admin/orders/new/page.tsx`, gdje stoji `<AddressPicker value={form.address} onChange={set("address")} />`, dodaj `onCoordinates` i isto tako proslijedi vrijednosti u tijelo zahtjeva.

- [ ] **Step 8: Provjeri tipove i lint**

```bash
cd "C:/Users/soviljn/Desktop/epiljara-backend-wt/frontend"
npx tsc --noEmit
npm run lint
```

Expected: bez grešaka. (Ne postoji `typecheck` skripta — koristi se `npx tsc --noEmit`.)

- [ ] **Step 9: Provjeri kroz browser**

Pokreni backend i frontend:

```bash
cd "C:/Users/soviljn/Desktop/epiljara-backend-wt/backend" && dotnet run --project src/EPiljara.API --urls http://0.0.0.0:5080
# u drugom terminalu:
cd "C:/Users/soviljn/Desktop/epiljara-backend-wt/frontend" && NEXT_PUBLIC_API_BASE_URL=http://localhost:5080 npm run dev
```

Napomena: `frontend/.env.local` pokazuje na port **5081**, a API sluša na **5080** — zato se varijabla postavlja u komandi, inače svaki zahtjev pada sa `network_error`.

Naruči nešto kroz `http://localhost:3000`, na mapi pomjeri pin, završi checkout. Zatim:

```bash
docker exec epiljara-postgres psql -U epiljara -d epiljara -c "SELECT \"Code\", \"DeliveryAddress\", \"Latitude\", \"Longitude\" FROM \"Orders\" ORDER BY \"CreatedAt\" DESC LIMIT 1;"
```

Expected: zadnja narudžba ima koordinate koje odgovaraju mjestu gdje si ostavio pin.

- [ ] **Step 10: Commit**

```bash
cd "C:/Users/soviljn/Desktop/epiljara-backend-wt"
git add frontend/src
git commit -m "feat(web): stop discarding picker coordinates; send them with the order"
```

---

### Task 5: Geokodiranje postojećih narudžbi

**Files:**
- Create: `backend/src/EPiljara.Application/Common/Interfaces/IGeocodingService.cs`
- Create: `backend/src/EPiljara.Infrastructure/Services/NominatimGeocodingService.cs`
- Create: `backend/src/EPiljara.Application/Features/Admin/AdminGeocoding.cs`
- Modify: `backend/src/EPiljara.Infrastructure/DependencyInjection.cs`
- Modify: `backend/src/EPiljara.API/Controllers/AdminController.cs`

**Interfaces:**
- Consumes: `Order.Latitude`, `Order.Longitude` (Task 1)
- Produces:
  - `IGeocodingService.GeocodeAsync(string address, CancellationToken) → Task<(double Lat, double Lon)?>`
  - `POST /admin/orders/geocode-missing` → `{ "pokrenuto": true, "brojNarudzbi": N }`

- [ ] **Step 1: Definiši interfejs**

`backend/src/EPiljara.Application/Common/Interfaces/IGeocodingService.cs`:

```csharp
namespace EPiljara.Application.Common.Interfaces;

/// <summary>
/// Pretvara tekstualnu adresu u koordinate. Implementacija mora poštovati
/// ograničenja servisa koji koristi (Nominatim: jedan zahtjev u sekundi).
/// </summary>
public interface IGeocodingService
{
    /// <summary>Vraća koordinate ili null kad adresa nije prepoznata.</summary>
    Task<(double Lat, double Lon)?> GeocodeAsync(string address, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Nominatim implementacija sa razmakom od jedne sekunde**

`backend/src/EPiljara.Infrastructure/Services/NominatimGeocodingService.cs`:

```csharp
using System.Text.Json;
using EPiljara.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace EPiljara.Infrastructure.Services;

/// <summary>
/// Geokodiranje preko OpenStreetMap Nominatim-a. Njihova pravila korišćenja traže
/// najviše JEDAN zahtjev u sekundi i User-Agent koji identifikuje aplikaciju;
/// kršenje vodi u blokadu IP adrese. Razmak se čuva semaforom, pa je servis
/// bezbjedan i kad ga zove više poslova odjednom.
/// </summary>
public class NominatimGeocodingService(HttpClient http, ILogger<NominatimGeocodingService> logger)
    : IGeocodingService
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static DateTimeOffset _zadnjiZahtjev = DateTimeOffset.MinValue;

    public async Task<(double Lat, double Lon)?> GeocodeAsync(
        string address, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(address)) return null;

        await Gate.WaitAsync(cancellationToken);
        try
        {
            var proteklo = DateTimeOffset.UtcNow - _zadnjiZahtjev;
            if (proteklo < TimeSpan.FromSeconds(1))
                await Task.Delay(TimeSpan.FromSeconds(1) - proteklo, cancellationToken);
            _zadnjiZahtjev = DateTimeOffset.UtcNow;

            var url = "https://nominatim.openstreetmap.org/search"
                + $"?format=jsonv2&limit=1&countrycodes=ba&q={Uri.EscapeDataString(address)}";

            using var response = await http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Nominatim vratio {Status} za adresu {Adresa}",
                    (int)response.StatusCode, address);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.GetArrayLength() == 0) return null;

            var hit = doc.RootElement[0];
            if (!double.TryParse(hit.GetProperty("lat").GetString(),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var lat)) return null;
            if (!double.TryParse(hit.GetProperty("lon").GetString(),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var lon)) return null;

            return (lat, lon);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(e, "Geokodiranje nije uspjelo za adresu {Adresa}", address);
            return null;
        }
        finally
        {
            Gate.Release();
        }
    }
}
```

`InvariantCulture` je obavezan: Nominatim vraća `44.7722`, a na srpskoj lokalizaciji `double.TryParse` bez njega tumači tačku kao razdjelnik hiljada i daje `447722`.

- [ ] **Step 3: Registruj servis**

U `backend/src/EPiljara.Infrastructure/DependencyInjection.cs`, uz ostale registracije servisa:

```csharp
        services.AddHttpClient<IGeocodingService, NominatimGeocodingService>(c =>
        {
            // Nominatim odbija zahtjeve bez User-Agenta koji identifikuje aplikaciju.
            c.DefaultRequestHeaders.UserAgent.ParseAdd("EPiljara/1.0 (dostava Banja Luka)");
            c.Timeout = TimeSpan.FromSeconds(15);
        });
```

- [ ] **Step 4: Komanda koja obrađuje narudžbe bez koordinata**

`backend/src/EPiljara.Application/Features/Admin/AdminGeocoding.cs`:

```csharp
using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Admin;

/* ---------- Jednokratno geokodiranje narudžbi bez koordinata ---------- */

public record GeocodeMissingCommand : IRequest<GeocodeMissingResult>;

public record GeocodeMissingResult(bool Pokrenuto, int BrojNarudzbi);

public class GeocodeMissingHandler(IAppDbContext db, IGeocodingService geocoder)
    : IRequestHandler<GeocodeMissingCommand, GeocodeMissingResult>
{
    public async Task<GeocodeMissingResult> Handle(GeocodeMissingCommand cmd, CancellationToken ct)
    {
        var narudzbe = await db.Orders
            .Where(o => o.Latitude == null && o.DeliveryAddress != "")
            .Select(o => new { o.Id, o.DeliveryAddress })
            .ToListAsync(ct);

        foreach (var n in narudzbe)
        {
            var tacka = await geocoder.GeocodeAsync($"{n.DeliveryAddress}, Banja Luka", ct);
            if (tacka is null) continue;

            var order = await db.Orders.FirstAsync(o => o.Id == n.Id, ct);
            order.Latitude = tacka.Value.Lat;
            order.Longitude = tacka.Value.Lon;
            await db.SaveChangesAsync(ct);
        }

        return new GeocodeMissingResult(true, narudzbe.Count);
    }
}

/* ---------- Ručna ispravka koordinata na jednoj narudžbi ---------- */

public record SetOrderCoordinatesCommand(Guid OrderId, double Latitude, double Longitude)
    : IRequest<Unit>;

public class SetOrderCoordinatesHandler(IAppDbContext db)
    : IRequestHandler<SetOrderCoordinatesCommand, Unit>
{
    public async Task<Unit> Handle(SetOrderCoordinatesCommand cmd, CancellationToken ct)
    {
        if (cmd.Latitude is < -90 or > 90)
            throw new DomainException("Neispravna geografska širina.", "invalid_coordinates");
        if (cmd.Longitude is < -180 or > 180)
            throw new DomainException("Neispravna geografska dužina.", "invalid_coordinates");

        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == cmd.OrderId, ct)
            ?? throw new NotFoundException("Narudžba nije pronađena.");

        order.Latitude = cmd.Latitude;
        order.Longitude = cmd.Longitude;
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
```

Preskakanje neuspjeha (`if (tacka is null) continue;`) je namjerno: narudžba ostaje bez koordinata i sljedeće pokretanje će je opet pokušati, ali obrada ne staje zbog jedne loše adrese.

- [ ] **Step 5: Izloži rute**

U `backend/src/EPiljara.API/Controllers/AdminController.cs`, uz ostale admin akcije:

```csharp
    [HttpPost("admin/orders/geocode-missing")]
    public async Task<IActionResult> GeocodeMissing() =>
        Envelope(await Mediator.Send(new GeocodeMissingCommand()));

    [HttpPut("admin/orders/{id:guid}/coordinates")]
    public async Task<IActionResult> SetCoordinates(Guid id, [FromBody] SetCoordinatesRequest body)
    {
        await Mediator.Send(new SetOrderCoordinatesCommand(id, body.Latitude, body.Longitude));
        return Envelope(new { message = "Koordinate su sačuvane." });
    }
```

U `backend/src/EPiljara.API/Controllers/Requests.cs` dodaj:

```csharp
public record SetCoordinatesRequest(double Latitude, double Longitude);
```

- [ ] **Step 6: Build**

```bash
cd "C:/Users/soviljn/Desktop/epiljara-backend-wt/backend"
dotnet build
```

Expected: `Build succeeded`.

- [ ] **Step 7: Provjeri geokodiranje uživo, uključujući i tempo**

Pokreni API, pa kao admin pozovi endpoint i **izmjeri koliko traje**:

```bash
API=http://localhost:5080
TOKEN=$(curl -s -X POST $API/auth/login -H "Content-Type: application/json" \
  -d '{"email":"admin@epiljara.ba","password":"admin123"}' | sed -n 's/.*"token":"\([^"]*\)".*/\1/p')

docker exec epiljara-postgres psql -U epiljara -d epiljara -c "SELECT COUNT(*) FROM \"Orders\" WHERE \"Latitude\" IS NULL;"

time curl -s -X POST $API/admin/orders/geocode-missing -H "Authorization: Bearer $TOKEN"
```

Expected: odgovor `{"data":{"pokrenuto":true,"brojNarudzbi":N}}`, a **proteklo vrijeme mora biti bar N sekundi** — to je dokaz da se razmak od jedne sekunde poštuje. Ako prođe za dvije sekunde uz N=20, ograničenje ne radi i Nominatim će blokirati IP.

Zatim provjeri rezultat:

```bash
docker exec epiljara-postgres psql -U epiljara -d epiljara -c "SELECT \"Code\", \"DeliveryAddress\", \"Latitude\", \"Longitude\" FROM \"Orders\" WHERE \"Latitude\" IS NOT NULL LIMIT 5;"
```

Expected: koordinate oko `44.7x` / `17.1x` (Banja Luka). Ako neka ispadne izvan tog opsega, adresa je loše pogođena — zabilježi koja.

Pokreni endpoint **drugi put** i potvrdi da `brojNarudzbi` padne (već geokodirane se preskaču).

- [ ] **Step 8: Provjeri ručnu ispravku**

```bash
ORDER=$(docker exec epiljara-postgres psql -U epiljara -d epiljara -t -c "SELECT \"Id\" FROM \"Orders\" LIMIT 1;" | tr -d ' \r\n')
curl -s -X PUT "$API/admin/orders/$ORDER/coordinates" -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" -d '{"latitude":44.7722,"longitude":17.1910}'
curl -s -X PUT "$API/admin/orders/$ORDER/coordinates" -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" -d '{"latitude":999,"longitude":17.1910}'
```

Expected: prvi poziv `200` sa porukom `"Koordinate su sačuvane."`, drugi `400` sa kodom `invalid_coordinates`.

Ugasi API proces prije nastavka.

- [ ] **Step 9: Commit**

```bash
cd "C:/Users/soviljn/Desktop/epiljara-backend-wt"
git add backend/src
git commit -m "feat(api): geocode orders missing coordinates, and allow manual correction"
```

---

### Task 6: Admin vidi i popravlja koordinate iz panela

**Files:**
- Modify: `frontend/src/lib/api/endpoints.ts`
- Modify: `frontend/src/app/admin/routes/page.tsx`

**Interfaces:**
- Consumes: `POST /admin/orders/geocode-missing`, `PUT /admin/orders/{id}/coordinates` (Task 5); `RouteStop.latitude/longitude` (Task 4, Step 5)
- Produces: ništa što kasniji taskovi koriste

- [ ] **Step 1: Dodaj pozive u API klijent**

U `frontend/src/lib/api/endpoints.ts`, u `adminApi` objekat:

```ts
  geocodeMissing: () =>
    apiFetch<{ pokrenuto: boolean; brojNarudzbi: number }>("/admin/orders/geocode-missing", {
      method: "POST",
    }).then((r) => r.data),
  setOrderCoordinates: (id: string, latitude: number, longitude: number) =>
    apiFetch<{ message: string }>(`/admin/orders/${id}/coordinates`, {
      method: "PUT",
      body: { latitude, longitude },
    }).then((r) => r.data),
```

- [ ] **Step 2: Označi stanice bez koordinata na stranici ruta**

U `frontend/src/app/admin/routes/page.tsx`, u `RouteCard`, unutar `<li>` koji renderuje stanicu, poslije reda sa adresom dodaj upozorenje:

```tsx
              {s.latitude == null && (
                <p className="text-xs font-medium text-honey-600">
                  Bez tačne lokacije — neće imati pin na mapi vozača
                </p>
              )}
```

- [ ] **Step 3: Dodaj dugme za geokodiranje**

U istom fajlu, u zaglavlju stranice pored polja za datum, dodaj mutaciju i dugme po postojećem obrascu (TanStack `useMutation` + `invalidateQueries` + toast):

```tsx
  const geocode = useMutation({
    mutationFn: () => adminApi.geocodeMissing(),
    onSuccess: (r) => {
      qc.invalidateQueries({ queryKey: ["admin", "routes", date] });
      show(`Geokodirano: ${r.brojNarudzbi} narudžbi obrađeno`);
    },
  });
```

i dugme:

```tsx
<Button variant="secondary" disabled={geocode.isPending} onClick={() => geocode.mutate()}>
  {geocode.isPending ? "Geokodiranje…" : "Geokodiraj adrese"}
</Button>
```

Dugme mora biti očigledno sporo — geokodiranje traje jednu sekundu po narudžbi, pa `isPending` stanje nije ukras nego jedina naznaka korisniku da posao traje.

- [ ] **Step 4: Provjeri tipove i lint**

```bash
cd "C:/Users/soviljn/Desktop/epiljara-backend-wt/frontend"
npx tsc --noEmit
npm run lint
```

Expected: bez grešaka.

- [ ] **Step 5: Provjeri u browseru**

Uz pokrenut backend i frontend, otvori `http://localhost:3000/admin/routes` kao admin. Dodijeli rutu vozaču, pa provjeri:

- stanice bez koordinata nose upozorenje
- dugme „Geokodiraj adrese" pokreće obradu i javlja koliko je narudžbi obrađeno
- poslije obrade i osvježavanja, upozorenje nestaje sa stanica koje su pogođene

- [ ] **Step 6: Commit**

```bash
cd "C:/Users/soviljn/Desktop/epiljara-backend-wt"
git add frontend/src
git commit -m "feat(web): flag stops without coordinates and add a geocode action"
```

---

### Task 7: Admin ručno postavlja lokaciju stanici koju geokodiranje nije pogodilo

**Files:**
- Create: `frontend/src/components/admin/stop-location-drawer.tsx`
- Modify: `frontend/src/app/admin/routes/page.tsx`

**Interfaces:**
- Consumes: `adminApi.setOrderCoordinates(id, latitude, longitude)` (Task 6, Step 1); `AddressPicker` sa `onCoordinates` (Task 4)
- Produces: ništa što kasniji taskovi koriste

Bez ovog taska `adminApi.setOrderCoordinates` iz Taska 6 nema nijednog pozivaoca, a stanica koju Nominatim ne pogodi ostaje bez pina zauvijek — nema načina da se to ispravi kroz aplikaciju.

- [ ] **Step 1: Napravi drawer sa mapom**

`frontend/src/components/admin/stop-location-drawer.tsx`:

```tsx
"use client";

import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { AddressPicker } from "@/components/shop/address-picker";
import { Button } from "@/components/ui/button";
import { useToast } from "@/components/ui/toast";
import { adminApi } from "@/lib/api/endpoints";

/**
 * Ručno postavljanje koordinata narudžbi koju geokodiranje nije pogodilo.
 * Adresa se ne mijenja — mijenja se samo tačka na mapi.
 */
export function StopLocationDrawer({
  orderId,
  orderCode,
  address,
  onClose,
}: {
  orderId: string;
  orderCode: string;
  address: string;
  onClose: () => void;
}) {
  const qc = useQueryClient();
  const { show } = useToast();
  const [coords, setCoords] = useState<{ lat: number; lon: number } | null>(null);
  const [tekst, setTekst] = useState(address);

  const save = useMutation({
    mutationFn: () => {
      if (!coords) throw new Error("Nema odabrane tačke.");
      return adminApi.setOrderCoordinates(orderId, coords.lat, coords.lon);
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin", "routes"] });
      show("Lokacija je sačuvana");
      onClose();
    },
    onError: () => show("Lokacija nije sačuvana"),
  });

  return (
    <div className="fixed inset-0 z-50 flex items-end justify-center bg-black/40 sm:items-center">
      <div className="w-full max-w-lg rounded-card bg-white p-5 shadow-lg">
        <h2 className="font-display text-lg font-semibold">Postavi lokaciju</h2>
        <p className="text-sm text-muted">
          {orderCode} · pomjeri pin na tačno mjesto dostave
        </p>

        <div className="mt-4">
          <AddressPicker
            value={tekst}
            onChange={setTekst}
            onCoordinates={(lat, lon) => setCoords({ lat, lon })}
          />
        </div>

        <div className="mt-4 flex justify-end gap-2">
          <Button variant="secondary" onClick={onClose}>
            Odustani
          </Button>
          <Button disabled={!coords || save.isPending} onClick={() => save.mutate()}>
            {save.isPending ? "Čuvanje…" : "Sačuvaj lokaciju"}
          </Button>
        </div>
      </div>
    </div>
  );
}
```

Dugme za čuvanje ostaje neaktivno dok vozač ne pomjeri pin — bez toga bi se moglo sačuvati podrazumijevano središte Banje Luke i to bi bilo gore od odsutne koordinate, jer bi izgledalo tačno.

- [ ] **Step 2: Otvori drawer iz upozorenja na stranici ruta**

U `frontend/src/app/admin/routes/page.tsx` uvezi komponentu, dodaj stanje u `RouteCard`:

```tsx
const [lokacijaZa, setLokacijaZa] = useState<{ id: string; code: string; address: string } | null>(null);
```

Zamijeni upozorenje iz Taska 6 dugmetom:

```tsx
              {s.latitude == null && (
                <button
                  type="button"
                  onClick={() => setLokacijaZa({ id: s.orderId, code: s.orderCode, address: s.address })}
                  className="text-xs font-medium text-honey-600 underline"
                >
                  Bez tačne lokacije — postavi
                </button>
              )}
```

i na kraj `RouteCard`-a, prije zatvaranja korijenskog `div`-a:

```tsx
      {lokacijaZa && (
        <StopLocationDrawer
          orderId={lokacijaZa.id}
          orderCode={lokacijaZa.code}
          address={lokacijaZa.address}
          onClose={() => setLokacijaZa(null)}
        />
      )}
```

- [ ] **Step 3: Provjeri tipove i lint**

```bash
cd "C:/Users/soviljn/Desktop/epiljara-backend-wt/frontend"
npx tsc --noEmit
npm run lint
```

Expected: bez grešaka.

- [ ] **Step 4: Provjeri u browseru**

Uz pokrenut backend i frontend, na `/admin/routes` nađi stanicu bez koordinata, klikni „Bez tačne lokacije — postavi", pomjeri pin, sačuvaj. Zatim:

```bash
docker exec epiljara-postgres psql -U epiljara -d epiljara -c "SELECT \"Code\", \"Latitude\", \"Longitude\" FROM \"Orders\" WHERE \"Id\" = '<ORDER_ID>';"
```

Expected: koordinate odgovaraju mjestu gdje je pin ostavljen, a upozorenje na stranici nestaje poslije osvježavanja.

- [ ] **Step 5: Commit**

```bash
cd "C:/Users/soviljn/Desktop/epiljara-backend-wt"
git add frontend/src
git commit -m "feat(web): let admin set a stop location by hand when geocoding misses"
```

---

## Provjera na kraju

Kad su svi taskovi gotovi, provjeri da vozačka aplikacija stvarno dobija koordinate:

```bash
API=http://localhost:5080
DRIVER=$(curl -s -X POST $API/auth/login -H "Content-Type: application/json" \
  -d '{"email":"vozac@epiljara.ba","password":"vozac123"}' | sed -n 's/.*"token":"\([^"]*\)".*/\1/p')
curl -s $API/driver/route -H "Authorization: Bearer $DRIVER"
```

Expected: svaka stanica u odgovoru ima `latitude` i `longitude` — bilo sa brojem, bilo sa `null`. Nijedna ne smije imati `0`.

## Šta ovaj plan namjerno ne radi

Prava optimizacija rute (koordinate se samo prosljeđuju postojećem `ZoneRouteOptimizer`-u koji ih ignoriše), mapa u mobilnoj aplikaciji, praćenje lokacije vozača, popup na dolasku. Sve to je dio 2b i dobija svoj plan.
