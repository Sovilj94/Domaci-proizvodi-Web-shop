# Stanje projekta — 2026-10-15 (P1–P7 gotovi)

Predaja sesije. Šta je urađeno, šta nije, i gdje tačno stati nogom kad se nastavi.

## Gdje je kod

| | |
|---|---|
| Grana | `main` i samo ona. Sve grane, zaključno sa `p7-cirilica`, su spojene i obrisane. |
| HEAD `main` | merge P7 (`7993626`) — vidi `git log -1` |
| Radno stablo | čisto |
| Nespojeno | ništa |
| `main` vs `origin/main` | **pushovan 2026-10-15** (P7), na korisnikov zahtjev. |
| Radna kopija | `C:/Users/soviljn/Desktop/epiljara-backend-wt` (ovdje se radi) |
| Druga kopija | `C:/Users/soviljn/Desktop/Kumov Web Shop` — detached HEAD na `0dadb11`, daleko iza. Ne dirati. |

Vraćanje obrisane grane ako ikad zatreba: `git branch redizajn-opcija3 9ca050c`.

## Gotovo i spojeno

**P1 — Korpice i sedmični ciklus.** Korpica je `Product` sa `Vrsta = Korpica`. Sedmični
sadržaj (`SedmicniSadrzaj` + `StavkaSadrzaja`), generator, ciklus nacrt → objavljen → zaključan,
pozadinski posao koji zaključava po isteku roka, javne stranice `/korpice`, admin ekran.

**P2a — Moj nalog.** `/moj-nalog` sa četiri sekcije (Pregled, Lični podaci, Adrese, Narudžbe),
straža u `layout.tsx` jednom. `PUT /account/profile` za ime i telefon. Prvi ekran nad adresarom
koji je na backendu stajao nepozvan od septembra. `/orders` je sada serverski redirect.

**P2b — Moja korpica.** Kupac mijenja sadržaj naručene korpice do roka, u okviru 95–100%
budžeta. Uz to dva preduslova koja su i sama popravke: `Order` dobija ISO sedmicu isporuke, i
pakerska lista prestaje da sabira šablon a počinje da sabira narudžbe (prije toga je četrdeset
narudžbi i jedna davale isti broj, pa je nabavka kupovala za jednu kutiju).

**P2c — Birač adrese na mapi u adresaru.** Prva stavka koja je u ranijoj verziji ovog
dokumenta stajala kao „nalaz nošen dalje". Forma adresara više nije tekstualno polje nego
isti `AddressPicker` koji koriste checkout i admin, pa sačuvana adresa nosi tačku i ruta je
ne vraća na sortiranje po zonama. Backend nije mijenjan — `latitude`/`longitude` je primao
od P2a, samo mu ih niko nije slao.

Dvije popravke u samom biraču, obje zato što bi inače obrisao tuđi pin: ne geokodira dok je
tekst onaj sa kojim je tačka došla (`polazniTekst`), i pri gašenju javlja `null` samo ako je
ikad sam javio tačku (`preuzeoTacku`). Obje su pisane da podnesu dvostruko izvršavanje —
React u razvoju (StrictMode) montira, počisti pa montira ponovo, i obje greške su se baš tu
pokazale. Isto je uklonilo i tiši kvar na admin ekranu *Magacin*, koji je jedini drugi
pozivalac sa `initialCoordinates`.

Uz to: pogađanje zone po naselju je izvađeno iz checkouta u `lib/zone.ts`, pa ga oba ekrana
zovu iz istog mjesta, a kartica adrese bez tačke to prigušeno kaže.

**P3 — Pretplate.** Kupac se pretplati na korpicu i narudžbe nastaju same, jednom sedmično,
48 sati prije roka. Pauza (1–8 isporuka), promjena paketa, nastavak i otkaz sa
`/moj-nalog/pretplate`. Popust se zarađuje unazad: prvih pet isporuka po punoj cijeni, od
šeste niža (`PretplatnaCijena`, jedino mjesto koje zna to pravilo).

Tri stvari iz P3 vrijedi znati prije nego se išta oko narudžbi dira:

1. **Posao ide kroz `CreateOrderHandler`, ne pored njega.** Komanda je dobila `CustomerId` i
   `PretplataId` koje nijedan kontroler ne popunjava — postoje zbog jedinog pozivaoca bez
   `ICurrentUser`. Tako pretplatna narudžba dobija snapshot korpice, sedmicu, zalihe i mejl
   besplatno (provjereno: na pretplatnoj narudžbi stoji šest redova sadržaja).
2. **Idempotentnost je dvostruka i u transakciji.** `Poslednja*` kao brz filter, upit nad
   narudžbama kao istina, oboje u istoj transakciji sa narudžbom. Dva prolaza daju jednu
   narudžbu — provjereno i testom i uživo (restart API-ja = novi prolaz).
3. **`BrojIsporuka` diže samo `BrojacIsporuka`**, zvan sa oba mjesta koja postavljaju
   `Delivered` (admin panel i vozačka aplikacija), prije prelaza statusa.

Stanje provjera na `main`-u: **`dotnet test` 199/199**, `tsc` čisto, `lint` čisto,
`npm run build` ✓ 55/55 stranica.

## Gotovo i spojeno — P4 (merge `f099c57`, 2026-10-14)

**P4 — Marketinška početna.** Početna `/` prodaje korpicu: hero „Domaće namirnice, svake
sedmice na tvojim vratima" sa cijenom „već od X KM" (najjeftinija dostupna korpica) i
**odbrojavanjem do roka**, pa „Kako funkcioniše" (tri koraka), postojeće kategorije i izdvojeni
proizvodi, domaćini sa do četiri svoja artikla, **utisci kupaca** (unosi ih admin na
`/admin/utisci`), **česta pitanja** i promo baner koji sad vodi na `/korpice`. Footer više ne
piše zakucano „Narudžbe do utorka u 12h" nego rok sa servera.

Šta vrijedi znati prije nego se dira:

1. **Početna ne kuca brojeve.** Rok, dan isporuke i popust pretplate dolaze sa servera
   (`/korpice/sedmica`, `KorpicaDto`). Rečenica kojoj treba broj ide kroz
   `lib/cesta-pitanja.ts` (`kontekstPitanja`) ili `lib/sedmica.ts`.
2. **Poslije roka početna govori o narednoj sedmici, `/korpice` i dalje o tekućoj.**
   `SedmicaDto` od P4 nosi `SljedeciRok`/`SljedecaIsporuka`; `aktivniKrug()` bira krug koji
   checkout stvarno prodaje. Izlog `/korpice` je namjerno ostavljen (spec O7) — otvoren nalaz.
3. **Utisci se ne seeduju, ni u demo seedu.** Sekcija se ne prikazuje dok nijedan nije
   objavljen. Izmišljen utisak na staging deployu je gori od prazne sekcije.
4. **Odbrojavanje čita sat samo u efektu** (hidratacija) i, kad dođe do nule, traži sedmicu
   ponovo svakih 10 s dok server ne potvrdi prelaz — inače bi se kod sata kupca koji žuri
   zaglavilo na „Rok je upravo istekao".

Provjereno uživo: desktop i 375px bez horizontalnog skrola; stanje prije roka (sekunde u
zadnja 24 h, rok sa minutama „6:30h"); **stvaran prelaz na nuli** na „Ova sedmica je
zatvorena — rok za sljedeću"; utisak kroz API se pojavi na početnoj i nestane po brisanju;
ključevi grešaka `input.*` na žici; 401 bez tokena.

Stanje provjera na `main`-u poslije P4: **`dotnet test` 215/215**, `tsc` čisto, `lint` čisto,
`npm run build` ✓ 56/56 stranica.

## Gotovo i spojeno — P5 (2026-10-14)

**P5 — Proizvođači, O nama, Recepti, Analize, Postani partner.** Pet javnih stranica i tri admin
ekrana:

- `/proizvodjaci` i `/proizvodjaci/[id]`: domaćin, njegovi artikli i njegove objavljene analize.
  Kartica domaćina na početnoj sada vodi tamo.
- `/o-nama` sa kontaktom (`#kontakt`). Kontakt podaci su izvađeni u `lib/kontakt.ts`, a footer
  dobija kolonu „E piljara".
- `/recepti` i `/recepti/[slug]`: sastojci vezani za ponudu, dugme „Dodaj sastojke u korpu".
  Admin ih uređuje na `/admin/recepti`.
- `/analize`: laboratorijski nalazi sa bedžom usklađeno ili nije usklađeno i linkom na PDF.
  Admin ih uređuje na `/admin/analize`.
- `/postani-partner`: forma za domaćinstva, sa honeypotom i ograničenjem broja zahtjeva. Admin
  obrađuje prijave na `/admin/partneri`, a navigacija pokazuje bedž sa brojem novih.

Šta vrijedi znati prije nego se dira:

1. **Analize se nikad ne seeduju**; nalaz koji nije usklađen objavljuje se isto kao usklađen.
   Proizvođač sa analizama se ne može obrisati (409).
2. **Recepti se seeduju samo u demo seedu** (dva). Slug recepta se nikad ne mijenja.
3. **Sastojak nudi proizvod samo ako je običan i neobrisan.** Izmjena recepta ili analize
   prihvata već vezan proizvod i kad je u međuvremenu obrisan.
4. **Enumi na žici su PascalCase stringovi**, a ulaz se provjerava sa `TryParse` + `IsDefined`.
   Svaki novi validator počinje sa `Input NotNull` (inače neispravan JSON daje 500).
5. **Javni tekst** na ovim stranicama tvrdi samo ono što aplikacija radi. Dvije rečenice je
   završna recenzija morala ublažiti: „šaljemo uzorke… objavljujemo sve" i „otkup".

Stanje provjera na `main`-u poslije P5: **`dotnet test` 256/256**, `tsc` čisto, `lint` čisto,
`npm run build` ✓ 64/64 stranica.

Provjereno uživo (API + pregledač): prijava partnera 201, honeypot 201 bez upisa, `null` tijelo i
prazan datum analize → 400 (ne 500), brisanje proizvođača sa analizom → 409; `/analize` prikazuje
neusklađen nalaz i **ne renderuje `javascript:` link**; stranica domaćina sa artiklima i analizom,
bez telefona, nepostojeći id → „nije pronađen"; recept → „Dodaj sastojke u korpu (4)" ubacio tačno
četiri vezana proizvoda po jedan, „so" ne; svih pet stranica na 375px bez horizontalnog skrola;
honeypot skriven i nefokusabilan. Ručni prolaz je našao englesku poruku validacije za telefon i
stranicu bez `h1` — popravljeno prije merge-a. Test podaci (analiza, prijava) obrisani iz dev baze.

## Popis — šta tačno postoji

Da se ne vadi iz koda pri nastavku.

**Endpointi kupca** (`AccountController`, svi `[Authorize]`)

```
PUT    /account/profile                 ime i telefon
GET    /account/addresses               adresar
POST   /account/addresses
PUT    /account/addresses/{id}
DELETE /account/addresses/{id}          jedini 204 bez tijela u cijelom API-ju
POST   /account/addresses/{id}/default
GET    /account/korpice                 korpice koje se trenutno mogu mijenjati
GET    /account/korpice/{orderItemId}   sadržaj, budžet, rok, ponuda
PUT    /account/korpice/{orderItemId}   tijelo: samo { productIds }
GET    /account/pretplate                                                   (P3)
POST   /account/pretplate
PUT    /account/pretplate/{id}
POST   /account/pretplate/{id}/pauza    tijelo: { sedmica: 1..8 }
POST   /account/pretplate/{id}/nastavi
DELETE /account/pretplate/{id}          drugi 204 bez tijela u API-ju
```

**Javno** — `GET /korpice`, `/korpice/sedmica`, `/korpice/{slug}`, `/korpice/{slug}/sadrzaj`.
Od P3 `KorpicaDto` nosi i `cijenaSaPretplatom` + `isporukaDoPopusta`; klijent popust ne računa.
Od P4: `/korpice/sedmica` nosi i `sljedeciRok` + `sljedecaIsporuka`, `GET /producers` nosi
`artikli` (do četiri dostupna obična artikla), i novi **`GET /utisci`** (samo objavljeni).
Od P5: `GET /products?producer={id}`, `GET /recepti`, `GET /recepti/{slug}`,
`GET /analize?producer=`, i **`POST /partneri/prijave`** (bez prijave, politika `auth`, honeypot `web`).

**Admin** — `/admin/korpice/sadrzaj`, `/sadrzaj/generisi`, `/sadrzaj/{id}`,
`/sadrzaj/{id}/objavi`, `/admin/korpice/pakerska-lista?godina=&sedmica=`,
`/admin/pretplate?status=` (P3 — koliko je korpica obećano za narednu sedmicu i koliko njih
još čeka sadržaj), `GET/POST /admin/utisci`, `PUT/DELETE /admin/utisci/{id}` (P4, ekran
`/admin/utisci`). Od P5: `/admin/recepti`, `/admin/analize` (GET/POST, PUT/DELETE `{id}`),
`GET /admin/partneri?status=`, `PATCH /admin/partneri/{id}` — ekrani istih imena.

**Entiteti dodati kroz P1–P5** — `SedmicniSadrzaj`, `StavkaSadrzaja`, `KorpicaKategorija`
(P1); `StavkaKorpiceNarudzbe` (P2b); `Pretplata` (P3); `Utisak` (P4, migracija `DodajUtiske`);
`Recept` + `ReceptSastojak`, `Analiza`, `PartnerPrijava` (P5, migracije `DodajRecepte`,
`DodajAnalize`, `DodajPartnerPrijave`). `Product` je dobio `Vrsta`,
`TipKorpice`, `Velicina`, `MozeUKorpicu`, `PrioritetUKorpici`, `Mjeseci`. `Order` je dobio
`Godina`, `Sedmica` i `PretplataId`.

**Ekrani kupca** (adresar od P2c nosi mapu u formi) — `/korpice`, `/korpice/[slug]` (od P3 ima
i „Pretplati se"), `/moj-nalog` i pod njim `/podaci`, `/adrese`, `/narudzbe`, `/korpica`,
`/pretplate`. `/orders` je serverski redirect na `/moj-nalog/narudzbe`. U navigaciji naloga
više nema zaključanih stavki. Od P5 i `/proizvodjaci`, `/proizvodjaci/[id]`, `/o-nama`,
`/recepti`, `/recepti/[slug]`, `/analize`, `/postani-partner` — linkovane iz footera.

**Pozadinski poslovi** — `ZakljucavanjeSadrzajaService` (svakih 15 minuta zaključava sadržaje
kojima je rok prošao), `PretplatneNarudzbeService` (svakih 15 minuta pravi pretplatne
narudžbe), `ExpiredGuestRequestsCleaner`.

**Zajedničke tačke koje se ne smiju duplirati** — `KorpicaPodobnost` (jedini pojam „smije u
korpicu"), `SnapshotKorpice` (kopiranje sadržaja pri nastanku narudžbe; zovu ga oba mjesta koja
stvarno prave `Order`), `SedmicaHelper` (sve o sedmicama i rokovima), `lib/zone.ts`
(`pogodiZonu` — jedino pogađanje zone po imenu naselja; zovu ga checkout i forme adresara i
pretplate), `PretplatnaCijena` (jedino mjesto sa pravilom popusta; zovu ga ekran, javni
`KorpicaDto` i `CreateOrderHandler`), `BrojacIsporuka` (jedino mjesto koje diže `BrojIsporuka`;
zovu ga oba mjesta koja postavljaju `Delivered`), `lib/sedmica.ts` (P4 — imena dana u dva padeža,
`rokTekst`, `isporukaTekst`, `aktivniKrug`; zovu ga traka roka, odbrojavanje, FAQ i footer),
`lib/cesta-pitanja.ts` (P4 — pitanja i jedino izvođenje brojeva za njih), `lib/kontakt.ts` (P5 —
telefon i adresa; footer i `/o-nama`), `ReceptPravila.SmijeUPrikaz` (P5 — jedino pravilo „sastojak
smije nuditi proizvod"), `components/analize/analiza-kartica.tsx` (P5 — `/analize` i stranica domaćina),
`lib/pismo/preslovi.ts` + `Cirilica.cs` (P7 — jedina pravila preslovljavanja; ugovor je
`docs/pismo-vektori.json`), `PismoPoruke` (P7 — jedino mjesto gdje mejl i Viber prelaze na pismo).

## Nije urađeno

**P6 je podijeljen na tri dijela** jer dva diraju iznos narudžbe: **P6a ocjena isporuke** (gotova
na grani `p6a-ocjena`, vidi dolje), **P6b promo kodovi** (sljedeći — uvodi popust na narudžbi),
**P6c poeni i referral** (troši poene kao popust, zavisi od P6b). Poslije toga **P7** višejezičnost.

### P6a — Ocjena isporuke (spojena u main 2026-10-15)

Kupac ocijeni isporučenu narudžbu jednom (1–5 zvjezdica, komentar, dozvola za objavu): sa nalogom iz
„Mojih narudžbi" (`POST /orders/{id}/ocjena`), gost sa stranice praćenja
(`POST /orders/track/{token}/ocjena`, politika `auth`). `OrderDto` nosi `ocjena?` i
`mozeSeOcijeniti`. Admin `/admin/ocjene` (`GET /admin/ocjene?ocjena=`): prosjek, broj, raspodjela,
filter, lista i „Napravi utisak" (`POST /admin/ocjene/{id}/utisak`) koji pravi **neobjavljen** utisak
„Ime P." — samo uz dozvolu i komentar od bar 10 znakova. Entitet `OcjenaIsporuke`, `Utisak.OcjenaId`,
migracija `DodajOcjeneIsporuke` (primijenjena na dev bazu).

Provjere: `dotnet test` 285/285, `tsc`, `lint`, `npm run build` 65/65. Uživo: ocjena kroz UI stranice
praćenja kao gost, druga ocjena 409, admin sažetak, utisak „Marko M." (Centar, neobjavljen), javni
`/utisci` prazan. U dev bazi je ostala **Mujina ocjena (4/5) na `EP-2610-0053`**, kao primjer za admin
ekran; utisak iz nje je obrisan.

Odloženo iz recenzija: obraćanje „ti/vi" se razlikuje između stranice praćenja i „Mojih narudžbi"
(postojeće stranice već jesu različite — treba jedan prolaz kroz cijeli sajt); zahvala „Hvala na
ocjeni!" se ne vidi jer refetch odmah prikaže „Tvoja ocjena"; admin putanje `Order.ToDto` ne učitavaju
`Ocjena` (polje tamo nije u upotrebi).

### P6b — Promo kodovi (spojena u main 2026-10-15)

Admin pravi kodove na `/admin/promo-kodovi` (procenat 1–90 ili iznos, važi od/do po datumu u Sarajevu,
minimum robe, ukupni limit, „jednom po kupcu", „samo prva narudžba", uključen/isključen; kod se ne
mijenja, briše se samo nekorišćen). Kupac i gost upisuju kod u koraku 3 checkouta; popust računa server
(`POST /promo-kodovi/provjera`, politika `auth`). Narudžba pamti `PromoKodId`/`PromoKod`/`Popust`,
`TotalAmount = Subtotal − Popust + DeliveryFee`; red „Popust (KOD)" je na potvrdi, praćenju, admin
kartici, u mejlu, mejlu gosta i Viberu. Gost: kod se ponovo provjerava pri potvrdi iz mejla (409 i
brisanje zahtjeva ako više ne važi). Migracija `DodajPromoKodove` (**nije primijenjena na dev bazu** —
primjenjuje je API pri startu ili `dotnet ef database update`). Demo seed: `DOBRODOSLI` (10 %, samo nova
narudžba).

**Bezbjednosna popravka u P6b:** `POST /orders` je vezivao komandu direktno iz tijela, pa je prijavljeni
kupac mogao poslati `customerId` (narudžba na tuđem nalogu) ili `pretplataId` (pretplatna cijena). Sada
prima `CreateOrderRequest` bez ta dva polja. Zatvorena na `main` i `origin` spajanjem 2026-10-15.

Provjere: `dotnet test` 352/352, `tsc`, `lint`, `npm run build`. **Ručna provjera kroz UI nije rađena**
(API i dev server nisu dizani zbog memorije).

Odloženo: limit koda se može preći za jedan pri istovremenim narudžbama (O10); javna provjera koda otkriva
da li je e-mail ranije naručivao (isto otkriva i zahtjev gosta; pod limitom); pregled na checkoutu i
narudžba računaju subtotal svaki u svojoj petlji.

### P6c — Poeni i preporuke (spojena u main 2026-10-15)

Kupac sa nalogom (uloga `Customer`) dobija 1 poen po cijeloj KM plaćene robe kad narudžba pređe u
`Delivered` (admin ili vozač); 20 poena = 1 KM. Na checkoutu (korak 3) checkbox „Iskoristi poene"
troši najveći cijeli broj KM koji poeni pokrivaju, poslije promo popusta, nikad na dostavu; otkaz
vraća potrošeno. Knjiga `PoeniStavka` (stanje = zbir). Lični kod preporuke (8 znakova, pravi se pri
prvom otvaranju `/moj-nalog/poeni`), link `/register?preporuka=KOD`; kad preporučeni primi prvu
isporuku, oboje dobiju po 100 poena (baza indeksima čuva jednu nagradu po preporučenom). Narudžbe koje
admin/vozač unesu sa svog naloga ne zarađuju i ne troše poene. Red „Poeni (N)" na svim prikazima,
mejlu i Viberu. Migracija `DodajPoene` (nije primijenjena — API je primjenjuje pri startu).

Provjere: `dotnet test` 395/395, `tsc`, `lint`, `npm run build`. **Ručna provjera kroz UI nije rađena.**

Odloženo: dvije istovremene narudžbe istog kupca mogu potrošiti iste poene (O8); istovremena dvostruka
promjena statusa iste narudžbe drugom zahtjevu vraća 500 (jedinstveni indeks — bolje nego dupli poeni);
nema admin ekrana za korekciju poena ni isteka poena; preporuka ne radi kroz Google/Facebook prijavu.

### Živa provjera P6b/P6c i popravka checkouta (2026-10-15, grana `fix-checkout-korak3`)

Prvi put pokrenuto uživo. Obje migracije (`DodajPromoKodove`, `DodajPoene`) su sada **primijenjene** na dev
bazu (API pri startu).

**API, 17/17:** kod se normalizuje (` smokeX ` → `SMOKEX`), provjera 200 / nepostojeći 4xx, popust 10 %,
`total = sub − popust − poeni + dostava`, „jednom po kupcu" odbija drugu narudžbu, `customerId`/`pretplataId`
iz tijela se ignorišu, isporuka daje `floor(roba)` poena i ponovni `Delivered` ne daje duplo, trošenje u
koracima od 20, poeni ne pokrivaju dostavu, otkaz vraća poene, korišćen kod se ne briše (409).

**Nađena i popravljena greška od prvog commita (avgust):** klik mišem na „Nastavi" u koraku 2 checkouta
**slao je narudžbu** — korak 3 se nikad nije vidio. React je zadržavao isti `<button>` i usred klika mu
mijenjao `type` u `submit`. Do P6b je to bilo tiho (korak 3 je bio samo pregled); od P6b su na koraku 3
promo kod i poeni, pa ih kupac mišem nije mogao ni vidjeti. Programski `.click()` grešku ne pokazuje — samo
pravi klik. Popravka: različiti `key` na dva dugmeta. Provjereno pravim klikom (0 slanja, korak 3 se vidi),
pa narudžba kroz UI sa kodom i poenima: 40 − 5 − 1 + 3 = 37 KM, red „Popust (UITEST5)" i „Poeni (20)" na
potvrdi. Uz to: traka primijenjenog koda prikazuje normalizovan kod, ne onaj otkucan malim slovima.

**Demo korisnici preimenovani** (na zahtjev korisnika): Mujo Mujić → **Marko Marković**
(`marko@example.com`/`marko123`), Fata Hadžić → **Jelena Petrović** (`jelena@`/`jelena123`), Haso Begić →
**Dragan Stojanović** (`dragan@`/`dragan123`). Promijenjeno u seedu, mock store-u, napomeni na prijavi,
testovima, README, CLAUDE.md i u dev bazi (korisnici sa novim heševima lozinki, imena i mejlovi na
narudžbama). Istorijski planovi/specovi i `.superpowers/` zapisi namjerno nisu dirani. Jedan JSON uzorak u
testu vozačke aplikacije (`EPiljaraVozac`, „Haso Begić") nije mijenjan jer je taj repo na tuđoj radnoj grani
`dizajn-po-mockupu`.

Provjere: `dotnet test` 395/395, `tsc`, `lint`, `npm run build` ✓.

### P7 — Ćirilica (spojena u main 2026-10-15)

Spec `specs/2026-10-15-p7-cirilica-design.md`, plan `plans/2026-10-15-p7-cirilica.md`. Korisnik je
izabrao **latinicu + ćirilicu** (bez engleskog) za **kupčev dio i mejlove**; admin ostaje latinica.

Ćirilica nastaje **preslovljavanjem izlaza**: u pregledaču nad DOM-om `(shop)` layouta poslije
hidratacije (`PismoProvider` → `lib/pismo/dom.ts`), na serveru nad gotovim HTML-om mejla
(`PismoPoruke` → `Cirilica.UCirilicuHtml`). Prekidač „Ћирилица"/„Latinica" je u navigaciji i
podnožju; pismo je u kolačiću `pismo`, a do API-ja ide zaglavljem `X-Pismo: cir`. Novo:
`PUT /account/pismo` (`{ pismo: "Cirilica" }`, vraća `AuthUserDto` koji sad nosi `pismo`),
`Pismo` na `User`/`Order`/`GuestOrderRequest`, migracija `DodajPismo` (**primijenjena** na dev bazu),
`ICurrentUser.Pismo`, font Onest za ćirilicu (DM Sans je nema), `npm test` (`node --test`).

Provjere: `dotnet test` 472/472, `npm test` 75/75, `tsc`, `lint`, `npm run build`. Uživo, na ćirilici:
21 kupčeva stranica skenirana skriptom — latinicom ostaju samo „E piljara" i brojevi narudžbi; checkout
do kraja (`EP-2610-0059`, `Order.Pismo = Cirilica`), mejl u Mailpitu na ćirilici sa netaknutim linkom,
mejl gosta na ćirilici, prekidač u oba smjera (i `User.Pismo` u bazi), admin latinica i pri punom
učitavanju i pri klijentskoj navigaciji iz prodavnice, 375 px bez horizontalnog skrola.

Nađeno i popravljeno uživo: kod sa tačkom na kraju (`EP-2610-0059.`) se preslovljavao; logotip je
bio „Е пиљара"; admin je postajao ćiriličan pri klijentskoj navigaciji iz prodavnice. Uz to je u
`dom.ts` ugrađen osigurač: više od 500 poziva posmatrača u sekundi ga gasi i ostavlja trag u konzoli.

Odloženo / poznato:
- Prijava na novom uređaju ne preuzima pismo naloga (`AuthUserDto.pismo` postoji, klijent ga još ne čita).
- Tekst koji je kupac unio prikazuje se preslovljen (ime, adresa: „Kralja Petra I" → „Краља Петра И").
- Stranice bez sopstvenog `metadata.title` (nalog, checkout) imaju podrazumijevani naslov — postojalo i prije.
- Pretraživači indeksiraju samo latinicu (svjesno, spec).
- Nepostojeća ruta (Nextova podrazumijevana 404, van `(shop)` layouta) je na ćirilici 1,5 s prazna pa se
  pokaže latinicom — provajder se tamo ne montira, pa CSS čeka rezervni izlaz. Rješenje je sopstveni
  `not-found.tsx` pod `(shop)`.
- Na ćirilici svaki zahtjev nosi `X-Pismo`, pa na produkciji sa API-jem na drugom domenu dobija CORS
  preflight. CORS ga dozvoljava (`AllowAnyHeader`); cijena je jedan dodatni zahtjev po pozivu.
- Snimci ekrana preko Chrome ekstenzije su tokom provjere padali (renderer „ne odgovara"), ali stranice
  rade — provjera je rađena skriptama; to je osobina alata, ne aplikacije (provjereno i na latinici).

## Otvoreni nalazi, nošeni dalje

Ništa od ovoga ne blokira ništa; sve je svjesno odloženo uz razlog.

| Nalaz | Gdje | Zašto je odloženo |
|---|---|---|
| Pretplata nema admin **ekran**, samo endpoint | `/admin/pretplate` | Podatak postoji i vraća i brojač „koliko čeka sadržaj", ali ga nijedna admin stranica ne prikazuje. Nabavka ga za sad čita kroz API. Ekran je mali posao, ali je izvan P3 opsega koji je bio kupčeva strana. |
| Pretplatnik ne dobija obavijest kad mu sedmica bude preskočena | `PretplatneNarudzbe` | Kad sadržaj nije objavljen, posao preskoči sedmicu i zapiše u log — kupac to vidi tek po izostanku isporuke. Mejl kupcu i alarm adminu su svjesno ostavljeni za kasnije (rizik R2 u specu); `cekajuSadrzaj` na admin endpointu je zamjena dok ne postoje. |
| `BrojIsporuka` je izveden podatak koji se može raziću sa narudžbama | `Pretplata`, `BrojacIsporuka` | Ako neko ručno mijenja statuse narudžbi, brojač i stvarni broj isporučenih mogu se razići. Mjerodavan je upit nad narudžbama; nije pravljen okidač u bazi jer logike u bazi nema nigdje u projektu. |
| Sedmica pretplate ne gleda njen `DeliverySlot` | `PretplatneNarudzbe`, `SedmicaHelper` | Ciljna sedmica se računa iz slota sa najnižim `DayOfWeek`, isto kao u checkoutu. Dok su svi slotovi u istoj sedmici (četvrtak i petak), razlike nema. Isti je nalaz već zaveden za `CreateOrder` (vidi „Pet sitnica" ispod) i treba ih riješiti zajedno, ne odvojeno. |
| Adrese sačuvane prije P2c nemaju tačku | postojeći redovi u `Addresses` | Birač postoji od P2c, ali ranije sačuvane adrese (i one prenesene iz starog `User.Address`) i dalje stoje bez pina. Kupac to vidi kao prigušenu napomenu na kartici i zatvara sam kroz „Izmijeni"; batch-geokodiranje adresara, kakvo narudžbe imaju (`admin/orders/geocode-missing`), nije pravljeno. |
| Niko ne provjerava da pin i tekst adrese govore o istom mjestu | `address-picker.tsx`, `AddressRules` | Kad reverzno geokodiranje ne uspije, tekst ostaje kako je otkucan a pin tamo gdje je kliknut; birač na to i upozorava rečenicom ispod mape. Server provjerava samo da je tačka u granicama BiH. Isto važi i na checkoutu, nije uvedeno u P2c. |
| Razlika broj-narudžbi naspram broj-redova nije testirana | `AdminKorpice.cs`, `BrojNarudzbi` | Pokrivanje traži drugu korpicu u dijeljenom test helperu dok su dva taska gradila na njemu. Završna recenzija je potvrdila da je odluka branjiva ali da sam rizik potcijenio — baš taj nedostajući fixture je ostavio neispitanim da editor ne provjerava je li red korpica. |
| Klijent predviđa količinu 1 za ponovo dodat šablonski artikal | `korpica-editor.tsx` | Zatvaranje traži da `PonudaArtiklaDto` nosi šablonsku količinu — izmjena ugovora backenda. Server ostaje mjerodavan; posljedica je samo da traka budžeta pokaže manje nego server. |
| Razlika na pola feninga između JS i .NET zaokruživanja | `korpica-editor.tsx` | JS zaokružuje od nule, .NET na parno. Zabilježeno u komentaru, server mjerodavan. |
| Vremenska zona na `DeliverySlot` | `SedmicaHelper` | **Naslijeđeno iz P1.** `TimeFrom` je „16:00" bez zone, backend ga gradi kao UTC, pa se rok zatvara dva sata kasnije od nominalnog. Prihvaćeno, zapisano u `CLAUDE.md`. |
| `/korpice` izlog govori o tekućoj sedmici, a checkout prodaje za narednu | `GetTekucaSedmicaHandler`, `rok-traka.tsx` | Poslije isteka roka izlog piše „rok istekao" dok naplata ide za sljedeću sedmicu. Naplata je tačna; izlog zaostaje. **Od P4 je razlika vidljivija:** početna i footer poslije roka već govore o narednoj sedmici (`aktivniKrug`), traka na `/korpice` još o tekućoj. Popravka je da i izlog pređe na `aktivniKrug`, ali to mijenja i kad se gasi dugme „Naruči korpicu" — zato nije u P4 (spec O7). |
| Hidratacijska greška u traci kategorija | `category-rail.tsx` | U dev konzoli: Suspense fallback renderuje `<div>`, komponenta `<nav>`. Postojala je i prije P4 (fajl nije diran); zaseban mali zadatak. |
| Utisak nije vezan za kupca | `Utisak` | Ne može se dokazati da je utisak od stvarnog kupca — unosi ga admin. Veza dolazi sa ocjenom isporuke u P6, kao nullable kolona. |
| Admin forme bez `label`/`htmlFor`, `role="dialog"` i Escape | `utisak-form.tsx`, `producer-form.tsx` i ostale | Nova forma utisaka prati postojeći obrazac; pristupačnost treba popraviti za sve admin forme odjednom. |
| Rok u pregledaču se prikazuje u zoni pregledača | `lib/sedmica.ts` | Isti naslijeđeni problem kao „Vremenska zona na `DeliverySlot`" — rok 16:00 (UTC na serveru) se kupcu u BiH prikazuje kao 18h. Dosljedno je sa trenutkom kad se rok stvarno zatvara, ali ne sa nominalnim. |
| Sitnice iz recenzija P4 | razno | Test granice godine ide na `SedmicaHelper`, ne kroz handler; update test utiska ne provjerava `Mjesto`/`Tekst`; nema testa za obrisan artikal u `Artikli`; mock vraća prazne `artikli`; red u footeru se pojavi tek kad stigne podatak. |
| Četiri slaba testa | `SedmicaZaNarudzbuTests`, `CreateOrderSedmicaTests`, `AdminKorpiceTests` | Jedan dokazuje samo da funkcija ne puca; jedan je djelimično cirkularan; jedan ima suvišan assert; jedan je tautološki. Nijedan nije pogrešan, samo dokazuju manje nego što im ime obećava. |
| Pet sitnica iz završne recenzije P2b | razno | Sedmica narudžbe ne gleda izabrani `DeliverySlot` iako seed nosi dva; mrtva `undefined` grana u praznom stanju admina; zastarjeli komentari. |

## Kako se radilo (da se ne mijenja usred posla)

Spec → plan → izvršavanje po taskovima sa svježim subagentom i recenzijom po tasku, pa završna
recenzija cijele grane, pa merge. Korisnik je dvaput tražio manje zaustavljanja: odluke se
donose same i zapisuju kao ruling sa cijenom ako su pogrešne, a pita se samo nepovratno —
merge, push, brisanje baze.

Radni zapisi stoje u `.superpowers/sdd/<ime-plana>/progress.md` (gitignorisano). Tamo je za
svaki potprojekat spisak rulinga, izmjerenih činjenica sa živog sistema i nalaza recenzija.
Nisu obrisani namjerno: to je jedino mjesto gdje piše šta je odlučeno umjesto korisnika.

## Okruženje

- Postgres u Dockeru: `docker exec epiljara-postgres psql -U epiljara -d epiljara`
- API na `:5080`, Next dev na `:3000`
- `dotnet ef` ne radi sa `--startup-project src/EPiljara.API` — živi API drži Debug DLL-ove
  zaključane, a `Infrastructure` pinuje `EFCore.Design` sa `PrivateAssets=all`. Radi preko
  `AppDbContextFactory`: oba projekta na `src/EPiljara.Infrastructure`, `--configuration Release`.
- `npm run build` dijeli `.next` sa dev serverom. Build sa upaljenim dev serverom ostavlja
  stanje u kojem svaka ruta vraća 500. Ugasiti server, obrisati `.next`, pa build.
- Seed lozinke: `admin@epiljara.ba`/`admin123`, `marko@example.com`/`marko123`, `vozac@epiljara.ba`/`vozac123`.

## Podaci koje sam ostavio u dev bazi

Sedmica 42/2026 je generisana i objavljena za „Malu domaćinsku korpicu", i na njoj stoji
narudžba `EP-2610-0052` (Marko) čiji sam sadržaj mijenjao kroz ekran dok sam ga provjeravao.
Rok za izmjenu te korpice je bio `2026-10-13 16:00`, pa je vjerovatno ispao iz prozora za
uređivanje — to je očekivano, ne kvar. Ako treba svježa korpica za ručnu provjeru,
generisati i objaviti novu sedmicu kroz admin endpointe.

Iz P2c je ostala Mujina adresa „Kuća" (Kralja Petra I Karađorđevića 93), sa pinom
postavljenim klikom na mapu — jedini red u `Addresses`. Ostavljena je namjerno: to je jedina
adresa sa tačkom u bazi, pa je zgodna i za provjeru rute i kao polazna adresa za checkout.
Geokodiranje je i dalje mrtvo lokalno (`GET /geocode` vraća `{}`, izlazni soket 10013), pa se
pin u ručnoj provjeri postavlja klikom na mapu, a ne kucanjem adrese.

Iz P3 su ostali: **sedmica 43/2026 generisana i objavljena** za „Malu domaćinsku korpicu",
**jedna aktivna pretplata** Mujina na tu korpicu, i **narudžba `EP-2610-0053`** koju je napravio
sam posao (pa sam je kroz admin prošetao do `Delivered`, da se vidi da brojač ide na 1).
Pretplata je zato na „još 4 isporuke do niže cijene". Ako treba ispočetka: obrisati red iz
`Pretplate` i narudžbu, pa napraviti novu pretplatu kroz ekran.

**Da bi se posao vidio uživo** treba mu proširiti prozor — podrazumijevano pravi narudžbe tek
48 sati prije roka, a to je obično nekoliko dana unaprijed. Privremeno u
`appsettings.Development.json`:
`"Pretplate": { "SatiUnaprijed": 10000 }`, pa restart API-ja (prvi prolaz ide odmah po dizanju).
**Vratiti poslije** — ostavljeno podešeno znači da narudžbe nastaju mjesecima unaprijed.

Iz P4: migracija `DodajUtiske` je **primijenjena** na dev bazu; tabela `Utisci` je prazna (privremeni
utisak za provjeru je obrisan). `DeliverySlot` za četvrtak je tokom provjere odbrojavanja
privremeno mijenjan i **vraćen** na `16:00` / `CutoffHours = 48`.

**P4 sesija je zatekla sve ugašeno** (Docker nije radio). Na kraju su ostavljeni: Docker i
`epiljara-postgres` upaljeni, **API upaljen** (`dotnet run`), **Next dev server ugašen** (zbog
`npm run build`) — dići ga sa `npm run dev` u `frontend/`.

Prije P4 je za predaju pisalo — **Dev server i API su ostavljeni upaljeni**, isti kakvi su i zatečeni: `npm run dev` i
`dotnet run --project src/EPiljara.API` u zasebnim prozorima. Oba su usput morala biti ugašena —
dev server zbog `npm run build`, API zbog toga što drži Debug DLL-ove zaključane dok traje
`dotnet build` i `dotnet ef`.

Iz žive provjere P6 (2026-10-15): narudžba **`EP-2610-0054`** (Marko, kod `SMOKE39006`, `Delivered`) daje Marku
**36 poena** — ostavljena kao primjer za `/moj-nalog/poeni` i admin promo kodove. `EP-2610-0055`–`0058` su
test narudžbe, sve **otkazane**. Kodovi `SMOKE39006` i `UITEST5` su korišćeni pa se ne mogu obrisati (stoje
aktivni; isključiti na `/admin/promo-kodovi` ako smetaju). API i Next dev server su ostavljeni **upaljeni**.

Iz P7 (2026-10-15): narudžba `EP-2610-0059` (Marko, ćirilica) je test i **otkazana**; zahtjev gosta
`nikola@example.com` (ćirilica) ističe sam za 15 minuta. Markov nalog i kolačić u Chromeu su vraćeni na latinicu.
