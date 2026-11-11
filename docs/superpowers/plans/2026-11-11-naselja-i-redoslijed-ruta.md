# Naselja i redoslijed ruta — plan implementacije

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Kupac unosi samo adresu, a sistem sam odredi banjalučko naselje ili odbije adresu. Naselja
imaju granice koje admin uređuje. Grupe naselja se dijele po lokaciji na rute od najviše 10 stanica,
rute dobijaju redoslijed od magacina, a sljedeća ruta se računa od pozicije vozača. Dostava je
besplatna uz korpicu, a bez nje 5 KM.

**Architecture:**
- `DeliveryZone` postaje naselje. Granica je GeoJSON tekst u koloni, a geometriju računa
  NetTopologySuite u Application sloju (bez PostGIS-a).
- Početne granice pravi jednokratni C# alat u `tools/naselja`. Rezultat ide kao JSON uz migraciju.
- Podjela, redoslijed stanica (rezerva bez OSRM-a) i redoslijed ruta su čiste funkcije bez baze.
  Dodjela ruta na oba admin ekrana ih koristi preko jedne zajedničke klase.

**Tech Stack:** .NET 9, EF Core 9 + Npgsql, NetTopologySuite 2.5.0 (postoji u lokalnom NuGet kešu, jer
je mreža za `dotnet` blokirana), xUnit + SQLite; Next.js 15 + Leaflet 1.9 (bez dodatne biblioteke za
crtanje); .NET MAUI vozačka aplikacija.

**Spec:** `docs/superpowers/specs/2026-11-11-naselja-i-redoslijed-ruta-design.md`

## Global Constraints

- Najviše 10 stanica po ruti (`Dispecer.MaxStanicaPoRuti`).
- Kod greške van naselja je `van_podrucja_dostave`. Poruka: „Na ovu adresu trenutno ne vršimo dostavu.
  Ako misliš da je greška, javi se administratoru: {telefon}, {mejl}." Kontakt se čita iz
  `SadrzajSajta`, a dio koji nedostaje se izostavlja.
- Početni podaci su 24 aktivna naselja (17 gradskih + Drakulić, Donja Kola, Debeljaci, Šargovac, Motike,
  Saračica, Zalužani). Okolna sela se ne upisuju.
- Dostava: `DostavaSaKorpicom = 0`, `DostavaBezKorpice = 5`, `MinimalnaNarudzba = 0`, sve u
  `Podesavanja`. Korpica je stavka sa `Product.Vrsta == VrstaProizvoda.Korpica`.
- Ugovor sa vozačkom aplikacijom se mijenja samo dodavanjem polja i endpointa.
- Pozicija vozača se ne čuva i ne loguje.
- Stare narudžbe zadržavaju `DeliveryZoneId`, `DeliveryFee` i `TotalAmount`.
- Bez novih npm paketa. Frontend se provjerava sa `npm run build`.
- Tekstovi za korisnika su na srpskom (ijekavica, latinica), a demo imena su srpska.

## Review Focus

1. Tačka tačno na zajedničkoj ivici dva naselja mora dobiti tačno jedno naselje: prvo po imenu,
   deterministički, nikad grešku. Test ide u Task 1.
2. Stara adresa ili pretplata bez koordinata ne smije srušiti pravljenje pretplatne narudžbe. Pada se
   na `Pretplata.DeliveryZoneId`. Test ide u Task 4.
3. Grupa od tačno 10, 11 i 20 narudžbi daje 1, 2 i 2 rute, nijedna preko 10, a 11 se dijeli 6+5.
   Narudžbe bez koordinata ne ruše podjelu. Test ide u Task 8.
4. Zahtjev za sljedeću rutu kad nema preostalih otvorenih ruta vraća `null`, ne grešku. Test ide u Task 10.
5. Kad admin isključi naselje, otvorena narudžba koja nije na ruti ostaje u njemu i označi se kao „van
   granica". Ne pada i ne prelazi u drugo naselje. Test ide u Task 6.

---

## Faza 1 — Naselja u backendu

### Task 1: Geometrija naselja

**Files:**
- Modify: `backend/src/EPiljara.Application/EPiljara.Application.csproj` (dodati `NetTopologySuite` 2.5.0)
- Create: `backend/src/EPiljara.Application/Features/Naselja/Granica.cs`
- Test: `backend/tests/EPiljara.Application.Tests/GranicaNaseljaTests.cs`

**Interfaces:**
- Produces:
  - `static class Granica`:
    - `Geometry Procitaj(string geoJson)` prima GeoJSON `Polygon` ili `MultiPolygon`
      (`{"type":..,"coordinates":..}`, lon/lat). Za neispravan oblik baca
      `DomainException("Granica nije ispravan poligon.", "neispravna_granica")`.
    - `string Zapisi(Geometry g)`
    - `bool Sadrzi(Geometry g, double lat, double lon)` vraća true i na ivici (`Covers`).
    - `double PreklapanjeM2(Geometry a, Geometry b)` računa površinu presjeka u m², približno lokalnom
      projekcijom `x = lon·cos(44.77°)·111320`, `y = lat·110540`.
    - `(double Lat, double Lon) Centar(Geometry g)` vraća `InteriorPoint`.

- [ ] **Step 1: Napiši testove**

  Testovi:
  - `Tacka_unutra_napolju_i_na_ivici` koristi kvadrat (17.19,44.77)–(17.20,44.78):
    - (44.775,17.195) daje true;
    - (44.79,17.195) daje false;
    - (44.77,17.195) daje true.
  - `MultiPolygon_se_cita`
  - `Neispravan_json_baca_neispravna_granica`
  - `Preklapanje_susjednih_kvadrata_je_nula`: kvadrati koji dijele ivicu daju vrijednost < 1.
  - `Preklapanje_pola_kvadrata`: površina je unutar ±2 % od očekivane.

- [ ] **Step 2:** Pokreni `dotnet test --filter GranicaNaseljaTests`. Očekivano: build pada, jer `Granica` ne postoji.
- [ ] **Step 3:** Implementiraj. JSON se čita kroz `System.Text.Json`, a geometrija se gradi preko
  `GeometryFactory`. Restore ide sa lokalnim kešom: ako `dotnet restore` padne, dodaj `--source ~/.nuget/packages`.
- [ ] **Step 4:** Ponovo pokreni testove. Očekivano: PASS.
- [ ] **Step 5:** Commit `feat(naselja): geometrija granice (NetTopologySuite)`.

### Task 2: Šema, početna naselja i podešavanja dostave

**Files:**
- Modify: `backend/src/EPiljara.Domain/Entities/DeliveryZone.cs`:
  - `string? Granica`
  - `double? CentarLat`
  - `double? CentarLon`
  - `string IzvorGranice = "procjena"` (`"procjena"`/`"potvrdjeno"`)
- Modify: `backend/src/EPiljara.Domain/Entities/DeliveryRoute.cs`:
  - `int? RedniBroj`
  - `string? IzvorRedoslijeda` (`"osrm"`/`"vazdusno"`)
- Modify: `backend/src/EPiljara.Domain/Entities/Podesavanja.cs`:
  - `decimal DostavaSaKorpicom = 0`
  - `decimal DostavaBezKorpice = 5`
  - `decimal MinimalnaNarudzba = 0`
- Modify: EF konfiguracija (`Granica` je `text`, `IzvorGranice` je `varchar(16)`).
- Create: `tools/naselja/` (konzolni projekat, NTS) — `Program.cs`, `README.md`, ulazi `grad6.json`
  (granica Grada iz Overpass-a, relacija admin_level 6), `centri.json` (51 centar),
  `prave-granice.json` (Srpske toplice, Kočićev vijenac).
- Create: `backend/src/EPiljara.Infrastructure/Persistence/Podaci/naselja-2026-11-11.json`
  (EmbeddedResource).
- Create: migracija `NaseljaIGranice`.

**Interfaces:**
- Produces:
  - `naselja-2026-11-11.json` je niz `{ naziv, granica (GeoJSON), centarLat, centarLon }` za 24 naselja;
  - kolone iznad;
  - `PodesavanjaDto` dobija tri polja dostave.

- [ ] **Step 1: Napravi alat u `tools/naselja`.**
  1. Projektuj centre lokalno (formula iz Task 1).
  2. Napravi `VoronoiDiagramBuilder` nad svih 51 centara, sa okvirom od granice Grada.
  3. Svaku ćeliju presijeci sa granicom Grada.
  4. Iz susjeda oduzmi prave granice dvije MZ, a tim dvjema MZ upiši prave granice.
  5. Zadrži samo 24 naselja iz Global Constraints.
  6. Vrati u lon/lat, zaokruži na 6 decimala i upiši JSON.

  README navodi izvor (OSM, ODbL, „© OpenStreetMap contributors"), datum i komandu.
- [ ] **Step 2:** Pokreni alat. Provjera: tačno 24 naselja, svaki poligon je `IsValid`, a zbir
  preklapanja je < 1 m². Centar (44.7722, 17.1910) pada u „Borik" ili susjedno gradsko naselje, a
  tačka (44.60, 17.05) ne pada ni u jedno.
- [ ] **Step 3:** Dodaj polja i konfiguraciju, pa izgeneriši migraciju.
  - Pokreni `dotnet ef migrations add NaseljaIGranice` iz `backend/src/EPiljara.Infrastructure`
    (`-s ../EPiljara.API`).
  - U `Up` poslije šeme pročitaj ugrađeni JSON. Za svako naselje radi upsert: postojeća zona čiji
    naziv bez dijakritika i velikih slova odgovara dobija `Granica`/`Centar`, a inače se ubacuje nova
    aktivna zona sa `MinOrderAmount = 0` i `DeliveryFee = 0`.
  - Postojeće zone bez para ostaju netaknute.
  - SQL se gradi parametrima, a ne lijepljenjem stringova.
- [ ] **Step 4:** Proširi `PodesavanjaDto` i komandu izmjene sa tri polja, uz validaciju `>= 0`. Dodaj
  test u `PodesavanjaTests`: `Dostava_polja_se_cuvaju`.
- [ ] **Step 5:** Pokreni `dotnet build` i `dotnet test`, pa `dotnet ef database update` na dev bazi.
  Zatim `select count(*) from "DeliveryZones" where "Granica" is not null` treba da vrati 24.
- [ ] **Step 6:** Commit `feat(naselja): granice 24 banjalučka naselja i podešavanja dostave`.

### Task 3: Određivanje naselja za tačku

**Files:**
- Create: `backend/src/EPiljara.Application/Features/Naselja/OdredjivanjeNaselja.cs`
- Modify: `backend/src/EPiljara.API/Controllers/` (javni kontroler zona): `GET /zones/za-tacku?lat=&lon=`
- Test: `backend/tests/EPiljara.Application.Tests/OdredjivanjeNaseljaTests.cs`

**Interfaces:**
- Produces:
  - `static class OdredjivanjeNaselja`:
    - `Task<DeliveryZone?> NadjiAsync(IAppDbContext db, double lat, double lon, CancellationToken ct)`
      gleda samo aktivne zone sa granicom. Kod više pogodaka (ivica) bira prvu po `Name`, ordinal.
    - `Task<DeliveryZone> OdrediAsync(IAppDbContext db, double? lat, double? lon, CancellationToken ct)`
      vraća `null` ako je tačka van, a kad nema tačke baca `van_podrucja_dostave`.
    - `Task<string> PorukaVanAsync(IAppDbContext db, CancellationToken ct)`
  - `record NaseljeZaTackuDto(Guid Id, string Name)`, koji `GET /zones/za-tacku` vraća u envelope-u.
    Van naselja endpoint vraća 400 sa kodom `van_podrucja_dostave`.

- [ ] **Step 1: Testovi** (SQLite, dvije zone kvadrati, jedna neaktivna, `SadrzajSajta` sa
  telefonom „051 123 456" i mejlom „info@epiljara.ba"):
  - `Tacka_u_aktivnom_naselju`
  - `Tacka_u_neaktivnom_daje_gresku`
  - `Tacka_van_svih_daje_gresku_sa_kontaktom`: poruka sadrži i telefon i mejl.
  - `Bez_kontakta_poruka_bez_zareza`
  - `Tacka_na_zajednickoj_ivici_daje_prvo_po_imenu`
  - `Zona_bez_granice_se_ne_pogadja`
- [ ] **Step 2:** Pokreni testove. Očekivano: FAIL.
- [ ] **Step 3:** Implementiraj, pa dodaj endpoint.
- [ ] **Step 4:** Ponovo pokreni testove. Očekivano: PASS.
- [ ] **Step 5:** Commit.

### Task 4: Narudžbe: naselje sa servera i cijena dostave uz korpicu

**Files:**
- Create: `backend/src/EPiljara.Application/Features/Orders/DostavaPravila.cs`
- Modify:
  - `CreateOrder.cs`
  - `GuestOrderRequests.cs` (create i confirm)
  - `PretplatneNarudzbe.cs`
  - `MojePretplate.cs` (cijena i minimum više ne zavise od zone)
- Test: `backend/tests/EPiljara.Application.Tests/DostavaINaseljeNarudzbeTests.cs`; postojeći
  testovi koji šalju `DeliveryZoneId` se prilagođavaju.

**Interfaces:**
- Consumes: `OdredjivanjeNaselja` (Task 3), `Podesavanja` polja (Task 2).
- Produces:
  - `static decimal DostavaPravila.Cijena(bool imaKorpicu, Podesavanja p)`
  - `CreateOrderCommand.DeliveryZoneId` i `CreateOrderRequest.DeliveryZoneId` postaju `Guid?` i
    server ih ignoriše, osim kao rezervu za pretplatu.
  - Isto važi za `CreateGuestOrderRequestCommand.DeliveryZoneId`.

- [ ] **Step 1: Testovi:**
  - `Narudzba_dobija_naselje_iz_koordinata_a_ne_iz_zahtjeva`: zahtjev nosi Id druge zone, a narudžba
    dobija zonu poligona.
  - `Narudzba_van_naselja_se_odbija`
  - `Narudzba_bez_koordinata_se_odbija`, sa kodom `van_podrucja_dostave` i porukom „Označi adresu na
    mapi." pod `deliveryAddress`.
  - `Korpica_besplatna_dostava`: `DeliveryFee == 0`.
  - `Bez_korpice_dostava_5`: `DeliveryFee == 5` i `TotalAmount` uključuje 5.
  - `Minimum_iz_podesavanja`: `MinimalnaNarudzba = 20` uz subtotal 10 daje `below_minimum`.
  - `Pretplatna_narudzba_bez_koordinata_ide_u_zonu_pretplate`
  - `Gost_potvrda_ponovo_provjerava_naselje`: granica se pomjeri između slanja i potvrde, pa potvrda
    daje `van_podrucja_dostave`.
  - `Gost_zahtjev_korpica_besplatno`
- [ ] **Step 2:** Pokreni testove. Očekivano: FAIL.
- [ ] **Step 3:** Implementiraj.
  - U validatorima ukloni pravilo „Odaberi zonu dostave." i dodaj „koordinate obavezne". Za
    `PretplataId != null` koordinate nisu obavezne.
  - Za pretplatu: ako `NadjiAsync` vrati zonu, koristi nju, a inače `cmd.DeliveryZoneId`.
  - Za ostale: `OdrediAsync`.
  - `imaKorpicu` se računa nad učitanim proizvodima stavki.
- [ ] **Step 4:** Pokreni cijeli `dotnet test`. Očekivano: PASS.
- [ ] **Step 5:** Commit.

### Task 5: Adresar i pretplate određuju naselje

**Files:**
- Modify: `Features/Account/AddressCommands.cs`, `Features/Pretplate/MojePretplate.cs`
- Test: `AddressCommandsTests.cs`, `MojePretplateTests.cs`

**Interfaces:**
- Consumes: `OdredjivanjeNaselja.OdrediAsync`.
- Produces:
  - `DeliveryZoneId` u komandama adrese i pretplate postaje neobavezan i ignoriše se.
  - Server upisuje zonu po tački. Adresa bez tačke ili van naselja se odbija sa `van_podrucja_dostave`.

- [ ] **Step 1:** Testovi:
  - `Adresa_dobija_naselje_po_tacki`
  - `Adresa_van_naselja_se_odbija`
  - `Pretplata_dobija_naselje_po_tacki`
  - `Pretplata_van_naselja_se_odbija`
- [ ] **Step 2:** Pokreni testove. Očekivano: FAIL.
- [ ] **Step 3:** Implementiraj.
- [ ] **Step 4:** Ponovo pokreni testove. Očekivano: PASS.
- [ ] **Step 5:** Commit.

### Task 6: Admin API za naselja

**Files:**
- Create: `backend/src/EPiljara.Application/Features/Naselja/AdminNaselja.cs`
- Modify: `AdminController.cs` (zamjenjuje postojeće admin zone endpointe; stari GET `/zones` ostaje za
  javnu listu)
- Test: `backend/tests/EPiljara.Application.Tests/AdminNaseljaTests.cs`

**Interfaces:**
- Produces:
  - `GET /admin/naselja` vraća `NaseljeAdminDto`:
    - Id, Name, IsActive, Granica, CentarLat, CentarLon, IzvorGranice;
    - `OtvorenihNarudzbi` i `VanGranica` (broj otvorenih narudžbi i adresa čija tačka više nije u
      njihovom naselju);
    - `ImaNarudzbi`.
  - `POST /admin/naselja` — `SacuvajNaseljeCommand(Guid? Id, string Name, bool IsActive, string? Granica,
    double? CentarLat, double? CentarLon, bool Potvrdjeno, bool PotvrdiUticaj)`:
    1. Odbija preklapanje > 1000 m² sa aktivnim naseljem (`naselja_se_preklapaju`, poruka sa imenom).
    2. Ako se mijenja granica ili aktivnost i `PotvrdiUticaj == false`, vraća `uticaj_treba_potvrdu`
       sa brojevima (narudžbe, adrese, pretplate) u `meta`, bez upisa.
    3. Zatim čuva i radi preračun.
  - `DELETE /admin/naselja/{id}` je dozvoljen samo bez narudžbi, inače vraća `naselje_ima_narudzbe`.
  - `static Task PreracunajAsync(IAppDbContext db, CancellationToken ct)` mijenja `DeliveryZoneId` samo
    kad nova zona postoji, i to za:
    - adrese i pretplate sa tačkom;
    - narudžbe u `Pending`/`Confirmed`/`Packing` koje nisu ni na jednoj ruti.

    Cijene se ne diraju.

- [ ] **Step 1: Testovi:**
  - `Cuvanje_odbija_preklapanje`
  - `Rupa_izmedju_naselja_je_dozvoljena`
  - `Izmjena_granice_trazi_potvrdu_uticaja`
  - `Preracun_premjesta_otvorenu_narudzbu_van_rute`
  - `Preracun_ne_dira_narudzbu_na_ruti`
  - `Preracun_ne_mijenja_cijenu`
  - `Iskljuceno_naselje_narudzba_ostaje_i_vodi_se_van_granica` (Review Focus 5)
  - `Brisanje_naselja_sa_narudzbama_se_odbija`
- [ ] **Step 2:** Pokreni testove. Očekivano: FAIL.
- [ ] **Step 3:** Implementiraj.
- [ ] **Step 4:** Ponovo pokreni testove. Očekivano: PASS.
- [ ] **Step 5:** Commit.

## Faza 2 — Rute

### Task 7: Rezervni redoslijed bez OSRM-a i izvor redoslijeda

**Files:**
- Create: `backend/src/EPiljara.Infrastructure/Services/NajbliziSusjedOptimizer.cs` (zamjenjuje
  `ZoneRouteOptimizer`, a stari se briše zajedno sa registracijom)
- Modify:
  - `Services.cs`: `RouteOptimizationResult` dobija `string Izvor`, sa vrijednostima `"osrm"` ili
    `"vazdusno"`;
  - `OsrmRouteOptimizer.cs`
  - `Optimizacija` u `AdminRoutes.cs`
  - `DeliveryRouteDto` dobija `RedniBroj` i `IzvorRedoslijeda`, a mapiranje se dopunjuje.
- Test: `backend/tests/EPiljara.Application.Tests/NajbliziSusjedOptimizerTests.cs`, `KilometrazaRuteTests.cs`

**Interfaces:**
- Produces:
  - `NajbliziSusjedOptimizer : IRouteOptimizer` slaže redoslijed po metodi najbližeg susjeda od starta
    (haversine), pa ga popravlja 2-optom dok ima poboljšanja (najviše 50 prolaza).
  - Kao `DuzinaMetara` vraća vazdušnu dužinu start → … → posljednja.
  - `TrajanjeSekundi` i `Geometrija` su null, a izvor je `vazdusno`.
  - Stanice bez koordinata idu na kraj, ulaznim redom.
  - `Optimizacija.PrimijeniAsync` upisuje `IzvorRedoslijeda`. `Status = Optimized` i
    `KilometrazaMetara` se upisuju samo za `osrm`.

- [ ] **Step 1: Testovi:**
  - `Redoslijed_na_pravoj_liniji`: start, pa tačke 3, 1, 2 po geografskoj dužini, daje 1, 2, 3.
  - `Dva_opt_rasplice_ukrstanje`
  - `Bez_koordinata_na_kraj`
  - `Vazdusna_ruta_nije_Optimized_i_ne_pise_kilometrazu`
- [ ] **Step 2:** Pokreni testove. Očekivano: FAIL.
- [ ] **Step 3:** Implementiraj i prepravi OSRM-ov poziv rezerve.
- [ ] **Step 4:** Ponovo pokreni testove. Očekivano: PASS.
- [ ] **Step 5:** Commit.

### Task 8: Podjela grupe po lokaciji i redoslijed ruta

**Files:**
- Create: `backend/src/EPiljara.Application/Features/Admin/PlanRuta.cs`
- Test: `backend/tests/EPiljara.Application.Tests/PlanRutaTests.cs`

**Interfaces:**
- Produces:
  - `record TackaNarudzbe(Guid Id, double? Lat, double? Lon)`.
  - `static IReadOnlyList<IReadOnlyList<TackaNarudzbe>> PlanRuta.Podijeli(IReadOnlyList<TackaNarudzbe> t, int max = 10)`:
    - `k = ceil(n/max)`; ciljne veličine se raspodjeljuju ravnomjerno (11 daje 6+5).
    - Algoritam je rekurzivna bisekcija: tačke sa koordinatama se sortiraju po osi većeg raspona
      (lokalna projekcija), uz `Id` kao tie-breaker, i režu se proporcionalno broju ruta na svakoj strani.
    - Tačke bez koordinata se poslije dodaju u grupe sa najmanje članova.
    - Rezultat je deterministički.
  - `static IReadOnlyList<Guid> PlanRuta.Redoslijed(IReadOnlyList<(Guid RutaId, IReadOnlyList<(double Lat,double Lon)> Stanice, (double Lat,double Lon)? Posljednja)> rute, double startLat, double startLon)`:
    - prva je ruta čija je najbliža stanica najbliža startu;
    - svaka sljedeća je najbliža posljednjoj stanici prethodne;
    - rute bez koordinata idu na kraj;
    - tie-breaker je `RutaId`.

- [ ] **Step 1: Testovi:**
  - `Petnaest_tacaka_Borika_daju_8_i_7_i_zbijene_su`: dva klastera od 8 i 7 tačaka, odvojena ~800 m,
    i nijedna ruta ne miješa klastere.
  - `Granice_10_11_20` (Review Focus 3)
  - `Ista_ulaz_isti_izlaz` (determinizam, i za permutovan ulaz)
  - `Bez_koordinata_u_najmanju`
  - `Redoslijed_ruta_od_magacina`
  - `Sljedeca_od_posljednje_stanice`
- [ ] **Step 2:** Pokreni testove. Očekivano: FAIL.
- [ ] **Step 3:** Implementiraj.
- [ ] **Step 4:** Ponovo pokreni testove. Očekivano: PASS.
- [ ] **Step 5:** Commit.

### Task 9: Dodjela na oba ekrana koristi podjelu, optimizaciju i redne brojeve

**Files:**
- Create: `backend/src/EPiljara.Application/Features/Admin/RasporedDana.cs`
- Modify:
  - `AdminRoutes.cs` (`AssignRouteHandler`)
  - `AdminDispatch.cs` (`Dispecer.DodijeliNaRutu`, oba handlera dobijaju `IRouteOptimizer`)
  - upiti ruta: `AdminRoutes`, `DriverFeature`, `DriverReoptimize` sortiraju po
    `RedniBroj ?? int.MaxValue`, pa po `Naziv`.
- Create: `POST /admin/dostava/podjela-pregled` vraća `[{ naziv, broj }]` za skup narudžbi, bez upisa.
- Test: `DispecerTests` (postojeći) i novi `RasporedDanaTests.cs`

**Interfaces:**
- Consumes: `PlanRuta` (Task 8), `Optimizacija.PrimijeniAsync` (Task 7).
- Produces: `static class RasporedDana`:
  - `Task PrenumerisiAsync(IAppDbContext db, Guid vozacId, DateOnly datum, CancellationToken ct)`:
    - počete rute (bar jedna zatvorena stanica) i završene zadržavaju broj;
    - nepočete idu iza njih po `PlanRuta.Redoslijed`, a start je posljednja zatvorena stanica dana
      ili magacin.
  - `bool JePocela(DeliveryRoute r)`

Ponašanje:
- **Grupna dodjela:** za svako pogođeno naselje vozača, nepočete rute tog naselja se rasturaju i
  narudžbe se dijele iznova (`Podijeli`) zajedno sa novim. Počete rute se ne diraju, a nove narudžbe
  idu u nove rute.
- **Pojedinačna dodjela:** ide u prostorno najbližu (centroid stanica) nepočetu rutu istog naselja sa
  mjestom, inače u novu.
- **Poslije svake izmjene:** svaka izmijenjena ruta ide na optimizaciju od magacina, pa `PrenumerisiAsync`.
- **`AssignRouteHandler`:** `Chunk` se zamjenjuje sa `Podijeli`, pa slijedi `PrenumerisiAsync`.

- [ ] **Step 1: Testovi:**
  - `Grupna_dodjela_15_Borik_daje_dvije_rute_po_lokaciji`
  - `Pojedinacna_ide_u_najblizu_rutu`
  - `Pocela_ruta_se_ne_rastura`
  - `Redni_brojevi_pocetih_ostaju`
  - `Podjela_pregled_ne_upisuje`
- [ ] **Step 2:** Pokreni testove. Očekivano: FAIL.
- [ ] **Step 3:** Implementiraj.
- [ ] **Step 4:** Pokreni cijeli `dotnet test`. Očekivano: PASS.
- [ ] **Step 5:** Commit.

### Task 10: Sljedeća ruta od pozicije vozača

**Files:**
- Create: `backend/src/EPiljara.Application/Features/Driver/SljedecaRuta.cs`
- Modify: `DriverController.cs`: `POST /driver/route/sljedeca` sa tijelom `{ latitude, longitude }`
  (isti record kao reoptimize)
- Test: `backend/tests/EPiljara.Application.Tests/SljedecaRutaTests.cs`

**Interfaces:**
- Produces: `SljedecaRutaCommand(double Latitude, double Longitude) : IRequest<DeliveryRouteDto?>`:
  1. Uzima nepočete otvorene rute vozača za danas.
  2. Bira onu čija je najbliža stanica najbliža poziciji.
  3. Optimizuje je od pozicije (`odMagacina: false`).
  4. Daje joj prvi broj iza najvećeg broja počete ili završene rute, a ostale nepočete pomjera.
  5. Vraća DTO, ili `null` kad nema preostalih ruta.

  Pozicija se ne upisuje nigdje i ne loguje.

- [ ] **Step 1: Testovi:**
  - `Bira_najblizu_poziciji`
  - `Nema_preostalih_vraca_null`
  - `Pomjera_redne_brojeve`
  - `Pozicija_se_ne_cuva` (nijedna kolona sa pozicijom se ne mijenja; ruta je jedino što se mijenja)
- [ ] **Step 2:** Pokreni testove. Očekivano: FAIL.
- [ ] **Step 3:** Implementiraj.
- [ ] **Step 4:** Ponovo pokreni testove. Očekivano: PASS.
- [ ] **Step 5:** Commit.

## Faza 3 — Web

### Task 11: Checkout, adresar i pretplata bez izbora zone

**Files:**
- Modify:
  - `frontend/src/types/domain.ts`, `lib/api/contracts.ts`, `lib/api/endpoints.ts`, `lib/api/hooks.ts`
    (`useNaseljeZaTacku(lat, lon)`)
  - `components/shop/address-picker.tsx`: poslije potvrde tačke prikazuje „Naselje: X" ili crvenu
    poruku sa servera.
  - `app/(shop)/checkout/page.tsx`, `components/nalog/adresa-forma.tsx`,
    `components/nalog/pretplata-forma.tsx`, `app/admin/orders/new/page.tsx`: bez `<select>` zone.
    Nastavak je blokiran dok naselje nije nađeno.
  - Cijena dostave: „Dostava: besplatno" ili „Dostava: 5 KM", uz napomenu „Uz korpicu je dostava
    besplatna". Iznos se čita iz javnih podešavanja.
- Delete: `frontend/src/lib/zone.ts` (`pogodiZonu`) i njegovi pozivi.
- Test: Vitest za čistu funkciju `cijenaDostave(stavke, podesavanja)` u `lib/dostava.ts`.

- [ ] **Step 1:** Napiši Vitest test `cijenaDostave`:
  - korpica daje 0;
  - bez korpice daje 5;
  - prazna korpa daje 5.
- [ ] **Step 2:** Pokreni ga. Očekivano: FAIL.
- [ ] **Step 3:** Implementiraj `lib/dostava.ts`. Ako javni endpoint podešavanja nema polja dostave,
  dodaj ih na backendu.
- [ ] **Step 4:** Ukloni zonu iz formi i prikaži naselje.
- [ ] **Step 5:** Provjeri sa `npm run lint`, `npx tsc --noEmit` i `npm run build`. Očekivano: sve
  prolazi.
- [ ] **Step 6:** Commit.

### Task 12: Admin „Naselja", podešavanja dostave i dispečer

**Files:**
- Create: `frontend/src/app/admin/naselja/page.tsx`, `frontend/src/components/admin/naselja-mapa.tsx`
  (Leaflet: sva naselja u boji, aktivno puno, neaktivno isprekidano).
  - Izabrano naselje dobija markere temena koji se vuku.
  - Klik na ivicu dodaje teme, desni klik ga briše.
  - „Novo naselje" crta tačke klikom.
  - Otvorene narudžbe su tačke, a one van granica crvene.
- Modify:
  - `app/admin/zone/page.tsx` preusmjerava na `/admin/naselja`;
  - `components/admin/admin-nav.tsx` dobija stavku „Naselja";
  - stranica poslovnih pravila dobija odjeljak „Dostava" sa tri polja;
  - `app/admin/dostava/page.tsx` dobija pregled podjele prije potvrde („Borik 1: 8 · Borik 2: 7"), a
    rute se ređaju po `redniBroj`;
  - `app/admin/routes/page.tsx` ređa rute po `redniBroj`.
- Kod `uticaj_treba_potvrdu`: dijalog sa brojevima i dugme „Potvrdi". Kod `naselja_se_preklapaju`:
  poruka.

- [ ] **Step 1:** Implementiraj stranicu i mapu.
- [ ] **Step 2:** Dodaj podešavanja i dispečerske izmjene.
- [ ] **Step 3:** Provjeri sa `npm run lint`, `npx tsc --noEmit` i `npm run build`. Očekivano: sve
  prolazi.
- [ ] **Step 4:** Ručno u pregledaču:
  1. pomjeri granicu Borika i potvrdi uticaj;
  2. isključi Motike;
  3. pokušaj preklapanje (odbijeno);
  4. dodijeli 15 Borik narudžbi.
- [ ] **Step 5:** Commit.

## Faza 4 — Vozačka aplikacija (`Desktop\EPiljaraVozac`)

### Task 13: Redni broj i „Sljedeća" ruta

**Files:**
- Modify:
  - `EPiljara.Driver/Models/Dtos.cs`: `DeliveryRouteDto` dobija `int? RedniBroj` i
    `string? IzvorRedoslijeda`.
  - `Services/DriverService.cs`: `Task<DeliveryRouteDto?> SljedecaRutaAsync(double lat, double lon)`
    šalje POST `driver/route/sljedeca`.
  - pregled ruta (RouteOverview ViewModel): rute se ređaju po `RedniBroj ?? int.MaxValue`, pa po
    `Naziv`.
  - Kad vozač zatvori posljednju otvorenu stanicu rute, a ima GPS dozvolu, aplikacija uzme jednu
    poziciju, pozove `SljedecaRutaAsync` i ruti iz odgovora da oznaku „Sljedeća: {Naziv}" na vrhu.
    Bez dozvole nema poziva.
  - Kad je izvor `vazdusno`, kilometraža se prikazuje kao „≈ X km (vazdušno)".
- Test: `EPiljara.Driver.Tests`:
  - `Rute_po_rednom_broju`
  - `Bez_rednog_broja_po_nazivu`
  - `Posljednja_stanica_trazi_sljedecu`
  - `Bez_dozvole_ne_salje_poziciju`
  - `Sljedeca_null_nema_oznake`
  - `Vazdusna_kilometraza_oznacena`

  Svaki test mora prvo pasti bez izmjene (pravilo iz CLAUDE.md aplikacije).

- [ ] **Step 1:** Napiši testove.
- [ ] **Step 2:** Pokreni ih. Očekivano: FAIL.
- [ ] **Step 3:** Implementiraj.
- [ ] **Step 4:** Ponovo pokreni testove. Očekivano: PASS.
- [ ] **Step 5:** Build za Android i provjera na emulatoru (`adb emu geo fix` kroz dvije rute).
- [ ] **Step 6:** Ažuriraj README/CLAUDE.md (novi endpoint), commit, merge u main i push.

## Završno

### Task 14: Dokumentacija, dev baza, merge

- [ ] Primijeni migraciju na dev bazu. Provjeri checkout sa adresom u Boriku, na Lauš i van grada.
- [ ] U `STANJE.md` dodaj odjeljak „Naselja i redoslijed ruta". Iz `ZA-URADITI.md` skini C4.
- [ ] Spoji `naselja-dizajn` u main, pushaj i obriši granu.
