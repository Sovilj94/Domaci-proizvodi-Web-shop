# Mapa kao glavni ekran i automatski popup na dolasku — Faza 2

**Datum:** 2026-08-18
**Status:** odobren dizajn, spreman za plan implementacije
**Prethodi:** `2026-08-17-driver-mobile-app-design.md` (Faza 1 — prijava, lista stanica, primopredaja)

## Problem

Vozačka aplikacija iz Faze 1 prikazuje stanice kao listu. Vozač mora sam da prati
gdje je, sam da otvori stanicu i sam da se sjeti da je zatvori. Traženo je da
aplikacija bude vođena mapom: kad je otvori, pred njim je aktivna dostava sa svim
stanicama, a kad stigne na adresu, aplikacija to sama primijeti i ponudi mu šta da
uradi.

To ne može da se izgradi na postojećim podacima. `Order` čuva samo
`DeliveryAddress` kao tekst — koordinate ne postoje nigdje na narudžbi.

## Cilj

Vozač otvori aplikaciju i vidi mapu sa svojim stanicama i sopstvenom pozicijom.
Kad se približi adresi, popup mu sam iskoči i ponudi da pozove kupca i da potvrdi
primopredaju. Kad potvrdi, stanica nestaje sa mape i on nastavlja dalje.

Uspjeh: vozač odvozi turu bez ijednog otvaranja liste i bez ijednog dodira dok
vozi — jedini put kad uzme telefon u ruke je kad je stao pred kućom.

## Dvije cjeline

Posao se dijeli na dvije, koje se grade jedna za drugom:

**2a — Koordinate.** Backend i web. Nijedna linija mobilnog koda.
**2b — Mapa i dolazak.** Mobilna aplikacija. Troši ono što 2a proizvede.

Redoslijed nije proizvoljan: automatska detekcija dolaska tačna je tačno onoliko
koliko su tačne koordinate. Da se gradi obrnuto, greške u geokodiranju otkrile bi
se tek kad popup počne da iskače pred pogrešnom zgradom.

**Ovaj spec daje dva plana implementacije, ne jedan.** Prvo se piše i izvršava plan
za 2a; plan za 2b se piše tek kad koordinate postoje i kad je provjereno da su
tačne na stvarnim adresama. Jedan plan koji bi obuhvatio i migraciju baze i
geokodiranje i dozvole za lokaciju i foreground servis i WebView mapu bio bi
prevelik da se pregleda.

## Odluke i njihov razlog

| Odluka | Razlog |
|---|---|
| **Popup se okida automatski, GPS-om** | Izbor korisnika. Alternativa (vozač sam tapne „Stigao sam") je pouzdanija i jeftinija, ali je odbijena. Ručno dugme ostaje kao rezervni režim kad GPS nije dostupan. |
| **OpenStreetMap kroz WebView, ne Google Maps** | `Microsoft.Maui.Controls.Maps` na Androidu koristi Google Maps i traži API ključ, a ključ traži Google Cloud nalog sa unesenom karticom. OSM je bez ključa i bez naloga, i web aplikacija ga već koristi (Leaflet + Nominatim u `address-picker.tsx`). |
| **Koordinate su nullable** | Geokodiranje promašuje — pogrešno ukucana ulica, novo naselje, broj koji Nominatim ne zna. Stanica bez koordinata ide u listu ispod mape umjesto da blokira dodjelu rute. |
| **Detekcija dolaska je ručno računanje razdaljine, ne Android Geofencing API** | MAUI nema omotač za Geofencing API. Za sedam stanica je računanje razdaljine trivijalno, a — što je važnije — to je čista funkcija koja se testira bez telefona i bez vožnje. |
| **Poziv kupcu nije obavezan** | Izbor korisnika. Ako kupac već čeka ispred zgrade, vozač samo potvrdi primopredaju. |
| **Pozicija vozača se ne šalje na server** | Ostaje na telefonu, koristi se samo za mapu i razdaljinu. Praćenje kretanja zaposlenog je zasebna tema sa pravnom stranom i ne uvodi se usput. |

---

# Dio 2a — Koordinate

## Domen

`Order` dobija dva nullable polja:

```csharp
/// <summary>Geografska širina adrese dostave. null kad geokodiranje nije uspjelo.</summary>
public double? Latitude { get; set; }
public double? Longitude { get; set; }
```

Obrazac već postoji u ovom kodu — `Producer` ima ista dva polja, sa istom
nullable semantikom.

Migracija su dvije nullable kolone. Bez gubitka podataka, bez ponovnog seed-a.

## Tri izvora koordinata

**1. Nove narudžbe — iz pickera.**
`frontend/src/components/shop/address-picker.tsx` već geokodira preko Nominatim-a i
nudi pin koji se prevlači, pa **odbaci** rezultat: `CLAUDE.md` to izričito bilježi
kao *"Only the address string is persisted (no lat/lng)"*. Picker prestaje da ih
baca i prosljeđuje ih uz adresu u payload za kreiranje narudžbe. Ovo je najtačniji
izvor jer je pin ručno namješten.

Dira: `address-picker.tsx`, checkout stranica, `admin/orders/new`, `CreateOrder`
komanda i njen validator.

**2. Postojeće narudžbe — jednokratno geokodiranje.**
Novi admin endpoint `POST /admin/orders/geocode-missing` obrađuje narudžbe kojima
koordinate nedostaju.

Ograničenja koja se moraju poštovati:

- Nominatim dozvoljava **najviše jedan zahtjev u sekundi**
- Zahtijeva `User-Agent` koji identifikuje aplikaciju
- Kršenje vodi u blokadu IP adrese

Zato obrada **ne ide u okviru HTTP zahtjeva**. Endpoint prima posao i vraća
odmah; obrada teče u pozadini sa razmakom od jedne sekunde. Neuspjeh se bilježi
na narudžbi da se isti zahtjev ne vrti unedogled.

**3. Ručna ispravka.**
Admin može otvoriti postojeću narudžbu i pomjeriti pin kad geokodiranje promaši.
Koristi isti `address-picker` koji već postoji.

## Ugovor prema mobilnoj aplikaciji

`RouteStopDto` dobija ista dva polja:

```csharp
public double? Latitude { get; init; }
public double? Longitude { get; init; }
```

Mapiranje u `MappingExtensions.ToDto(this RouteStop s)` ih čita sa `s.Order`.

Mobilna aplikacija DTO-ove **prepisuje ručno** i drži ih 1:1 sa backendom (nema
dijeljenog projekta, samo HTTP ugovor). Zato ista dva polja moraju u
`EPiljara.Driver/Models/Dtos.cs`, kao `double? Latitude` i `double? Longitude` na
`RouteStopDto`. To je jedina izmjena mobilnog koda koja pripada dijelu 2a.

## Nusprodukt koji ne košta ništa

`OptimizeRouteHandler` trenutno gradi `RouteStopInput(s.OrderId, address, zone, null, null)`
— a `RouteStopInput` **već prima** `double? Latitude, double? Longitude`. Kad im se
proslijede stvarne vrijednosti, `ZoneRouteOptimizer` (koji sad samo sortira po četiri
hardkodirane zone Banje Luke) može se zamijeniti pravom optimizacijom iza istog
`IRouteOptimizer` interfejsa, bez diranja ičeg drugog.

**Prava optimizacija nije u obimu ove faze.** Prosljeđuju se samo koordinate.

---

# Dio 2b — Mapa i dolazak

## Ekran

`RoutePage` prestaje da bude lista i postaje mapa:

```
+-----------------------------+
|                             |
|      MAPA (Leaflet/OSM)     |  pinovi 1..N, pozicija vozača
|   +---+                     |  zatvorene stanice sive
|   | 2 |      *              |
|   +---+                     |
+-----------------------------+
| 2 od 7 završeno             |
| > 3. Ana Kovač              |  trenutna stanica
|   Reljina 18 · Borik        |
+-----------------------------+
| Bez tačne lokacije (1)   v  |  sklopiva traka
+-----------------------------+
```

Trenutna stanica je prva nezatvorena po `Sequence`.

## Mapa

MAUI `WebView` koji učitava lokalni HTML iz `Resources/Raw/`. **Leaflet i njegov
CSS su spakovani u aplikaciju**, ne povlače se sa CDN-a — nema zavisnosti od tuđeg
servera i nema iznenađenja ako CDN padne. Pločice mape se povlače sa OSM-a i
zahtijevaju internet.

Komunikacija u oba smjera:

- **C# → JS:** `EvaluateJavaScriptAsync` prosljeđuje stanice kao JSON
- **JS → C#:** tap na pin mijenja lokaciju na `app://stop/{orderId}`, što se hvata
  u `Navigating` događaju i otkazuje prije nego što WebView pokuša da navigira

## Praćenje lokacije

`Geolocation.StartListeningForegroundAsync` sa `GeolocationAccuracy.Best` i
razmakom od 5 sekundi.

Na Androidu je uz to potreban **foreground servis** tipa `location`. Bez njega
sistem uspava aplikaciju čim se ekran zaključa, i vozač ostane bez popup-a usred
vožnje. Servis prikazuje trajno obavještenje dok ruta traje — to je ujedno i
pošteno prema vozaču, jer vidi da aplikacija prati poziciju.

Osluškivanje počinje kad se učita ruta sa bar jednom otvorenom stanicom, a
prestaje kad su sve zatvorene.

## Detekcija dolaska

Čista funkcija, bez ijedne zavisnosti od platforme:

```
ArrivalDetector.Provjeri(pozicija, tačnost, stanice, većJavljene) -> stanica ili null
```

Pravila:

- Javlja stanicu kad je razdaljina **manja od 150 m**
- Preskače zatvorene stanice (`Delivered`, `FailedDelivery`, `Cancelled`)
- Preskače stanice bez koordinata
- **Odbacuje očitanja sa tačnošću lošijom od 100 m** — inače popup iskače pred
  pogrešnom zgradom
- Istu stanicu ne javlja dvaput
- Ponovo se naoruža kad vozač izađe iz kruga pa se vrati

Razdaljina se računa Haversine formulom.

## Popup

Prikazuje redni broj, ime kupca i adresu. Četiri dugmeta:

| Dugme | Radnja |
|---|---|
| **Pozovi** | Otvara telefon. Nije obavezan korak. |
| **Primopredaja izvršena** | `PATCH /driver/orders/{id}/status` → `Delivered` |
| **Neuspjela dostava** | Isto → `FailedDelivery` |
| **Zatvori** | Sklanja popup bez radnje. Ista stanica se ne javlja ponovo dok vozač ne izađe iz kruga. |

Poslije potvrde stanica se zatvara, nestaje sa mape, brojač raste, i sljedeća
postaje trenutna.

**Ekran stanice ostaje.** Popup je prečica, ne zamjena — tap na pin ili na karticu
trenutne stanice i dalje vodi na `StopPage` sa punim podacima.

## Dozvole i otkazivanja

Traži se `ACCESS_FINE_LOCATION`, a od Androida 14 i `FOREGROUND_SERVICE_LOCATION`.
Dozvola se traži pri prvom otvaranju rute, sa objašnjenjem zašto.

**Odbijena dozvola ne smije da zaustavi rad.**

| Situacija | Ponašanje |
|---|---|
| Dozvola odbijena | Mapa prikazuje stanice, bez pozicije vozača i bez automatskog popup-a. Na kartici trenutne stanice se pojavljuje dugme **„Stigao sam"** — ručno pokretanje istog popup-a. |
| GPS nedostupan (tunel, zgrada) | Isto — rezervno dugme. |
| Nema interneta | Pločice mape se ne mogu povući; pada nazad na listu stanica uz traku „Mapa nije dostupna bez interneta". Stanice su već preuzete, posao se nastavlja. |
| Očitanje netačnije od 100 m | Odbacuje se, popup ne iskače. |
| Stanica bez koordinata | Nema pina ni popup-a; stoji u traci ispod mape i zatvara se ručno. |

## Baterija

Osluškivanje traje samo dok ruta ima otvorenih stanica. Kad su sve zatvorene,
prestaje. Razmak od 5 sekundi je kompromis između odziva popup-a i potrošnje.

---

## Testiranje

Emulator prima lažnu GPS poziciju:

```bash
adb emu geo fix 17.1910 44.7722
```

Time se cijeli tok dolaska odglumi za stolom — nema vožnje po Banjoj Luci da bi se
provjerilo da li popup radi.

**Bez uređaja (unit testovi):**

- Haversine razdaljina, provjerena na koordinatama sa poznatim rastojanjem
- `ArrivalDetector`: unutar kruga javlja; izvan ne javlja; ista stanica ne javlja
  dvaput; ponovo se naoruža poslije izlaska; preskače zatvorene stanice; preskače
  stanice bez koordinata; odbacuje netačna očitanja
- Podjela stanica na one sa pinom i one za listu ispod mape
- Izbor trenutne stanice (prva nezatvorena po `Sequence`)

**Na emulatoru (ručno):**

- Pinovi stoje na tačnim mjestima
- `adb emu geo fix` do svake stanice redom → popup iskače kod prave, tačno jednom
- Odbijena dozvola → mapa radi, pojavljuje se dugme „Stigao sam"
- Isključen internet → pad na listu uz traku

**Backend:**

- Geokodiranje poštuje jedan zahtjev u sekundi
- Preskače narudžbe koje već imaju koordinate
- Neuspjeh se bilježi umjesto da se isti zahtjev vrti unedogled
- `RouteStopDto` iznosi koordinate, a `RouteStopInput` ih prima umjesto `null`

## Van obima

- Prava optimizacija rute (koordinate se samo prosljeđuju optimizatoru)
- Slanje pozicije vozača na server i praćenje kretanja
- Utovar i push notifikacije (Faza 3)
- Naplata i dnevni obračun
- Navigacija unutar aplikacije — i dalje se otvara Google Maps
- iOS verzija
- Rad bez interneta
