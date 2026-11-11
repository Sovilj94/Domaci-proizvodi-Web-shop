# Naselja Banje Luke i redoslijed ruta — dizajn

**Datum:** 2026-11-11
**Obim:** backend + web admin + checkout (ovaj repo) i aplikacija za vozače (`Desktop\EPiljaraVozac`)

## Zašto

Danas kupac pri naručivanju sam bira jednu od 4 zone (Borik, Centar, Lauš, Starčevica). Narudžbe se
dijele na rute od najviše 10 **po vremenu naručivanja**, pa dvije rute istog naselja mogu da se
razvlače preko cijelog naselja. Koja ruta ide prva se ne računa nigdje: rute stoje po abecedi.

Vlasnik hoće sljedeće:

1. **Kupac ne bira naselje.** Unese adresu, a sistem sam odredi banjalučko naselje i narudžbu odmah
   stavi u grupu tog naselja.
2. **Adresa van naselja u kojima dostavljamo se odbija** porukom da se na tu adresu ne vrši dostava i
   da se, ako je u pitanju greška, javi administratoru.
3. **Naselja i njihove granice se čuvaju u aplikaciji**, a admin ih može dodavati i mijenjati kad se
   nešto promijeni.
4. **Grupa naselja se dodjeljuje vozaču u cjelini.** Grupa ima najviše 10 narudžbi. Ako ih je više
   (npr. 15 za Borik), dijeli se na dvije rute **po lokacijama**, tako da bliske adrese idu zajedno.
5. **Pri dodjeli se računa najbrži obilazak**, i vozač stanice vidi tim redoslijedom.
6. **Prva ruta je naselje najbliže magacinu.** Kad vozač završi rutu, sljedeća se bira kao najbliža
   **njegovoj trenutnoj lokaciji**.

## Šta je istraženo (11.11.2026.)

- **Zvanično:** Grad Banja Luka ima **57 mjesnih zajednica** (banjaluka.rs.ba → Gradska uprava →
  Mjesne zajednice). Spisak je dole. Grad uz svaku MZ objavljuje adresu sjedišta i **jednu tačku na
  mapi**, a **granice ne objavljuje** nigdje na internetu.
- **OpenStreetMap:**
  - granice kao poligone ima samo za 2 MZ (Srpske toplice, Kočićev vijenac);
  - ima granicu grada („Grad Banja Luka", admin_level 6) i gradskog naselja („Banja Luka", admin_level 9);
  - ima 27 gradskih četvrti kao **tačke** (Ada, Borik, Centar, Lauš, Starčevica, Obilićevo,
    Paprikovac, Petrićevac, Rosulje, Nova varoš…) i desetine sela kao tačke.
- **Posljedica:** granice moramo napraviti sami. Prva verzija je **procjena** iz centara naselja i
  admin je provjerava na mapi. Bez toga zadatak nije moguć ni na jedan drugi način.

Spisak 57 MZ sa zvaničnog sajta (ćirilica u originalu): Agino Selo, Ada, Bistrica, Borik 1, Borik 2,
Borkovići, Bočac, Bronzani Majdan, Bulevar, Verići, Vrbanja, Goleši, Gornja Piskavica, Debeljaci,
Donja Kola, Dragočaj, Drakulić, Zalužani, Karanovac, Kmećani, Kola, Kočićev Vijenac, Krmine, Krupa na
Vrbasu, Kuljani, Lazarevo 1, Lazarevo 2, Lauš 1, Lauš 2, Ljubačevo, Mišin Han, Motike, Nova Varoš,
Obilićevo 1, Obilićevo 2, Pavići, Paprikovac, Petrićevac, Piskavica, Pobrđe, Potkozarje, Prijakovci,
Priječani, Rekavice 1, Rekavice 2, Rosulje, Saračica, Srpske Toplice, Starčevica, Stratinska,
Stričići, Centar 1, Centar 2, Česma, Čokorska Polja, Šargovac, Šimići.

## Ciljevi, ne-ciljevi, anti-ciljevi

**Ciljevi:** sve iz „Zašto" (tačke 1–6).

**Ne-ciljevi (ovaj put ne):**
- termini dostave (`DeliverySlot`) u računanju redoslijeda;
- automatska raspodjela naselja između više vozača — admin i dalje bira vozača;
- push notifikacije;
- iOS.

**Anti-ciljevi (ako se ovo desi, posao nije uspio, iako „radi"):**
- kupac i dalje mora da bira naselje iz liste;
- server primi narudžbu sa adrese van naselja u kojima dostavljamo (provjera samo u pregledaču nije dovoljna);
- ruta ima više od 10 stanica (Google Maps link je tada odsječen);
- vozačeva lokacija se čuva ili loguje — šalje se samo za jedno računanje, kao i danas
  (vidi CLAUDE.md vozačke aplikacije);
- stare narudžbe, istorija ruta ili poeni promijene naselje unazad.

## Ograničenja

- **Na Renderu nema OSRM-a** (`DEPLOY.md`), pa se ture tamo danas slažu po zonama. Kad je u ruti
  jedno naselje, to sortiranje ne daje nikakav redoslijed. Zato je **rezervni redoslijed bez OSRM-a
  obavezan dio ovog posla** (vidi D3), a ne usputna stvar.
- **Granica od 10 stanica po ruti** ostaje (`MaxStanicaPoRuti`, `NavigacijaLink.MaxStanica`).
- **Bez zakucanih radnih podataka u kodu** (pravilo projekta). Naselja, granice i centri idu u bazu kroz
  migraciju sa početnim podacima i odatle se mijenjaju iz admin panela.
- **Ugovor sa vozačkom aplikacijom** se mijenja samo dodavanjem polja i endpointa. Postojeća polja se
  ne preimenuju (DTO-ovi su ručno preslikani 1:1).
- **Javni geo servisi** (Overpass/Nominatim) se ne zovu u toku rada. Granice se pripreme jednom i
  ulaze u migraciju.

## Kako — odlučeno

### A. Naselje kao zapis u bazi

- **Postojeća `DeliveryZone` postaje naselje** i dobija nova polja:
  - granicu (poligon ili multipoligon kao GeoJSON);
  - centar (lat/lon);
  - izvor granice: `procjena` ili `potvrđeno`.

  `Order.DeliveryZoneId` i sve što na njega gleda ostaje, pa nema velikog prepravljanja.
- **`IsActive` ostaje** i znači „dostavljamo u ovo naselje".
- **Početni spisak (odluka vlasnika, 11.11.2026.): samo banjalučka regija, bez okolnih sela.**
  - Numerisane MZ se spajaju u jedno naselje („Borik 1" + „Borik 2" = „Borik"; isto Centar, Lauš,
    Lazarevo, Obilićevo).
  - U bazu ulaze **24 naselja**, sva aktivna:
    - 17 gradskih: Centar, Bulevar, Obilićevo, Nova Varoš, Borik, Kočićev Vijenac, Paprikovac,
      Pobrđe, Rosulje, Starčevica, Lauš, Petrićevac, Ada, Srpske Toplice, Česma, Lazarevo, Vrbanja;
    - 7 prigradskih do ~7,5 km od centra: Drakulić, Donja Kola, Debeljaci, Šargovac, Motike,
      Saračica, Zalužani.
  - Granica 7,5 km dolazi iz računice isplativosti (jedna narudžba sa korpicom zadržava bar pola
    marže; gorivo 0,24 KM/km iz `DnevneVoznje`, marža ~33 % iz `OrderItems`).
  - **Okolna sela** (Karanovac, Kuljani, Bočac, Krupa na Vrbasu…) **se ne upisuju**. Admin ih dodaje
    na stranici „Naselja" kad odluči da tamo dostavlja.
  - Voronoi se ipak računa sa centrima svih 51 naselja, a čuvaju se samo granice ovih 24. Tako se
    granica Zalužana ne razvuče do Kozare: područje okolnih sela ostaje van svakog naselja i adresa
    tamo dobija grešku iz B.
  - Naselje više **ne nosi cijenu dostave ni minimum** (vidi E). `DeliveryFee` i `MinOrderAmount`
    na zoni se prestaju koristiti za nove narudžbe. Kolone ostaju, da stari podaci ostanu čitljivi.
- **Postojeće 4 zone:**
  - Borik, Centar, Lauš i Starčevica se **ne brišu**: prepoznaju se po imenu i postaju naselja istog
    imena, uz dodatu granicu.
  - Stare narudžbe zadržavaju svoj `DeliveryZoneId` zauvijek.
- **Kako nastaje procijenjena granica:**
  - Za centar svakog naselja uzima se OSM tačka četvrti/sela, a gdje je nema, tačka sa sajta grada.
  - Ravan se dijeli tako da svaka tačka grada pripadne najbližem centru (Voronoi), pa se to odsječe
    granicom „Grad Banja Luka".
  - Dvije MZ koje u OSM-u imaju pravu granicu dobijaju nju, a njihova površina se oduzima od susjeda.
  - Sve dobija oznaku `procjena`.
  - Priprema je jednokratna skripta u repou (`tools/` ili slično), koja dokumentuje izvor i datum.
    Rezultat ide u migraciju kao podatak, a ODbL atribucija ide uz podatke.

### B. Određivanje naselja za narudžbu

- **Checkout, adresar i pretplata više nemaju izbor zone.** Ostaje sve što postoji: pretraga adrese,
  pin na mapi, „Koristi moju lokaciju" i „Ne možeš da pronađeš adresu?".
- **Koordinate su obavezne.** Kad pretraga ne nađe adresu, kupac spusti pin na mapu. Bez pina nema
  nastavka.
- **Naselje se određuje provjerom u koji poligon aktivnog naselja tačka pada.** Radi se:
  - u pregledaču, odmah po potvrdi lokacije, preko endpointa. Tada se ispiše „Naselje: Borik" ili greška;
  - **na serveru, obavezno**, u `CreateOrder` (i gost i prijavljeni kupac), pri čuvanju adrese u
    adresar i pri kreiranju pretplate. Time se zatvara i ZA-URADITI C4.
- **Greška kad tačka ne pada ni u jedno aktivno naselje:**
  - Poruka: „Na ovu adresu trenutno ne vršimo dostavu. Ako misliš da je greška, javi se
    administratoru: {telefon}, {mejl}."
  - Kontakt se čita iz sadržaja sajta u bazi (kontakt blok), a ne iz koda.
  - Kod greške je `van_podrucja_dostave`.
  - Neaktivno naselje daje istu poruku.
- **Cijena dostave više ne zavisi od naselja**, vidi E.
- **Kad admin promijeni granicu ili aktivnost naselja**, ponovo se računa naselje za sačuvane adrese
  kupaca i za **otvorene narudžbe koje još nisu na ruti**. Narudžbe na ruti i zatvorene narudžbe se ne diraju.
- **Postojeća provjera „samo Banja Luka"** (`Podesavanja.GradDostave`, `UPodrucjuDostave` u prijedlozima
  adresa) ostaje, ali samo kao rano upozorenje u prijedlozima. Konačnu riječ ima naselje.

### C. Admin: stranica „Naselja"

- Zamjenjuje sadašnju `/admin/zone`. Ima spisak naselja i mapu Banje Luke sa svim granicama
  (Leaflet, kao ostatak projekta).
- **Admin može:**
  - dodati naselje (ime + nacrtana granica);
  - izmijeniti ime, granicu (pomjeranje tačaka poligona), centar i aktivnost;
  - označiti granicu kao „potvrđeno".
- **Brisanje** je dozvoljeno samo za naselje bez ijedne narudžbe. Inače se nudi „isključi".
- **Preklapanje granica:**
  - Čuvanje se **odbija** kad nova granica preklapa susjednu više od zanemarljive površine, uz poruku
    koje se naselje preklapa.
  - Rupe između naselja su dozvoljene i znače „ne dostavljamo".
- Na mapi se vidi koliko otvorenih narudžbi ima svako naselje i gdje su. Tako admin vidi i pogrešnu
  granicu i narudžbu koja je „pala" u krivo naselje.

### D. Grupe, podjela i redoslijed

**D1. Grupe na dispečerskom ekranu (`/admin/dostava`).** Narudžbe su grupisane po naselju, a cijela
grupa se dodjeljuje vozaču jednim potezom. Grupna dodjela već postoji (`AssignOrdersBulkCommand`), ali
se mijenja podjela.

**D2. Podjela grupe veće od 10:**
- Broj ruta je `ceil(n / 10)`. Narudžbe se dijele **po lokaciji**, tako da je svaka ruta prostorno
  zbijena i nijedna nema više od 10.
- Zamjenjuje sječenje po `CreatedAt` u `AssignRouteHandler` i `Dispecer`, a važi i za pojedinačnu i
  za grupnu dodjelu.
- Podjela je **deterministička**: isti ulaz daje istu podjelu.
- Admin podjelu vidi prije potvrde („Borik 1: 8 · Borik 2: 7").
- Narudžba bez koordinata (stara) ide u rutu sa najmanje stanica.

**D3. Redoslijed stanica u ruti:**
- OSRM `/trip` od polazne tačke, kao danas. Polazna tačka je magacin za planiranje, a vozačeva
  pozicija za sljedeću rutu (D5).
- **Kad OSRM nije dostupan** (Render): najbliži sljedeći po vazdušnoj udaljenosti, pa 2-opt
  popravka. Kilometraža je tada vazdušna procjena sa izvorom `vazdusno` (novo polje na ruti, vidi
  „Odluke poslije recenzije", tačka 7).
  Ovo zamjenjuje sortiranje po zonama kao rezervu.

**D4. Planirani redoslijed ruta:**
- Pri dodjeli se svim vozačevim rutama za taj dan upiše redni broj.
- Prva je ruta čija je **najbliža stanica najbliža magacinu**.
- Svaka sljedeća je ruta najbliža **posljednjoj stanici** prethodne.
- Ruta dobija polje za redni broj, a `DeliveryRouteDto` dobija novo polje. Vozačka aplikacija i
  dispečerski ekran prikazuju rute po njemu umjesto po abecedi.

**D5. Sljedeća ruta od trenutne lokacije vozača:**
- Kad vozač zatvori posljednju otvorenu stanicu rute, aplikacija pošalje njegovu poziciju **samo za
  ovo računanje** na novi endpoint.
- Server tada:
  1. izabere najbližu preostalu otvorenu rutu tog dana (ista mjera kao u D4, od pozicije vozača);
  2. preračuna njene stanice od pozicije vozača (D3);
  3. upiše joj prvi slobodan redni broj;
  4. vrati je.
- Aplikacija je stavi na vrh pregleda sa oznakom „Sljedeća: {naziv}".
- Ako nema GPS-a ili dozvole, aplikacija ostaje na planiranom redoslijedu iz D4 i ništa ne šalje.
- **Pozicija se ne čuva i ne loguje** ni na serveru ni u aplikaciji, kao kod „Preračunaj od moje
  lokacije".

### E. Cijena dostave (odluka vlasnika, 11.11.2026.)

- **Narudžba koja sadrži bar jednu korpicu: dostava je besplatna**, bez obzira na naselje i iznos.
  Narudžbe iz pretplate su uvijek korpice, pa su i one besplatne.
- **Narudžba bez korpice: dostava 5 KM.**
- Oba iznosa su **jedno pravilo za cijeli grad**, u `Podesavanja` (admin: Poslovna pravila → Dostava),
  a ne u kodu i ne po naselju. Početne vrijednosti su 0 KM uz korpicu i 5 KM bez nje.
- **Minimalni iznos narudžbe:**
  - **Moja odluka:** nema ga. Vlasnik ga nije pomenuo, a do sad je bio vezan za zonu.
  - Podešavanje „minimalna narudžba" postoji sa početnom vrijednošću 0, da se može uvesti bez
    programiranja.
  - Provjera minimuma u `CreateOrder`, gost narudžbi i pretplati čita to podešavanje.
- **Šta se računa kao korpica:** stavka narudžbe koja je korpica u istom smislu u kojem je danas
  razlikuju korpa i pretplata. Graditelj koristi postojeći kriterijum iz koda; novi ne izmišlja.
- Pravilo važi na **serveru** (`CreateOrder`, `CreateGuestOrderRequestHandler`, pravljenje
  pretplatnih narudžbi). Checkout i korpa samo prikazuju isti rezultat: „Dostava: besplatno" ili
  „Dostava: 5 KM", uz napomenu „Uz korpicu je dostava besplatna".
- **Stare narudžbe i poslani zahtjevi gostiju** zadržavaju snimljenu cijenu dostave. Na fiskalnom
  računu se red „Dostava" ne ispisuje kad je dostava 0, kao i danas.
- Admin stranica „Naselja" nema polja za cijenu ni minimum.

## Odluke poslije recenzije (moje odluke, vlasnik ih provjerava čitajući)

1. **Dodavanje u već podijeljeno naselje:**
   - Pojedinačna dodjela („Dodaj"/„Prebaci" na `/admin/dostava`, `Dispecer.DodijeliNaRutu`) stavlja
     narudžbu u **prostorno najbližu** rutu istog naselja koja ima mjesta, inače pravi novu. Zatim
     preračuna redoslijed samo te rute (D3).
   - Podjela iz D2 od nule radi se samo pri grupnoj dodjeli, i to samo nad rutama koje još **nisu
     počele** (nijedna stanica zatvorena).
   - Stanice nikad ne prelaze iz rute koja je počela.
2. **Redni brojevi ruta poslije ponovne dodjele usred dana:**
   - Rute koje su počele ili su završene **zadržavaju svoj broj**.
   - Nepočete se prenumerišu iza njih po D4, a polazna tačka je posljednja zatvorena stanica dana,
     ili magacin ako je nema.
   - Ruta izabrana u D5 dobija prvi broj iza najvećeg broja počete ili završene rute, a ostale
     nepočete se pomjeraju za jedan.
3. **Narudžba ili adresa koja poslije izmjene granice ne pada ni u jedno aktivno naselje:**
   - Zadržava staro naselje (`DeliveryZoneId` ostaje obavezan).
   - Označava se kao „van granica" na mapi i u spisku naselja.
   - Admin prije čuvanja granice vidi upozorenje sa brojem pogođenih otvorenih narudžbi, adresa i
     pretplata, i čuvanje mora potvrditi.
   - Takva adresa pri sljedećoj narudžbi daje grešku iz B.
4. **Ponovno računanje naselja ne mijenja cijenu postojeće narudžbe.** `DeliveryFee` i `TotalAmount`
   su snimak u trenutku narudžbe. Minimum se ne provjerava ponovo, a fiskalizacija i poeni ostaju
   nepromijenjeni.
5. **Pretplate se računaju ponovo** isto kao sačuvane adrese (`Pretplata.DeliveryZoneId`), da
   narudžbe koje iz njih nastaju idu u novo naselje.
6. **Dva ekrana za dodjelu:**
   - `/admin/dostava` i `/admin/routes` ostaju, ali koriste **jednu zajedničku** logiku podjele,
     redoslijeda stanica i rednih brojeva ruta.
   - Danas `/admin/dostava` ne poziva optimizaciju; od sada je poziva za svaku rutu koju izmijeni.
7. **Izvor redoslijeda na ruti:**
   - Ruta dobija polje izvora (`osrm` / `vazdusno`).
   - Status `Optimized` i `KilometrazaMetara` (gorivo) se upisuju **samo za `osrm`**.
   - Kod rezerve se `DuzinaMetara` upisuje kao vazdušna procjena, ali sa izvorom `vazdusno`, pa se
     nigdje ne predstavlja kao prava kilometraža. Dnevna tura (`DnevnaVoznja`) zadržava svoje računanje.
8. **Gost narudžbe:** provjera naselja ide i u `CreateGuestOrderRequestHandler` i u
   `ConfirmGuestOrderHandler`. Potvrda zahtjeva **ponovo provjerava** naselje, jer se granica mogla
   promijeniti u međuvremenu.
9. **Postojeće zone u produkciji** (napravio ih je admin, nisu u migraciji):
   - Migracija ih poredi sa 51 naseljem po imenu, bez obzira na velika slova i kvačice.
   - Zona koja se poklopi dobija granicu i centar tog naselja.
   - Zona koja se ne poklopi ostaje kakva jeste, bez granice, pa je nijedna nova adresa ne može dobiti.
     U spisku se vidi kao „bez granice", a admin joj nacrta granicu ili je isključi.
   - Ništa se ne briše.

## Prepušteno graditelju

- Algoritam podjele u D2 (balansirani k-means, sweep oko centra naselja ili slično), dok poštuje
  granicu od 10, determinizam i zbijenost.
- Biblioteka za crtanje i izmjenu poligona u Leafletu. Verzija se zakucava tačno.
- Format čuvanja granice (GeoJSON tekst u koloni ili PostGIS) i kako se radi provjera „tačka u
  poligonu". **Moja preporuka:** bez PostGIS-a, NetTopologySuite u .NET-u, jer Neon/Render
  podešavanje ostaje isto.
- Prag „zanemarljivog" preklapanja u C.
- Tačan izgled stranice „Naselja" i oznake „Sljedeća" u aplikaciji, u postojećem stilu.

## Testiranje

- **Backend:**
  - tačka u poligonu (unutra, napolju, na ivici, rupa između naselja);
  - odbijanje u `CreateOrder` za gosta i kupca, i poruka sa kontaktom iz baze;
  - podjela 15 tačaka Borika na 8 + 7 po lokaciji (fiksni podaci) i determinizam;
  - redoslijed ruta od magacina;
  - izbor sljedeće rute od pozicije;
  - rezervni redoslijed bez OSRM-a;
  - ponovno računanje naselja poslije izmjene granice ne dira narudžbe na ruti.
- **Vozačka aplikacija:** izbor i prikaz „Sljedeća" i ponašanje bez GPS dozvole. Prije svake izmjene
  pokazati da test pada bez nje (pravilo iz njenog CLAUDE.md).
- **Ručno:**
  - checkout sa adresom u Boriku, na Lauš i van grada (greška);
  - admin pomjeri granicu i narudžba pređe u drugo naselje;
  - dodjela 15 narudžbi Borika;
  - emulator sa `adb emu geo fix` kroz dvije rute.
- Frontend obavezno i sa `npm run build`, ne samo sa tsc/lint.
