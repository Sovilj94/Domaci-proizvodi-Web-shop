# Automatsko računanje redoslijeda dostava i ruta na mapi

**Datum:** 2026-08-21
**Status:** odobren dizajn, spreman za plan implementacije
**Prethodi:** `2026-08-18-mapa-i-dolazak-design.md` (Faza 2 — koordinate, mapa, popup na dolasku)

## Problem

Vozač dobija stanice redoslijedom kojim ih je admin naklikao. `ZoneRouteOptimizer` ih sortira
po četiri hardkodirane zone Banje Luke (`Centar`, `Borik`, `Lauš`, `Starčevica`) i unutar zone
zadržava redoslijed unosa. **Koordinate potpuno ignoriše**, iako mu se od Faze 2a prosljeđuju.
Pokreće se samo kad admin stisne „Optimizuj rutu".

Dugme „Navigacija" otvara Google Maps sa **jednom** adresom, pa vozač mora da se vraća u
aplikaciju i pokreće navigaciju iznova za svaku stanicu.

Niko, dakle, ne računa kojim redom je najbrže obići sve dostave.

## Cilj

Kad admin dodijeli rutu, sistem sam izračuna najbrži redoslijed obilaska iz magacina, upiše ga
na stanice, i pošalje vozaču zajedno sa linijom rute i procjenom kilometraže i vremena. Vozač na
svojoj mapi vidi kuda ide, a jednim dodirom pokrene navigaciju za cijelu seriju stanica odjednom.

Uspjeh: vozač ne razmišlja kojim redom da ide, i ne pokreće navigaciju za svaku adresu posebno.

## Odluke i njihov razlog

| Odluka | Razlog |
|---|---|
| **Ruta kreće iz magacina** | Izbor korisnika. „Najbolji redoslijed" nema značenje bez polazišta — redoslijed najkraći iz magacina nije isti kao iz vozačeve kuće. Piljara ima jedno mjesto utovara. |
| **Stvarna razdaljina po putevima, ne vazdušna** | Izbor korisnika. Kroz Banju Luku teče Vrbas: dvije tačke mogu biti 300 m vazdušne linije a 3 km vožnje ako je most daleko. Vazdušna linija bi davala redoslijed lošiji od sadašnjeg. |
| **OSRM, ne Google Routes** | Google-ovo automatsko preslaganje (`optimize:true`) postoji samo u plaćenom Directions API-ju, koji traži nalog sa karticom. OSRM je besplatan, radi nad istim OpenStreetMap podacima koje mapa već crta, i njegov `/trip` endpoint rješava problem trgovačkog putnika u jednom pozivu. Provjereno uživo: 18 stanica prolazi bez problema. |
| **Računa backend, ne telefon** | Jedan izvor istine — admin i vozač vide isti redoslijed. Linija se računa jednom, a ne na svakom telefonu. Ako servis padne, backend se vrati na sortiranje po zonama umjesto da vozač ostane bez ičega. |
| **Zadržavamo i našu mapu i Google Maps** | Izbor korisnika, uz podjelu uloga: naša mapa pokazuje **šta je tura** i daje popup na dolasku; Google Maps **vozi**. Uklanjanje naše mape pretvorilo bi popup iz ekrana pred vozačem u notifikaciju u pozadini, a redoslijed bismo i dalje morali računati sami. |
| **`geometries=geojson`** | OSRM podrazumijevano vraća kodiranu poliliniju koja traži dekoder u JavaScriptu. GeoJSON vraća obične koordinate koje Leaflet crta direktno. Provjereno uživo. |

---

## Dio A — Magacin i interfejs optimizatora

Polazište je **adresa magacina sa koordinatama, u konfiguraciji backenda**. Jedna piljara, jedan
magacin — nema potrebe za tabelom. Ako se kasnije pokaže da admin hoće da je mijenja bez
redeploy-a, prelazak na red u bazi je mala izmjena.

Trenutni interfejs ne zna odakle se kreće i vraća samo redoslijed. Mijenja se u:

```csharp
public interface IRouteOptimizer
{
    Task<RouteOptimizationResult> OptimizeAsync(
        double startLat, double startLon,
        IReadOnlyList<RouteStopInput> stops,
        CancellationToken cancellationToken = default);
}

public record RouteOptimizationResult(
    IReadOnlyList<Guid> Order,
    double? DuzinaMetara,
    double? TrajanjeSekundi,
    string? Geometrija);
```

Jedan implementator i jedan pozivalac, pa je izmjena bezbolna.

**Optimizacija prestaje da bude samo dugme.** Pokreće se automatski kad admin dodijeli rutu
(`AssignRouteCommand`), a postojeće dugme „Optimizuj rutu" ostaje za ponovno pokretanje kad se
ruta izmijeni.

## Dio B — OsrmRouteOptimizer

Zove OSRM `/trip` sa:

- `source=first` — kreni iz magacina, koji se šalje kao prva koordinata
- `roundtrip=false` — ne vraćaj se na polazište
- `geometries=geojson` — linija kao obične koordinate
- `overview=simplified` — dovoljno tačna linija, manji odgovor

Jedan poziv vraća redoslijed (`waypoint_index` po tački), ukupnu kilometražu i trajanje, i
geometriju.

**Stanice bez koordinata se ne šalju** servisu. Dodaju se na kraj redoslijeda i ostaju u traci
„Bez tačne lokacije" u aplikaciji.

**Kad servis ne odgovori**, vraćamo se na postojeće sortiranje po zonama i rutu bez linije,
kilometraže i vremena. Vozač dobije turu koja radi, samo neoptimizovanu — nikad prazan ekran.
Neuspjeh se bilježi u log, ne baca se prema korisniku.

Servis se predstavlja `User-Agent`-om koji identifikuje aplikaciju, kao i kod geokodiranja.

## Dio C — Šta se čuva i kako stiže do aplikacije

`DeliveryRoute` dobija tri nullable polja: `DuzinaMetara`, `TrajanjeSekundi`, `Geometrija`.
Nullable jer ruta bez optimizacije mora i dalje da radi.

**Redoslijed se ne čuva zasebno.** Već postoji `RouteStop.Sequence`; optimizacija ga prepisuje.
To je ujedno i temelj za sječenje na serije u dijelu D.

`DeliveryRouteDto` iznosi ista tri polja. Mobilni `Models/Dtos.cs` ih ručno preslikava, po
pravilu koje važi od Faze 1 — DTO-ovi se drže 1:1 sa backendom i repoi ne dijele kod.

Migracija su tri nullable kolone na `DeliveryRoutes`.

## Dio D — Linija na mapi i serije za Google Maps

### Linija

`map.html` dobija `postaviRutu(geojsonKoordinate)` koja crta `L.polyline`. Pošto OSRM vraća
GeoJSON, koordinate stižu kao `[dužina, širina]` — **obrnuto od Leafletovog `[širina, dužina]`**,
pa se pri crtanju moraju zamijeniti. To je klasičan izvor grešaka i mora biti pokriveno testom.

Kartica trenutne stanice dobija ukupnu kilometražu i vrijeme, npr. „3,7 km · 8 min".

### Serije

Google Maps kroz `maps/dir/?api=1` prima polazište, **do devet međustanica** i odredište. Polazište
je vozačeva trenutna pozicija (parametar se izostavlja), pa jedan link nosi **deset naših stanica**:
devet kao međustanice i desetu kao odredište. I ne preslaže ih — vozi ih redom kojim mu se daju.

Zato:

1. **Optimizacija pokriva cijelu turu odjednom.** Dvadeset četiri stanice znače da OSRM računa
   redoslijed za sve 24 i upisuje `Sequence` 1–24. Ništa se u toj fazi ne siječe.
2. **Dugme „Navigacija" reže iz tog redoslijeda.** Uzme **otvorene** stanice poredane po
   `Sequence`, uzme prvih deset, napravi link. Ostatak se ne šalje.
3. **Dugme je dostupno u svakom trenutku, ne samo kad se serija završi.** Pravilo je uvijek isto:
   prvih deset otvorenih po redoslijedu. Ako je vozač od deset poslatih odradio tri, sljedeći
   dodir daje onih sedam preostalih **plus tri naredne** — opet punih deset. Ne mora da doveze
   seriju do kraja da bi povukao nove; može poslije jedne dostave, poslije par, ili tek kad
   isprazni seriju.
4. **Nikad se ne računa iznova.** Serija je samo prozor u već izračunatu rutu. Ako vozač preskoči
   stanicu i ostavi je otvorenu, ona ostaje u sljedećoj seriji na svom mjestu, jer je prozor
   vezan za `Sequence`, a ne za to koliko je puta dugme stisnuto.

Dugme nosi i broj, da vozač zna šta dobija: **„Navigacija (10 od 24)"** — koliko ide u ovaj link
i koliko je ukupno otvorenih. Kad otvorenih ima deset ili manje, piše samo „Navigacija", jer
tada ostatka nema.

Dugme ide na ekran rute, za cijelu seriju. Postojeće dugme na ekranu stanice ostaje, za slučaj
kad vozač hoće samo tu jednu adresu.

Vozač se poslije svake dostave ionako vraća u aplikaciju da potvrdi primopredaju, jer mu tamo
iskače popup — pa pokretanje navigacije po serijama nije dodatni teret, nego dio postojećeg toka.

## Dio E — Ručna ponovna optimizacija od trenutne pozicije

Vozač dobija dugme koje ponovo računa redoslijed **od mjesta gdje se trenutno nalazi**, umjesto
od magacina. Koristi se kad tura krene naopako: zastoj u saobraćaju, preskočena stanica, ili
dodata narudžba usred ture.

Računa se na backendu kao i svaka druga optimizacija, samo sa drugim polazištem. Ulaze samo
**otvorene** stanice; zatvorene se ne diraju.

Numeracija mora ostati bez preklapanja, i to je mjesto gdje se lako pravi greška: zatvorene
stanice zadržavaju svoje brojeve, a otvorene se prenumerišu **počevši od broja za jedan većeg od
najvećeg zatvorenog**. Ako su stanice 1, 2 i 3 zatvorene a 4–9 otvorene, poslije ponovne
optimizacije otvorene nose brojeve 4–9 u novom redoslijedu. Nikad se ne kreće od jedinice, jer bi
se sudarile sa zatvorenima i sječenje na serije bi počelo da vraća pogrešne stanice.

### Izmjena ranije odluke o privatnosti, izričito

Faza 2 je postavila pravilo: **pozicija vozača se nikad ne šalje na server.** Ova funkcija to
mijenja, i to se ovdje bilježi svjesno, a ne prećutno.

Razlika je u prirodi slanja:

- **Ne uvodi se praćenje.** Ne postoji endpoint koji prima poziciju u pozadini, niti bilo šta
  što je periodično šalje.
- **Šalje se jedna koordinata, na izričit dodir vozača**, kao parametar računanja.
- **Ne čuva se.** Koordinata ne ulazi ni u jednu kolonu; koristi se u pozivu prema OSRM-u i
  odbacuje. U logu se ne bilježi.

Ako se ikada poželi da se pozicija čuva ili prati, to je zasebna odluka sa pravnom stranom i
ne smije se dogoditi kao nusprodukt ove funkcije.

---

## Šta moraš uraditi prije produkcije

| Stavka | Zašto |
|---|---|
| **Zamijeniti OSRM demo server** | `router.project-osrm.org` je po pravilima korišćenja za testiranje i razvoj, ne za produkciju, i nema garantovanu dostupnost. Rješenja: sopstvena instanca (Docker + OSM izvod za BiH) ili OpenRouteService sa besplatnim ključem. Interfejs je isti, mijenja se samo implementacija. |
| **Upisati stvarne koordinate magacina** | Konfiguracija kreće sa placeholder vrijednošću. Bez ovoga se cijela ruta računa od pogrešne tačke, a rezultat izgleda uvjerljivo. |
| **Zamijeniti `https://api.example.invalid`** | Naslijeđeno iz Faze 2a: `appsettings.Release.json` nosi namjerno neispravan URL. Bez izmjene Release build ne može doći ni do jednog backenda, a mora biti `https` jer Release zabranjuje običan HTTP. |
| **Provjeriti Google-ovu granicu međustanica** | Dizajn radi po tome da je devet međustanica plus odredište. Potvrditi u Google-ovoj dokumentaciji prije puštanja — ako se granica smanji, sječenje na serije puca tiho. |
| **Nominatim za geokodiranje adresa** | Isti problem kao OSRM: javni servis nije za masovnu upotrebu. Naslijeđeno iz Faze 2a. |
| **Provjeriti foreground servis na stvarnom telefonu** | Iz Faze 2b: sa `NotSticky`, ako Android ubije proces usred smjene, praćenje tiho prestane i aplikacija to ne može otkriti. |

## Greške

| Situacija | Ponašanje |
|---|---|
| OSRM ne odgovori ili vrati grešku | Sortiranje po zonama, ruta bez linije i bez procjene. Tura radi. |
| Nijedna stanica nema koordinate | Optimizacija se preskače u cjelini, redoslijed ostaje kakav je admin postavio. |
| Dio stanica bez koordinata | Optimizuju se samo one sa koordinatama; ostale idu na kraj i stoje u žutoj traci. |
| Ponovna optimizacija bez dozvole za lokaciju | Dugme se ne prikazuje — isto pravilo kao „Stigao sam" iz Faze 2b, vezano za `PracenjeRadi`. |
| Manje od dvije tačke sa koordinatama | Nema šta da se optimizuje; poziv se ne šalje. |

## Testiranje

**Bez mreže i bez uređaja:**

- Građenje Google Maps linka: tačan broj međustanica, redoslijed, escape-ovanje adresa
- Sječenje na serije: preko deset stanica, nekoliko zatvorenih u sredini, tačno deset, manje od deset
- Sječenje usred serije: od deset poslatih tri zatvorene → sljedeći poziv vraća sedam preostalih
  plus tri naredne, u ispravnom redoslijedu
- Natpis dugmeta: „Navigacija (10 od 24)" kad ima ostatka, samo „Navigacija" kad ga nema
- Zamjena `[dužina, širina]` iz GeoJSON-a u Leafletov `[širina, dužina]`
- Izbor stanica za ponovnu optimizaciju: samo otvorene, zatvorene zadržavaju `Sequence`

**Pokretanjem (backend nema test projekat):**

- OSRM poziv vraća redoslijed i geometriju za stvarne koordinate Banje Luke
- Pad servisa vodi na sortiranje po zonama, a ne u grešku prema korisniku
- Stanice bez koordinata završe na kraju redoslijeda

**Na emulatoru:**

- Linija rute se crta preko pravih ulica, a ne kao prava crta između pinova
- „Navigacija" otvara Google Maps sa svim stanicama serije u tačnom redoslijedu
- Poslije zatvaranja prvih deset, isto dugme otvara sljedeću seriju
- Ponovna optimizacija od trenutne pozicije mijenja redoslijed i liniju

## Van obima

Navigacija skretanje-po-skretanje unutar naše aplikacije (glas, preusmjeravanje, saobraćaj uživo) —
to je zaseban proizvod, mjeseci posla, i teško bi bio bolji od Google Maps-a koji vozač već ima.
Takođe: vremenski prozori dostave, kapacitet kombija, više vozača na istoj optimizaciji, i čuvanje
ili praćenje pozicije vozača.
