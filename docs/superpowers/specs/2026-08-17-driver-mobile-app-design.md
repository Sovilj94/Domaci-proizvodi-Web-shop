# Mobilna aplikacija za vozače — Faza 1

**Datum:** 2026-08-17
**Status:** odobren dizajn, spreman za plan implementacije
**Obim:** Faza 1 od tri (vidi "Faze koje slijede")

## Problem

Vozači E piljare nemaju alat. Admin može da dodijeli narudžbe vozaču i optimizuje
redoslijed (`/admin/routes`), ali vozač tu rutu nigdje ne vidi — na terenu radi po
papiru ili telefonom, a statusi narudžbi se u sistemu ne pomjeraju dok neko iz
kancelarije to ručno ne uradi.

Backend za vozača **već postoji i radi**: `DriverController` izlaže `GET /driver/route`
i `PATCH /driver/orders/{id}/status`, a `Driver` entitet je povezan sa `User`-om role
`Driver`. Nedostaje samo klijent.

## Cilj Faze 1

Vozač na Android telefonu odradi cijelu turu: prijavi se, vidi svoje stanice za danas,
za svaku pozove kupca ili otvori navigaciju, i poslije svake isporuke potvrdi
primopredaju — nakon čega stanica nestaje iz aktivne liste.

Uspjeh: vozač odradi turu od početka do kraja bez ijednog poziva kancelariji, a statusi
narudžbi u sistemu su tačni bez ručnog rada admina.

## Odluke i njihov razlog

| Odluka | Razlog |
|---|---|
| **.NET MAUI, samo Android** (`net9.0-android`) | Korisnik piše C#; cijeli backend je .NET 9. iOS bi tražio Mac koji korisnik nema — bez iOS TFM-a build ne traži Mac. |
| **Kombi = vozač** | Svaki vozač vozi svoj kombi. Zadržava postojeći model (`DeliveryRoute` = vozač + datum); nema entiteta `Vehicle`. Seeder to već odražava: `"Mladen (kombi 1)"`, `"Saša (kombi 2)"`. |
| **DTO-ovi se prepisuju, ne dijele** | `CLAUDE.md`: *"They talk over HTTP only — there is no shared code or build between them."* Frontend isto radi (`types/domain.ts`). Referenca na `EPiljara.Application` bi povukla EF Core i MediatR u mobilnu aplikaciju. |
| **Faza po faza, ne sve odjednom** | Faza 1 ne traži gotovo nikakav backend posao jer endpointi postoje. Rizik raste postepeno — Firebase i migracija koordinata tek kasnije. |
| **MVVM sa `CommunityToolkit.Mvvm`** | Source generatori uklanjaju ručni `INotifyPropertyChanged`. Standard za MAUI. |

## Ekrani

Tri ekrana, `Shell` navigacija.

### Prijava (`LoginPage`)
Email i lozinka → `POST /auth/login`. Token u `SecureStorage` (Android Keystore) da se
vozač ne prijavljuje svaki dan. Ako odgovor nosi `user.role != "driver"`, prijava se
odbija sa porukom da nalog nije vozački.

Demo nalog iz seed-a: `vozac@epiljara.ba` / `vozac123`.

### Moja ruta (`RoutePage`)
`GET /driver/route` (bez `date` parametra → današnji dan). Stanice poređane po
`Sequence`. Svaka kartica: redni broj, ime kupca, adresa, zona.

- Zaglavlje: napredak — `"3 od 7 isporučeno"`.
- Aktivne stanice gore, završene (`Delivered` / `FailedDelivery`) prigušene na dnu.
- Prazan odgovor (`data: null` — nema rute za danas) → poruka `"Nemate rutu za danas."`
- Sve isporučeno → poruka da je tura gotova.
- Povlačenje nadolje osvježava listu.

### Stanica (`StopPage`)
Puni podaci: šifra narudžbe, ime, telefon, adresa, zona.

- **Pozovi** → `tel:` intent.
- **Navigacija** → Google Maps na adresu (`https://www.google.com/maps/dir/?api=1&destination=<adresa>, Banja Luka&travelmode=driving`), isti obrazac koji admin panel već koristi u `buildNavUrl`.
- **Primopredaja izvršena** → `PATCH /driver/orders/{id}/status` sa `Delivered`.
- **Neuspjela dostava** → isto, sa `FailedDelivery`.

Oba ishoda traže potvrdu prije slanja (dijalog), pa se vraćaju na listu gdje je stanica
prešla u završene.

## Izmjene na backendu

Tri, sve male.

### 1. `RouteStopDto` dobija `Status`

Bez njega aplikacija poslije restarta ne zna koje su stanice završene — sve bi se
ponovo prikazale kao aktivne.

```csharp
// EPiljara.Application/DTOs/AdminDtos.cs
public record RouteStopDto
{
    // ... postojeća polja
    /// <summary>Order status, so the driver app knows which stops are done.</summary>
    public string Status { get; init; } = "Confirmed";
}
```

Plus jedan red u `MappingExtensions.ToDto(this RouteStop s)`:
`Status = s.Order?.Status.ToString() ?? "Confirmed"`.

Admin frontend ovo polje ignoriše — dodavanje polja u envelope ga ne lomi.

### 2. Vozačeva potvrda sama pregura narudžbu u `OutForDelivery`

**Problem:** mašina stanja dozvoljava `Delivered` samo iz `OutForDelivery`
(`Order.Transitions`), a admin panel dozvoljava dodjelu rute narudžbama u statusu
`Confirmed`, `Packing` ili `OutForDelivery` (`ASSIGNABLE` u `admin/routes/page.tsx`).
Narudžba dodijeljena dok je još `Confirmed` daje `400 invalid_transition` kad vozač
potvrdi primopredaju — aplikacija radi ispravno, tok ne radi.

**Rješenje:** u `UpdateStopStatusHandler`, prije prelaza na konačni status, prebaciti
narudžbu kroz međukorake ako je zatečena ranije:

```csharp
if (order.Status is OrderStatus.Confirmed) order.TransitionTo(OrderStatus.Packing);
if (order.Status is OrderStatus.Packing) order.TransitionTo(OrderStatus.OutForDelivery);
order.TransitionTo(status);
```

Mašina stanja ostaje netaknuta — nema ilegalnih skokova, svaki prelaz je dozvoljen.
Kad Faza 3 uvede "zaključaj raspored" (koji sve narudžbe rute prebaci u
`OutForDelivery`), ovaj kod postaje bezopasan no-op.

Dva ruba koja ovo namjerno **ne** pokriva:

- **Narudžba u statusu `Pending`.** Admin panel je ne nudi za dodjelu, pa je nedostižna
  kroz normalan tok. Ako se ipak pojavi, `TransitionTo` baca `invalid_transition` —
  to je tačno ponašanje, ne treba ga gutati.
- **Ponovni pokušaj neuspjele dostave.** `FailedDelivery → Delivered` nije dozvoljen
  prelaz (ide se preko `OutForDelivery`). U Fazi 1 je neuspjela stanica **konačna u
  aplikaciji** — nema dugmeta za ponovni pokušaj, pa se ovaj slučaj ne može izazvati.
  Ponovno slanje ostaje adminu.

Dvostruki dodir na *Primopredaja izvršena* je bezopasan: `CanTransitionTo` propušta
`Status == next`, pa je ponovljeni isti prelaz idempotentan.

### 3. Ispravka `CLAUDE.md`

`CLAUDE.md` kaže *"UI text and API messages are in Bosnian (BHS)"*. Jezik je **srpski**.
Ispraviti obje pojave, da nova pravila ne navode pogrešno.

## Struktura projekta

```
mobile/
  EPiljara.Driver.sln
  EPiljara.Driver/
    EPiljara.Driver.csproj      net9.0-android
    App.xaml, AppShell.xaml     Shell rute: login → route → stop
    MauiProgram.cs              DI: HttpClient + handleri + servisi + ViewModels
    Models/Dtos.cs              AuthResultDto, AuthUserDto, DeliveryRouteDto,
                                RouteStopDto — 1:1 sa backendom
    Services/
      ApiClient.cs              raspakuje { data, error, meta };
                                na error baca ApiException(code, message)
      AuthHeaderHandler.cs      DelegatingHandler: Bearer token; 401 → odjava
      AuthService.cs            login, token u SecureStorage, tekući korisnik
      DriverService.cs          GetRouteAsync(date?), UpdateStopStatusAsync(orderId, status)
    ViewModels/                 LoginViewModel, RouteViewModel, StopViewModel
    Views/                      LoginPage.xaml, RoutePage.xaml, StopPage.xaml
  EPiljara.Driver.Tests/        xUnit — čista logika, bez uređaja
```

Folder `mobile/` ide kao sestra postojećim `frontend/` i `backend/`.

### Konfiguracija

Bazni URL API-ja **nije hardkodiran** — čita se iz `appsettings.json` ugrađenog kao
MAUI asset, sa razumnim default-om:

- Emulator: `http://10.0.2.2:5080` (`localhost` sa emulatora ne pogađa host)
- Pravi telefon: IP računara na Wi-Fi mreži, npr. `http://192.168.1.10:5080`

Android od verzije 9 blokira običan HTTP. Dev build dobija `usesCleartextTraffic="true"`
u `AndroidManifest.xml` — samo za Debug konfiguraciju, ne za Release.

## Obrada grešaka

`ApiClient` čita `error.code` iz envelope-a i mapira ga u poruku na srpskom:

| Situacija | Ponašanje |
|---|---|
| Nema mreže / timeout | Traka `"Nema veze sa serverom"` + dugme *Pokušaj ponovo*. Lista ostaje na ekranu ako je već učitana. |
| `401` | Token obrisan, povratak na prijavu. |
| `403` (`"Niste registrovani kao vozač."`) | Jasna poruka; odjava. |
| `invalid_transition` | `"Narudžba je već zatvorena."` + osvježavanje liste. |
| Ostale greške | `error.message` sa backenda (već je na srpskom). |

Potvrda primopredaje je **optimistična u UI-ju uz vraćanje na grešku**: stanica se odmah
prebaci u završene, a ako poziv padne, vrati se nazad uz poruku.

## Testiranje

Repo nema automatske testove (`CLAUDE.md`: *"There is no automated test suite"*).
Odstupamo minimalno: `EPiljara.Driver.Tests` (xUnit) pokriva samo logiku koja ne traži
uređaj —

- `ApiClient` raspakivanje envelope-a: uspjeh, `error` sa kodom, prazan `data: null`.
- `RouteViewModel` podjela stanica na aktivne i završene po `Status` polju.
- Sortiranje po `Sequence`.

Ostalo ručno, uz pokrenut backend i emulator. Ručni scenario:

1. `docker compose up -d postgres`, `dotnet run --project src/EPiljara.API`
2. Admin (`admin@epiljara.ba` / `admin123`) na `/admin/routes` dodijeli 2–3 narudžbe vozaču Mladen. **Seed ne sadrži nijednu rutu** — ovaj korak je obavezan da bi aplikacija imala šta da pokaže.
3. Prijava u aplikaciji kao `vozac@epiljara.ba` / `vozac123` → stanice se vide u zadatom redoslijedu.
4. Potvrda primopredaje na prvoj stanici → nestaje iz aktivnih, brojač raste.
5. Restart aplikacije → završena stanica je i dalje završena (provjera da `Status` polje radi).
6. Gašenje backenda → osvježavanje pokazuje traku o vezi, ne pad aplikacije.

## Van obima Faze 1

Namjerno izostavljeno, da bi Faza 1 bila kratka:

- Mapa i pinovi lokacija
- Prava optimizacija rute
- Utovar i notifikacije
- Naplata i dnevni obračun
- iOS verzija
- Rad bez interneta (offline red čekanja)

`RouteStopDto` trenutno **ne** šalje iznos narudžbe ni stavke. Ako se pokaže da vozaču
trebaju, to je dodatno polje — svjesno izostavljeno dok ne zatreba.

## Faze koje slijede

**Faza 2 — pinovi i prava ruta.** `Order` dobija `Latitude`/`Longitude`. Postojeći
`address-picker.tsx` (OpenStreetMap + Leaflet) **već zna koordinate** — Nominatim
geokodira, pin se prevlači — ali ih baca (`CLAUDE.md`: *"Only the address string is
persisted"*). Trebaju: perzistencija tih koordinata, geokodiranje postojećih narudžbi,
mapa sa pinovima u aplikaciji, i zamjena `ZoneRouteOptimizer`-a (hardkodirano sortiranje
po četiri zone Banje Luke) pravom optimizacijom iza istog `IRouteOptimizer` interfejsa.

**Faza 3 — utovar i notifikacije.** Admin akcija "zaključaj raspored" → `RouteStatus`
prelazi u `InProgress` i sve narudžbe rute u `OutForDelivery`; push vozaču da dođe na
lokaciju utovara (Firebase Cloud Messaging + registracija tokena uređaja); vozač potvrdi
utovar; tek tada se stanice otključavaju. Traži i lokaciju utovara (depo) u modelu,
koja danas ne postoji.
