# Troškovi poslovanja i dobit — dizajn

Odobreno 04.11.2026 („može, kreni sa oba i računanje goriva po rutama odnosno pređenih kilometara").
Nadograđuje analitiku poslovanja (`2026-11-03-poslovanje-analitika-design.md`).

## Cilj
Pored zarade na robi pokazati **pravu dobit**: neto prihod − nabavna roba − ambalaža − gorivo − ostali troškovi.
Svaki broj dolazi iz baze; ništa se ne zakucava u kod (ni vrste troškova).

## 1. Ambalaža po artiklu (automatski)
- `Product.AmbalazaCijena` (`decimal?`, 10,2): trošak ambalaže po jedinici prodaje (kom/kg/l). Za korpicu = kutija.
- `OrderItem.AmbalazaCijena` (`decimal?`): snimak u trenutku narudžbe, upisuje ga `AppDbContext.SaveChangesAsync`
  isto kao nabavnu (samo Added redovi sa null). Zadržani redovi nose svoj snimak.
- Trošak linije = `AmbalazaCijena × Quantity`. Sadržaj korpice nema svoju ambalažu (pokriva je kutija).
- Admin: polje „Ambalaža po jedinici (KM)" na formi artikla i korpice.

## 2. Ručni troškovi
- `VrstaTroska`: Naziv (jedinstven), `JeGorivo` (bool), Redoslijed, Aktivna. Admin ih pravi na stranici Troškovi.
- `Trosak`: Datum (`DateOnly`), VrstaTroskaId, Iznos (>0), Opis, `RacunUrl?` (slika računa, folder `troskovi`).
- `PonavljajuciTrosak`: VrstaTroskaId, Iznos, Opis, OdGodina/OdMjesec, DoGodina?/DoMjesec?. Važi u svakom mjesecu
  u opsegu; „zaustavi" postavlja Do na prošli mjesec. Ne generišu se redovi — mjesec ga uračunava pri čitanju.
- Mjesec troška = mjesec `Datum`-a (po kalendaru), ne sedmica isporuke.

## 3. Gorivo po rutama
- `CijenaGoriva`: VaziOd (`DateOnly`), CijenaPoLitru, PotrosnjaL100km. Važi do sljedećeg unosa. Istorija se ne mijenja
  kad gorivo poskupi.
- `DeliveryRoute.KilometrazaMetara` (`double?`): dužina cijele ture magacin→stanice→magacin. Upisuje se samo kad se
  ruta optimizuje od magacina (admin dodjela / ponovna optimizacija). Vozačeva ponovna optimizacija od trenutne
  pozicije mijenja `DuzinaMetara` (ostatak puta), ali NE `KilometrazaMetara`.
- Trošak rute = km × potrošnja/100 × cijena važeća na `route.Date`. Broje se rute čiji je `Date` u mjesecu i koje
  imaju bar jednu stanicu sa isporučenom ili neuspjelom narudžbom (vozilo je izašlo).
- U dobit ide: **računi za gorivo** (troškovi čija vrsta ima `JeGorivo`) ako ih u mjesecu ima, inače **procjena po
  rutama**. Oba broja se prikazuju radi poređenja.

## 4. Analitika
- `PoslovanjeMjesecaDto.Troskovi`: Ambalaza, Gorivo {Km, BrojRuta, PoRutama?, PoRacunima, UDobiti, Izvor}, PoVrsti
  [{Vrsta, Iznos, Ponavljajuci}], Ostali (zbir bez goriva), Ukupno, Dobit, MarzaDobiti (dobit/neto), TrosakPoNarudzbi,
  BezCijeneGoriva (bool: ima ruta, nema cijene).
- Isto za prethodni mjesec (kartica „Dobit" sa ▲/▼).
- `MjesecGodineDto.Troskovi`, `.Dobit`; godišnji grafikon dobija treću seriju „Dobit".
- PDF i štampa dobijaju odjeljak Troškovi.

## 5. Stranice
- `/admin/poslovanje/troskovi?godina&mjesec`: lista troškova mjeseca (dodaj/izmijeni/obriši, slika računa),
  ponavljajući troškovi (dodaj/zaustavi/obriši), vrste troškova (dodaj/preimenuj/gorivo/aktivna), cijene goriva
  (dodaj/obriši). Link sa mjesečnog pregleda.

## 6. Test podaci
Program `Desktop\epiljara-backupi\troskovi` puni istoriju 11/2023–danas: ambalaža na artiklima, vrste, ponavljajući
(zakup, struja, internet, knjigovođa, osiguranje), jednokratni (servis vozila, gajbe, marketing, kutije na veliko),
cijene goriva kroz vrijeme, rute sa kilometražom (OSRM) za sve isporučene sedmice.

## Van opsega
Kaucija za povratnu ambalažu, PDV, amortizacija, troškovi po vozaču/vozilu.
