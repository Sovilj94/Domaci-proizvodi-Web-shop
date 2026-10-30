# Stanje projekta — 2026-10-30 (sve u `main` i pushovano; test deploy živi)

Predaja sesije. Šta je urađeno, šta nije, i gdje tačno stati nogom kad se nastavi.
Najnoviji posao (28.–30.10.) je u sekciji **„Deploy, slike u bazi, kalendar, engleski i Špajz"** pri kraju
spiska urađenog.

## Gdje je kod

| | |
|---|---|
| Grane | Samo **`main`** — sve task grane su spojene i obrisane (lokalno i na GitHubu). |
| HEAD `main` | `b6496d1` (2026-10-29) — Recepti i Analize skinuti iz menija |
| `main` vs `origin/main` | **isto — pushovano 2026-10-29**, na korisnikov zahtjev. |
| Produkcija (test) | Frontend <https://domaci-proizvodi-web-shop.vercel.app> (Vercel), API <https://epiljara-api.onrender.com> (Render free), baza Neon. **Push na `main` = automatski deploy** oba dijela (`DEPLOY.md`). |
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

### Redizajn po farmit.rs (R1–R4, 2026-10-16) — spojen u main (`be80e43`) i pushovan 2026-10-16

Korisnik: „identičan sajt", iste boje i stilovi, isti mobilni prikaz; prodaju se samo korpice povrća i voća, a sir,
suho meso, ajvar i zimnica se dodaju iz Špajza. Spec `specs/2026-10-16-farmit-izgled-design.md`, snimak uzora
`farmit-snimak-2026-10-16.md`, planovi `plans/2026-10-16-r1-…` i `plans/2026-10-16-r2-r4-…`.

- **R1** dizajn sistem (farmit tokeni, Poppins/Montserrat, pill dugmad), zaglavlje prozirno na početnoj, krem traka
  roka, mobilni meni preko ekrana, footer sa prijavom na novosti (`PrijavaNovosti`, `POST /novosti/prijava`,
  `/admin/novosti`), početna sa deset sekcija, `Product.Boja` + `BojaProizvoda` (migracija `DodajBojuINovosti`).
- **R2** planovi pretplate Nedjeljna/Mjesečna/3 mjeseca (10/15/20 %, od prve isporuke; obaveza 4/13), otkaz tokom
  obaveze samo admin (`/admin/pretplate` — novi ekran), sakrivene domaćinska/mliječna/suhomesnata (migracija
  `DodajPlanPretplate`), `/korpice/{vrsta}` i `/korpice/{vrsta}/{velicina}`, javni sadržaj za sedmicu koja se prodaje
  (zatvara izlog-nalaz P4 O7: dugme se više ne gasi poslije roka — narudžba ide za narednu sedmicu).
- **R3** `/spajz`, nova kartica proizvoda, „Dodaj iz Špajza" na stranici korpice i u korpi.
- **R4** `PageHeader` na listama, 404 unutar prodavnice (`[...nepoznato]` + `not-found.tsx`).

Provjere: `dotnet test` 492/492, `npm test` 75/75, `tsc`, `lint`; `npm run build` ✓ poslije R1 (68/68). Uživo
provjereni: početna (desktop, 390 px, ćirilica), korpice i stranica korpice (desktop, 390 px), Špajz (desktop),
prijava na novosti, planovi i otkaz kroz API, admin novosti i pretplate. **Nije provjereno uživo** (API i dev server
ugašeni zbog memorije): R4 stranice, Špajz na 390 px, korpa sa „Dodaj iz Špajza", build poslije R2–R4.

Dev baza: sadržaj sedmice 42 objavljen za srednju korpicu povrća; test pretplata (Marko, mjesečna) otkazana;
prijava na novosti `nikola@example.com`.

### Završni prolaz kroz ZA-URADITI (2026-10-19) — grana `zavrsetak` (nespojena)

Stavke iz `ZA-URADITI.md` po predloženom redoslijedu, na jednoj grani. Urađene stavke su obrisane
iz `ZA-URADITI.md`.

**Već zatvoreno ranije, samo nije bilo skinuto sa spiska:**
- **A1** admin ekran pretplata — `/admin/pretplate`, nastao u R2.
- **A3** izlog `/korpice` poslije roka — u R2 izlog prikazuje sadržaj sedmice koja se prodaje, a
  `rok-traka.tsx` više ne postoji; sve trake roka idu kroz `aktivniKrug`.
- **A7** sopstvena 404 — `app/(shop)/not-found.tsx` + `[...nepoznato]`, R4.

**Urađeno na `zavrsetak`:**
- **A2** (`755379f`) — pretplatnik čija je prethodna sedmica prošla bez narudžbe dobija mejl
  (`SendPreskocenaIsporukaAsync`, šablon `PreskocenaIsporukaTemplate`), jednom po sedmici
  (`Pretplata.ObavijestPreskocenaGodina/Sedmica`, migracija `DodajObavijestPreskocene`). Ne javlja se
  pauziranoj, otkazanoj ni pretplati nastaloj poslije roka. **Alarm adminu nije rađen** — ostao u
  `ZA-URADITI.md` kao A2b, pa urađen (vidi ispod).
- **A4** (`fe9ba03`) — `SedmicaHelper.Zona` = `Europe/Sarajevo` (rezerva: Windows ime). Tekuća ISO
  sedmica i datum isporuke računaju se po lokalnom kalendaru, a „16:00" termina se u UTC pretvara uz
  ljetno/zimsko računanje. Rok se više ne zatvara dva sata kasnije.
- **A5** — **odluka, ne kvar**: rok se namjerno računa od prvog dana isporuke, ne od izabranog termina,
  jer se sadržaj sedmice zaključava i pakerska lista štampa jednom. Zapisano u komentaru
  `SedmicaHelper.UcitajRokAsync`.
- **A6, A8, A10** (`9c84106`) — pismo naloga se preuzima pri prijavi; klijentske stranice imaju naslov
  kroz `layout.tsx`; „Hvala na ocjeni" je toast.
- **B3** (`cb348e8`) — istovremena promjena statusa iste narudžbe → 409 umjesto 500.
- **B5** (`cb348e8`, `f139bb8`) — kod preporuke prolazi kroz Google/Facebook prijavu; dugmad na registraciji.
- **B1 + B2** (`a1e7b0f`) — `ZakljucavanjeNarudzbe`: u transakciji narudžbe `SELECT … FOR UPDATE` nad
  redom kupca pa nad redom koda (uvijek tim redom, da nema unakrsnog zaključavanja). SQLite u testovima
  to preskače (`PodrzavaZakljucavanje`), pa **istovremenost nije pokrivena testom** — samo Postgres.

- **A2b** — alarm adminu: kad je do roka ostalo `Pretplate:AlarmSatiPrijeRoka` (24) sati, a neka korpica sa
  aktivnim pretplatama nema objavljen sadržaj ciljne sedmice, svi admini dobijaju jedan mejl sa spiskom
  (`SendAlarmSadrzajaAsync`, `AlarmSadrzajaTemplate`). Svaka korpica ulazi u alarm jednom po sedmici
  (`Product.AlarmSadrzajaGodina/Sedmica`, migracija `DodajAlarmSadrzaja`). 4 testa.
- **B7** (`df79cc3`) — kad je promo kod provjeren, pregled checkouta (zbir, ukupno, minimum zone) koristi
  `subtotal` iz `POST /promo-kodovi/provjera` umjesto zbira iz korpe (`cijenaRobe`). Bez koda ostaje zbir korpe.
- **B8** — **odluka, ne kvar**: `Delivered` je završni status (`Order.Transitions`), a EF upisuje apsolutnu
  vrijednost brojača, pa ni dvostruko ni istovremeno označavanje ne može razići `BrojIsporuka` sa
  isporukama. Razilaženje nastaje samo ručnom izmjenom baze; dugme „preračunaj" nije pravljeno.
- **A9** (`df79cc3`) — kupčev dio, poruke API-ja i mejlovi (potvrda e-maila, potvrda narudžbe) obraćaju se sa
  „ti". Rečenice koje bi tražile rod prepisane su bezlično („Prijava je uspjela", „ništa nije izgubljeno").
  **Namjerno na „vi":** stranice za proizvođače (`postani-partner`, `partner-forma`, odlomak na `o-nama`)
  — to su poslovni partneri, ne kupci.
- **E1** (`6e0cf38`) — `components/admin/admin-ladica.tsx`: jedan omotač za devet admin ladica (8 formi +
  editor nacrta) sa `role="dialog"`, `aria-modal`, `aria-labelledby`, Escape, fokusom u ladicu i povratkom
  fokusa. `StopLocationDrawer` (centrirani modal) dobio iste atribute i Escape. `Field` sam dodjeljuje `id`
  polju (`Input`/`Select`/`Textarea`/nativna polja) i veže grešku kroz `aria-describedby`; tri složena
  polja (boja proizvoda, telefon na checkoutu, promo kod) vezana ručno.
- **E2** — zastario: `category-rail.tsx` je uklonjen u redizajnu (R3).
- **B4** — `/admin/poeni` (meni „Poeni"): lista kupaca sa stanjem (pretraga po imenu/e-mailu, do 100,
  najveće stanje prvo), ladica sa cijelom istorijom i ručnom korekcijom. Endpointi `GET /admin/poeni`,
  `GET /admin/poeni/{userId}`, `POST /admin/poeni/{userId}/korekcija` (`Features/Poeni/AdminPoeni.cs`).
  Korekcija je nova stavka `Korekcija` sa `Razlog` i `UnioId` (migracija `DodajKorekcijuPoena`), u
  transakciji sa zaključanim redom kupca (kao B1), i **ne smije spustiti stanje ispod nule**
  (`poeni_ispod_nule`). Razlog vidi i kupac („Korekcija: …", `PoeniOpis` — zajednički opis za kupca i
  admina). 9 testova. **Istek poena nije rađen** — poslovna odluka, ostala u `ZA-URADITI.md` kao B4b.

Provjere poslije ovih stavki (zaključno sa B4): `dotnet test` 514/514, `npm test` 75/75, `tsc`, `lint`, `npm run build` ✓
(rađen u kopiji jer je dev server na :3000 bio upaljen). **Nije provjereno uživo:** admin ladice (API
ugašen, Docker ne odgovara), `/admin/poeni` i korekcija nad Postgresom, mejl alarma (lokalni `dotnet` ne
može van — vidi C3).

**U toku / sljedeće na ovoj grani:** vidi „Prijedlog redoslijeda" u `ZA-URADITI.md`. C3/D2
(vlastiti Nominatim) i D1 (Viber) se preskaču: prvi traži uvoz mape od nekoliko desetina minuta i
memoriju koje ovaj računar nema (F3), drugi traži ugovor sa provajderom.

### Pregled poslije spajanja (2026-10-19) — grana `pregled-popravke`

`zavrsetak` je spojen u `main` (`3a1935e`) i pushovan. Zatim je cijeli sistem pokrenut uživo (API + Postgres +
Next dev) i provjeren:

- **API uživo:** obje nove migracije primijenjene pri startu. Javni, kupčevi, admin i vozački endpointi
  odgovaraju. Prošao je cijeli tok: admin objavi sadržaj sedmice 43 → Marko naruči srednju korpicu povrća
  (`EP-2610-0063`, 38 KM) → Confirmed → Packing → OutForDelivery → Delivered (nedozvoljen prelaz 400) →
  +35 poena → ocjena. Korekcija poena nad Postgresom radi (transakcija + `FOR UPDATE`), kao i odbijanje
  ispod nule i validacija. Rok je tačan po ljetnom i zimskom vremenu (A4).
- **U pregledaču:** `/admin/poeni` (ladica, fokus, Escape, labele), forma utisaka (dijalog, fokus se vraća na
  dugme), korpica, checkout i „Moji poeni" — bez grešaka u konzoli.

**Popravljeno:**
- **Neispravno ili prazno JSON tijelo davalo je 500 na gotovo svim POST/PUT endpointima** (akcija je dobijala
  `null`). Novi globalni `NeispravnoTijeloFilter` vraća 400 `invalid_body`.
- Labela „Ulica" na checkoutu i „Adresa" na admin novoj narudžbi nisu bile vezane za polje birača adrese
  (`AddressChooser` sada prosljeđuje `id`). Sačuvane adrese su `radiogroup`.
- Dva teksta koja su pretpostavljala muški rod („Preporučio si", „je primio prvu isporuku") prepisana bezlično.

**Okruženje (nije kod):** Docker engine je visio (`docker version` bez odgovora), pa API nije mogao do baze.
Restartovan je Docker Desktop. `epiljara-postgres` nema `restart: unless-stopped` i ne diže se sam (vidi
prijedlog u odgovoru). Next dev server je pao („Jest worker … child process exceptions", malo slobodne
memorije), pa je restartovan sa čistim `.next`.

**Dev baza:** sadržaj sedmice 43 objavljen za srednju korpicu povrća; narudžba `EP-2610-0063` (Marko)
isporučena i ocijenjena; Marko ima 71 poen (dvije probne korekcije +15/−15 se poništavaju).

### Admin panel usklađen sa novim sajtom (2026-10-19) — grana `admin-panel` (na `pregled-popravke`)

Zahtjev: „da se sve može unositi kroz admin panel, pratiti, pakovati". Popis je pokazao pet rupa, sve zatvorene:

- **Pakovanje** (`/admin/pakovanje`, `GET /admin/pakovanje`, `Features/Admin/Pakovanje.cs`): sedmica razložena
  po kutiji — sadržaj korpice kakav ga je kupac ostavio (izbor kupca označen), dodaci iz Špajza, napomena,
  termin, iznos pouzećem; filteri za pakovanje/spakovano i po danu; „Spakovano" (Pending se prvo potvrdi) →
  `Packing`, „Predato vozaču" → `OutForDelivery`; štampa (meni se ne štampa). Ima i **zbir Špajz dodataka** —
  pakerska lista sabira samo sadržaj korpica, pa nabavka do sada nije vidjela med, sir, ajvar.
- **Zone i termini** (`/admin/zone`, `admin/zone`, `admin/termini`, `AdminDostava.cs`): zona se dodaje, mijenja
  i gasi (ne briše se); termin se dodaje, mijenja, a briše samo ako ga ne koristi nijedna narudžba ni pretplata
  i nije jedini. Forma upozorava da termin sa najranijim danom određuje rok svima.
- **Kupci** (`/admin/kupci`, `AdminKupci.cs`): pretraga, broj narudžbi, isporučeno u KM, pretplate, poeni,
  zadnja narudžba; detalj sa adresama, pretplatama i narudžbama (link vodi na narudžbu u adminu).
- **Narudžbe**: pretraga (šifra, kupac, telefon, adresa — i kroz `?pretraga=`) i filter po sedmici isporuke.
- **Kontakt i česta pitanja** (`/admin/sadrzaj`, tabela `SadrzajSajta`, migracija `DodajSadrzajSajta`, javno
  `GET /sadrzaj-sajta`): telefon, e-mail, adresa, područje i pitanja sa oznakama `{rok}`, `{isporuka}`,
  `{popustNedjeljna}`, `{popustTriMjeseca}`. Dok admin ne sačuva, sajt prikazuje ugrađene tekstove.
- Meni je grupisan (Prodaja / Ponuda / Dostava / Sadržaj sajta); Korpice, Pakovanje i Narudžbe dijele izbor
  sedmice (`izbor-sedmice.tsx`). Prije toga, na `pregled-popravke`: greške servera se sada prikazuju na statusu
  narudžbe, rutama i dostupnosti proizvoda, a `/admin/pretplate` pokazuje poslato obavještenje o preskočenoj sedmici.

Provjere: `dotnet test` 525/525 (11 novih), `npm test` 77/77, `tsc`, `lint`, `npm run build` ✓. **Uživo:** svi novi
endpointi (i 403 za kupca), pakovanje do „Spakovano" (`EP-2610-0064`), zone i termini (duplikat 409, loš termin
400, zauzet termin 409), kupci i link na narudžbu, kontakt i pitanje izmijenjeni u adminu pa viđeni na početnoj
(zatim vraćeni na ugrađene). Popravljen usput: link za poziv iz admin telefona (`kontaktIz`, test).

**Nalaz u dev podacima:** Marko ima aktivnu pretplatu na „Malu domaćinsku korpicu", koja je od R2 sakrivena —
takva pretplata svake sedmice bude preskočena (A2 mu to javi). Na produkciji provjeriti da nema pretplata na
sakrivene korpice, ili ih ponuditi za prebacivanje na korpicu povrća.

### Nabavna cijena, sastav korpica po marži i zamjene (2026-10-19) — grana `korpice-nabavna` (nespojena)

Zahtjev: „nabavna cijena na proizvodu; sistem sam spaja korpice po sezonama i određuje koliko čega ide; opcije
za zamjenu kad kupac neće neki proizvod". Sezona (`Mjeseci`), prioritet i generator su postojali od P1 — dodato je:

- **Nabavna cijena** (`Product.NabavnaCijena`, null = nije unesena; migracija `NabavnaCijenaIZamjene`): forma
  proizvoda sa maržom uživo, marža u listi proizvoda. Izlazi SAMO kroz admin DTO-e (`AdminProductDto`,
  `AdminSadrzajDto`) — javni katalog je nema (test na tipu + provjereno uživo). Brisanje unesene: `ukloniNabavnuCijenu`.
- **Generator po marži** (`KorpiceOpcije.MinimalnaMarza` = 0.25): nabavna vrijednost sadržaja ≤ cijena korpice ×
  (1 − marža); količina artikla se smanji da stane u nabavni budžet. Unutar prioriteta prednost imaju artikli sa
  boljom maržom (razredi po 10%, unutar razreda nasumično). Artikli bez nabavne se ne ograničavaju nego se navode
  (`BezNabavneCijene`). Upozorenje kad nabavni budžet obori vrijednost ispod 95% cilja.
- **Generiši ponovo** (`Ponovo`): briše NACRT i sastavlja iznova (objavljen se ne dira) — poslije unosa nabavnih cijena.
- **Predložene zamjene** (tabela `ZamjeneSadrzaja`, `BrojZamjena` = 4): artikli van kutije čiji odnos
  nabavna/prodajna ne obara maržu; prvo iz kategorija korpice, pa iz ostale sezonske ponude (povrće u oktobru i
  novembru sve ode u kutiju). Admin ih uređuje u editoru nacrta; objava izbacuje nedostupne.
- **Kupčeva zamjena** (`POST /account/korpice/{id}/zamijeni`, `ZamjenaUKorpici.cs`): kupac klikne ⇄ na artiklu i
  izabere drugi (preporučene prve); server određuje količinu tako da vrijednost ostane kao sa izbačenim artiklom
  (95–100% cilja) i odbija zamjenu koja bi kutiju gurnula preko nabavnog budžeta (`zamjena_nije_dozvoljena`,
  poruka ne pominje maržu). Moguća samo dok nema nesačuvanih izmjena u editoru.
- Admin Korpice: marža, nabavno i broj zamjena po korpici, „Ponovo", „Pregled" objavljenog sadržaja; pakerska
  lista pokazuje trošak nabavke.

Provjere: `dotnet test` 540/540 (15 novih), `node --test lib/marza.test.ts`, `tsc`, `npm run build` ✓. **Uživo:**
nabavne upisane za 33 artikla (55–80% prodajne — **demo vrijednosti u dev bazi**), sedmica 47 generisana sa
maržama 26–51% i po 4 zamjene, sedmica 43 „Mala korpica povrća" **objavljena** (nije imala sadržaj), probna
narudžba `EP-2610-0065` → zamjena Crni luk → 0,9 l pavlake (27,94/28,24 KM) → **otkazana**.

Nalaz u podacima: katalog ima samo 7 artikala povrća (babura sa zalihom 0), pa srednja i velika korpica povrća ne
dostižu minimum ni cilj (upozorenje „premalo artikala"). Do tada nijedan proizvod nije imao unesene mjesece —
sve je važilo cijele godine (malina u novembru).

**Sezonski kalendar** (`docs/sezonski-kalendar.md`): povrće, voće, gljive i namirnice bez sezone za Krajinu, po
mjesecima, uz predložene Mjesece za naše proizvode i spisak šta fali za zimu i proljeće. Mjeseci su upisani u
`DbSeeder` i u dev bazu (13 proizvoda; ostali cijele godine). Posljedica: u 11–3 povrća ima samo krompir, luk,
mrkva i kupus — manje od 5 koliko traži mala korpica, dok se ne dodaju cvekla, praziluk, bijeli luk, celer i ostalo.

### Sezonska ponuda za punjenje korpica (2026-10-21) — grana `korpice-sezonska-ponuda` (nespojena)

Zahtjev: naći šta se kod nas uzgaja po mjesecima i po tome napraviti ponudu za punjenje korpica. Kalendar je
upoređen sa dva izvora (Avaz, Mamin ručak; južno voće izostavljeno). Dodato:

- **49 sezonskih artikala** (`DbSeeder.SezonskaPonuda.cs`): 26 povrća (zimsko/skladišno kod Kovačevića, proljetno i
  ljetno kod nove **Porodice Petković**, Nova Topola) i 23 voća (stare sorte jabuka, kaluđerka, orašasto i suho kod
  Jovanovića; jagodičasto i koštičavo kod novog **Voćnjaka Đurić**, Ivanjska). Svaki ima mjesece, boju, demo nabavnu
  (60% prodajne) i `MozeUKorpicu`; suho voće, orašasto, grah i bijeli luk su dopuna (prioritet 1). Idempotentno po
  slugu — dodaje se i na već seedovanoj demo bazi (samo demo seed, produkcija ga ne dobija). Osnovni katalog u svježem
  seedu dobija demo nabavnu 60%.
- **Generator, drugi prolaz:** kad bazen potroši, a vrijednost je ispod 95% cilja, povećava količine artikala koji su
  već u kutiji (zaliha i nabavni budžet se poštuju). Bez toga velika korpica voća zimi staje na ~48/57,5 KM.
- Tabela „Ponuda za punjenje korpica" u kalendaru: šta je u katalogu za svaki mjesec (zimi 14 povrća / 9–10 voća,
  ljeti do 26 / 17).

Provjere: `dotnet test` 553/553 (`SezonskaPonudaTests`: svih 6 korpica povrća i voća puno u svakom od 12 mjeseci,
uz maržu ≥ 25%, i seed je idempotentan). **Uživo:** dev baza dobila 49 artikala i 2 proizvođača; sedmica 47
regenerisana — 5–10 artikala, 95–100% cilja, marža 30–34%, po 4 zamjene.

### Interaktivniji admin panel (2026-10-21) — grana `admin-interaktivnost` (nespojena)

Zahtjev: „admin panel nije intuitivan ni interaktivan". Urađene prve tri tačke iz prijedloga:

- **Zadaci** (`GET /admin/zadaci`, `AdminZadaci.cs`): narudžbe na čekanju, neobjavljen sadržaj korpica za sedmicu
  koju checkout prodaje, potvrđene za pakovanje, artikli u sezoni bez zalihe, artikli za korpice bez nabavne, nove
  prijave domaćina — svaki sa nivoom (hitno/upozorenje/info), linkom i akcijom; srpska množina (1 / 2–4 / 5+).
  Isti odgovor nosi i brojke za meni.
- **Pregled** (dashboard): zadaci na vrhu kao kartice, „Generiši sada" za korpice bez sadržaja pravo sa kartice;
  statistika ispod; redovi narudžbi i statusi vode na filtrirane narudžbe (`?status=`, `?pretraga=`).
- **Meni**: Pregled iznad svega, pet grupa (Prodaja, Ponuda, Dostava, Kupci, Sadržaj sajta) koje se zatvaraju
  (pamti se u localStorage; grupa sa otvorenom stranicom ostaje otvorena), brojke na Narudžbama, Korpicama,
  Proizvodima i Partnerima, a zbir na zatvorenoj grupi. Jedan upit umjesto dva.
- **Proizvodi**: pretraga (bez kvačica, i po domaćinu), kategorija i filteri sa brojkama (bez zalihe, bez nabavne,
  marža < 25%, u sezoni, skriveni) — sve u URL-u, pa linkovi sa Pregleda otvaraju filtrirano. Cijena, nabavna i
  zaliha se mijenjaju klikom (Enter čuva, Esc odustaje, prazna nabavna = brisanje), sezona je traka od 12 mjeseci
  (tekući uokviren), prekidači za korpice i prodaju. Izmjena je optimistična i vraća se ako server odbije; toast
  ima „Poništi" (toast dobio opcionu akciju).

Provjere: `dotnet test` 556/556 (3 nova), `npm test` 85/85 (5 novih za `lib/admin/proizvodi-tabela`), `tsc`,
eslint, `npm run build` ✓. Uživo: `/admin/zadaci` na dev bazi vraća 4 zadatka (2 narudžbe, 6 korpica za sedmicu 44,
5 za pakovanje, babura bez zalihe).

Sljedeći krug iz prijedloga: kanban narudžbi sa prevlačenjem, Ctrl+K paleta, bočni panel umjesto prelaska na
novu stranicu, vizuelni editor korpice.

### Admin, drugi krug: tabla narudžbi, bočni panel, Ctrl+K (2026-10-21) — grana `admin-kanban-pretraga` (nespojena)

- **Tabla narudžbi** (`narudzbe-tabla.tsx`, `lib/admin/kanban.ts`): podrazumijevani prikaz narudžbi (Lista ostaje, i
  otvara se sama kad link nosi `?status=`; izbor u `?prikaz=`). Kolone Na čekanju → Potvrđeno → Pakovanje → U
  dostavi → Dostavljeno (dostavljene: zadnjih 15). Kartica se prevlači (HTML5 DnD) samo tamo gdje `Order.Transitions`
  dozvoljava — dozvoljena kolona svijetli, ostale se priguše; dugme „→ sljedeći status" na kartici radi isto bez
  miša. Otkazivanje: zona „Spusti ovdje da otkažeš" + potvrda (nepovratno). Neuspješne dostave u crvenoj traci
  iznad. „Sve" na koloni Na čekanju potvrđuje sve odjednom (jedno obavještenje). Promjena je optimistična;
  lista se osvježava na 30 s i javlja novu narudžbu obavještenjem.
- **Bočni panel**: klik na karticu otvara detalje narudžbe (postojeći OrderCard) u AdminLadici, tabla ostaje iza.
- **Ctrl+K / ⌘K / „/"** (`komandna-paleta.tsx`, `lib/admin/paleta.ts`): akcije (nova narudžba, novi proizvod —
  `/admin/products?novi=1`, generisanje korpica, pakerska lista, artikli bez zalihe), sve stranice menija,
  proizvodi (iz keša), narudžbe i kupci (server, od 2 slova). Rangiranje bez kvačica, više riječi. Dugme „Traži…"
  u sidebaru i ikona na telefonu. Kupci primaju `?pretraga=&kupac=` (otvara ladicu kupca).

Provjere: `npm test` 91/91 (6 novih), `tsc`, eslint, `npm run build` ✓. Uživo u pregledaču: tabla, prevlačenje
(DragEvent; kolone se pravilno istaknu/priguše, badge pao 2 → 1), Ctrl+K („marko" → 5 narudžbi + kupac, „cvar" →
Čvarci → filtriran katalog), bočni panel. **Dev baza:** narudžba `EP-2610-0011` je pri provjeri potvrđena.

### Jednostavnije mijenjanje korpica — kupac, admin za kupca, sedmični sadržaj (2026-10-21) — grana `korpice-jednostavno` (nespojena)

Zahtjev: „jednostavnije dodavanje za admina, lakši način da se mijenja sadržaj korpice, i generalno i za
kupce; i za klijenta da bude jednostavno".

- **Kupac** (`korpica-editor.tsx` prepisan): nema više dodaj/izbaci + traka budžeta + „Sačuvaj". Na svakom
  artiklu „Zamijeni" → „Izaberi umjesto mene" (server bira) ili pločica (preporučene prve, „prikaži sve"), uz
  kvačicu „Ubuduće mi ne stavljaj". Svaka zamjena se upiše odmah; „Vrati kako je bilo"; ispod spisak „Ne
  stavljamo ti" sa ✕. Stari `PUT /account/korpice/{id}` ostaje na API-ju, ekran ga više ne koristi.
- **„Ne stavljaj mi"** (entitet `IzbjegnutArtikal`, migracija `IzbjegnutiArtikli`; `GET/PUT/DELETE
  /account/izbjegnuti-artikli[/{productId}]`): primjenjuje se pri SVAKOJ narudžbi korpice prijavljenog kupca
  (`SnapshotKorpice` → `AutoZamjena.PrimijeniIzbjegnuteAsync`, pa i pretplate), pri „izaberi umjesto mene" i
  pri vraćanju. Artikal bez zamjene koja staje ostaje u kutiji (kutija ne pada ispod cilja). Gost nema spisak.
- **`AutoZamjena`**: jedna logika za sve zamjene (količina u 95–100% cilja, zaliha, nabavni budžet).
  Kandidati: osnovni artikli prije dopune (svježe za svježe, ne suhi grah), pa predložene zamjene, pa kategorije
  korpice. `POST /account/korpice/{id}/zamijeni` prima `ubaci: null` i `zapamti`; `POST …/vrati` je novo.
- **Admin, korpica kupca** (`GET/PUT /admin/narudzbe/korpica/{orderItemId}`, `korpica-kupca-editor.tsx`):
  dugme „Uredi sadržaj" uz korpicu u detaljima narudžbe (admin lista narudžbi dobila `jeKorpica`). Slobodne
  količine (bez granica budžeta i roka, do Pakovanja uključivo), vrijednost/cilj i marža uživo, kupčev spisak
  istaknut. Komad mora biti cio.
- **Admin, sedmični sadržaj**: `sadrzaj-nacrt-editor.tsx` koristi isti `KutijaEditor` — +/−, zamjena iste
  vrijednosti (⇄), dodavanje klikom na pločice SEZONSKE ponude (ranije padajuća lista svih 97 artikala, i van
  sezone) u količini koja popuni ono što fali do cilja; kategorije iz kutije prve. Zamjene se dodaju pločicama.

Provjere: `dotnet test` 564/564 (8 novih), `npm test` 96/96 (5 novih za `lib/admin/kutija`), `tsc`, eslint,
`npm run build` ✓. Uživo: migracija primijenjena; skripta (narudžba EP-2610-0066) — izaberi umjesto mene +
zapamti, spisak, vrati sa spiskom, admin pregled/izmjena, čišćenje; pregledač (EP-2610-0067) — kupčev editor
(Blitva → Krastavac 2,8 kg), adminov editor kupčeve korpice. Obje probne narudžbe **otkazane**, spisak prazan.
**Dev baza:** sedmica 44 „Mala korpica povrća" generisana i objavljena. Editor sedmičnog nacrta nije gledan u
pregledaču (isti `KutijaEditor` kao korpica kupca, koja jeste).

### Stranica korpica po uzoru na farmit + dodatne kulture (2026-10-28) — grana `korpica-dodatne-kulture`

- **„Pogledaj sadržaj korpice"**: prozor ima „Trenutno u korpici" pa **„Dodatne kulture"** — ono što „Zamijeni"
  nudi ove sedmice, bez onoga što je već u kutiji (`SadrzajDto.Dodatne`, samo kategorije te vrste korpice +
  predložene zamjene; svježe prije dopune).
- **`/korpice/{vrsta}`** ispod kartica, kao farmit: utisci (postojeći), „Šta je bitno da znaš?", „Šta dobijaš u
  korpici?" (mjesec ‹ ›, veličina, obojene pločice sa okvirnom količinom; `GET /korpice/sezona?tip=`), „U svakom
  trenutku možeš da:" (samo ono što aplikacija radi; otkaz po isteku obaveze) i česta pitanja (postojeća).
- Okvirna količina = porcija generatora (cilj / minimalan broj artikala) — kod nas je ~5 KM po namirnici u svakoj
  veličini; veća korpica nosi više namirnica, pa uz veličinu piše „najmanje N namirnica, vrijednost X KM".
  Jeftine namirnice zato ispadnu u velikim količinama (kupus 5,7 kg) — isto radi i generator.
  *(Zamijenjeno 29.10. — vidi „Korpice u sinku" ispod: sad je 8 kultura u svakoj veličini.)*

### Deploy, slike u bazi, kalendar, engleski i Špajz (2026-10-28 – 2026-10-30) — sve u `main`, pushovano

Sve je spojeno u `main` i pushovano; backend testovi 590, frontend 102, `tsc`/lint/`next build` čisti.

**Test deploy (Vercel + Render + Neon).** `render.yaml` (Blueprint, `branch: main`, `autoDeploy`,
`buildFilter` na `backend/**`), `backend/Dockerfile`, env aliasi (`CORS_ORIGINS`, `RUN_MIGRATIONS` — van
Developmenta migracije samo uz `true`, `SEED_DATA`, `PORT`, frontend `NEXT_PUBLIC_API_URL`), Neon `postgresql://`
URI se prevodi u Npgsql. Vodič `DEPLOY.md`, checklista kao artifact. Render free blokira odlazni SMTP
(25/465/587) i spava poslije ~15 min. Ništa tajno nije u repou.

**SEO.** Metapodaci po stranici (`lib/seo`), `sitemap.xml`, `robots.txt`, JSON-LD (GroceryStore, Product…).
Indeksiranje je **isključeno** dok se ne postave `SEO_INDEX=true` i `NEXT_PUBLIC_SITE_URL` (test sajt je `noindex`).

**Admin iz env-a.** `ADMIN_EMAIL` / `ADMIN_PASSWORD` / `ADMIN_NAME` — pravi admin se pravi ili ažurira na
svakom startu (`AdminNalog.cs`), nezavisno od `SEED_DATA`. Demo `admin@epiljara.ba/admin123` i dalje postoji uz seed.

**Seed uz `SEED_DATA`.** Objavljen sadržaj korpica za tekuću i narednu sedmicu (`SeedSadrzajKorpicaAsync`)
i 5 objavljenih utisaka kad ih nema (`SeedUtisciAsync`).

**Slike u bazi.** Tabela `Slike` (bytea, jedinstven ključ), `GET /slike/{**kljuc}` (keš 7 dana + ETag),
`POST /admin/slike` (JPG/PNG/WEBP do 5 MB, uvijek novi ključ), Next rewrite `/slike/*` → API,
`images.unoptimized`. Početne slike su u `backend/src/EPiljara.Infrastructure/Persistence/SeedSlike/`
(products, categories, producers, korpice, izrezi); `SlikeUvoz` ih na startu upisuje samo ako ih nema.
Migracija `SlikeUBazi` je prepisala stare `/products/...` putanje u `/slike/...`. U admin formama
(proizvod, kategorija, domaćin, recept) je „Učitaj sliku". Izvori i licence: `docs/slike-izvori.md`
(Wikimedia Commons; farmitove fotografije se **ne** koriste — autorsko pravo).

**Izrezi (slike bez pozadine) i fotografije korpica.** `Product.IzrezUrl` + polje u formi proizvoda; 73 izreza
(pozadina skinuta skriptom, CC BY-SA ostaje BY-SA), povezuju se po slugu na startu (`PoveziIzrezeAsync`).
Korpice povrća i voća imaju svoje fotografije (`/slike/korpice/{tip}-{velicina}.jpg`). Bez slike su još:
čičoka, crna rotkva, rukola, pak čoj, zelena i žuta šilja, kineski kupus; 10 novih kultura privremeno koristi
sliku srodne kulture (vidi `slike-izvori.md`).

**Stranica korpica i korpice kao farmit.**
- `/korpice/{vrsta}/{velicina}`: „Ove sedmice…" i „Od dodatnih kultura…" sa domaćinom i selom, utisci,
  ostali paketi, česta pitanja, ljepljiva traka sa cijenom i dugmetom.
- `/korpice/{vrsta}`: ljepljiva traka sa tri korpice; „Rekli su o E piljari" kao Swiper na farmitu (traka
  klizi, aktivna raste, petlja, 3 s, samo cijele fotografije); „Šta dobijaš" pločice 162×162 sa izrezom.
- Kartice planova: „4 dostave do tvojih vrata", ukupna cijena plana („85.00 KM /4 korpice") i sitno cijena po
  korpici · plaćanje pri svakoj dostavi. **3 mjeseca = 12 dostava** (`PretplateOpcije.ObavezaTriMjeseca`).

**Sezonski kalendar.** Mjeseci povrća preslikani sa farmitovog kalendara (činjenični podaci); 32 nove kulture
u `DbSeeder.SezonskaPonuda` (**cijene su okvirne — provjeriti**), migracije `SezonaPovrcaKalendar` i
`SezonaPovrcaIspravka`. Grašak, kiseli kupus, kopriva i sremuš ne idu u korpice. Admin stranica
**`/admin/korpice/kalendar`** (kultura × 12 mjeseci, „U korpici", broj kultura po mjesecu, crveno ispod 9).
Voće nije dirano (farmit nema mjesečnu listu za voće).

**Korpice u sinku.** `KorpiceOpcije.MinimalanBrojArtikala` = **8 za sve veličine** (farmit „8+ kultura");
generator i „Šta dobijaš" dijele vrijednost korpice na isti broj, pa veća korpica nosi veće količine istih
kultura (≈ 1 : 1,4 : 1,8), a prikazana okvirna količina odgovara pakovanju. Kupac koji sam mijenja korpicu
mora zadržati 8 kultura.

**Jezik: Srpski / English umjesto ćirilice.** Prekidač sa SVG zastavicama (meni, mobilni meni, footer), kolačić
`jezik`. Prevod ide nad DOM-om poslije hidratacije (`lib/jezik/dom.ts`, kreće u `requestIdleCallback` da ne
sudari sa Suspense hidratacijom); rječnici `lib/jezik/rjecnik.ts` i `rjecnik-stranice.ts`, obrasci za tekst sa
brojevima u `prevod.ts`. Nazivi proizvoda i kategorija iz baze: `Product.NazivEn`/`OpisEn`,
`Category.NazivEn`, `GET /prevodi`, početni prevodi `PrevodiKataloga.cs` (samo u prazna polja), polja u
admin formama. Admin panel ostaje na srpskom. Ćirilica je skinuta sa sajta; kod za ćirilicu u mejlovima ostaje.

**Špajz kao farmit.** `Product.USpajzu` — pojedinačno se prodaju korpice, jaja, ajvar, džem, med i mliječno;
povrće, voće i **meso** su van Špajza (ostaju za korpice), narudžba ih odbija (`product_unavailable`), a
stranica proizvoda kaže „Ova namirnica stiže u korpicama". Zimnica je podijeljena na kategorije **Ajvar** i
**Džem** (migracija `SpajzKaoFarmit`). Kvačica „Prodaje se pojedinačno u Špajzu" u formi proizvoda.
Recepti u korpu dodaju samo artikle Špajza.

**Sitnice.** Traka roka ispod menija se više ne skuplja (`shrink-0`); dugmad hero karusela na početnoj vode
kud treba (neaktivni slajdovi `pointer-events-none`); Recepti i Analize skinuti iz glavnog menija (stranice
postoje).

### Mapa za adresu dostave po uzoru na farmit (2026-10-30) — grana `mapa-dostave`

Farmit checkout (Google Maps): polje sa prijedlozima → mirna mapa sa šargarepom → „Izmeni lokaciju na mapi"
(pin stoji u centru, mapa se pomjera) → „Potvrdi lokaciju", plus „Koristi moju trenutnu lokaciju" i „Ne možeš
da pronađeš adresu?". Kod nas isto, na OSM + Leaflet, bez ključa (`address-picker.tsx`).

- **Prijedlozi dok se kuca:** `GET /geocode/suggest` → Photon (javni Nominatim zabranjuje autocomplete).
  Samo BiH, Banja Luka prva, ista ulica iz više OSM komada jednom; „Ulica 10" bez kuće u OSM-u dobije
  ulicu sa upisanim brojem. Per-IP limit 60/min.
- **Checkout:** izbor puni „Ulica" i „Kućni broj" posebno (`onBroj`); potvrđena tačka čita ulicu i zonu.
- Tekst koji niko nije izabrao sa liste se geokodira pri izlasku iz polja, kao ranije; admin ekrani
  (nova narudžba, lokacija stanice, magacin) i adresar/pretplata koriste isti birač.
- Testovi: backend 596 (+6 prijedlozi), frontend 114 (+5 `adresa-prijedlog`); `tsc`, lint, `next build` čisti;
  ručno u pregledaču na :3001 + API :5081.

**Samo Banja Luka (isti dan).** Grad dostave je u bazi, `Podesavanja.GradDostave` (migracija `GradDostave`
postavlja „Banja Luka"; admin ga mijenja u „Poslovna pravila → Dostava"). Svaki pogodak geokodera nosi
`uPodrucjuDostave` (grad ili opština, i sela na teritoriji grada prolaze). Prijedlozi iz Banje Luke idu prvi,
ostali su označeni „Ne dostavljamo"; izbor takve adrese (ili tačke na mapi / GPS) daje crveno upozorenje
„Ne dostavljamo na ovu adresu" i blokira „Nastavi" u checkoutu, čuvanje u adresaru i pretplati. Backend
testovi 603 (+7 područje dostave). **Nije provjereno na serveru** pri `CreateOrder` — vidi ZA-URADITI C4.

**Zašto je lokalno pisalo „nema te adrese":** API koji je radio na :5080 vraćao je `{}` i za pretragu i za
reverse, a ista verzija koda pokrenuta svježe na :5081 geokodira normalno (Render takođe). Restart API-ja
na :5080 to rješava; nije greška u kodu (vidi ZA-URADITI C3).

### Narudžbe: koliko korpica može da se potvrdi i roba za sedmicu (2026-10-30) — grana `potvrda-i-roba`

**Korpice na čekanju** (panel na `/admin/orders`, `GET /admin/korpice/potvrdjivanje`): po sedmici isporuke i
korpici „M od N kom“ može da se potvrdi. Slobodno stanje se računa samo naspram potvrđenih korpica (sve osim
na čekanju i otkazanih); narudžbe na čekanju se uzimaju redom kojim su stigle, sa stvarnim sadržajem, i dijele
isto stanje — pa brojevi zajedno važe. Uz manjak stoji artikal kojeg je prvog nestalo. „Preostalo za prodaju“
ostaje kakvo je (broji i narudžbe na čekanju), zato iz njega nije moglo da se pročita koliko smije da se potvrdi.

**Roba za sedmicu** (pakerska lista proširena; sklopivo na `/admin/orders`, i umjesto stare liste na `/admin/korpice`):
po artiklu Korpice / Špajz / Ukupno / Na stanju / Fali, i traka „otvoreno do roka“ ili „zaključeno — količine su
konačne“ (`Rok`, `Zakljucena`). Lista sad sabira i Špajz (red same korpice se ne broji). Fali = korpice − stanje,
jer je Špajz već skinut sa stanja pri narudžbi. Backend testova 607 (+4).

## Otvoreni nalazi, nošeni dalje

> **Radna lista svega što nije urađeno je `docs/superpowers/ZA-URADITI.md`** (sa prioritetima i
> prijedlogom redoslijeda). Tabela ispod je istorijski zapis razloga.

Ništa od ovoga ne blokira ništa; sve je svjesno odloženo uz razlog.

| Nalaz | Gdje | Zašto je odloženo |
|---|---|---|
| `BrojIsporuka` je izveden podatak koji se može raziću sa narudžbama | `Pretplata`, `BrojacIsporuka` | Ako neko ručno mijenja statuse narudžbi, brojač i stvarni broj isporučenih mogu se razići. Mjerodavan je upit nad narudžbama; nije pravljen okidač u bazi jer logike u bazi nema nigdje u projektu. |
| Adrese sačuvane prije P2c nemaju tačku | postojeći redovi u `Addresses` | Birač postoji od P2c, ali ranije sačuvane adrese (i one prenesene iz starog `User.Address`) i dalje stoje bez pina. Kupac to vidi kao prigušenu napomenu na kartici i zatvara sam kroz „Izmijeni"; batch-geokodiranje adresara, kakvo narudžbe imaju (`admin/orders/geocode-missing`), nije pravljeno. |
| Niko ne provjerava da pin i tekst adrese govore o istom mjestu | `address-picker.tsx`, `AddressRules` | Kad reverzno geokodiranje ne uspije, tekst ostaje kako je otkucan a pin tamo gdje je kliknut; birač na to i upozorava rečenicom ispod mape. Server provjerava samo da je tačka u granicama BiH. Isto važi i na checkoutu, nije uvedeno u P2c. |
| Razlika broj-narudžbi naspram broj-redova nije testirana | `AdminKorpice.cs`, `BrojNarudzbi` | Pokrivanje traži drugu korpicu u dijeljenom test helperu dok su dva taska gradila na njemu. Završna recenzija je potvrdila da je odluka branjiva ali da sam rizik potcijenio — baš taj nedostajući fixture je ostavio neispitanim da editor ne provjerava je li red korpica. |
| Klijent predviđa količinu 1 za ponovo dodat šablonski artikal | `korpica-editor.tsx` | Zatvaranje traži da `PonudaArtiklaDto` nosi šablonsku količinu — izmjena ugovora backenda. Server ostaje mjerodavan; posljedica je samo da traka budžeta pokaže manje nego server. |
| Razlika na pola feninga između JS i .NET zaokruživanja | `korpica-editor.tsx` | JS zaokružuje od nule, .NET na parno. Zabilježeno u komentaru, server mjerodavan. |
| Hidratacijska greška u traci kategorija | `category-rail.tsx` | U dev konzoli: Suspense fallback renderuje `<div>`, komponenta `<nav>`. Postojala je i prije P4 (fajl nije diran); zaseban mali zadatak. |
| Utisak nije vezan za kupca | `Utisak` | Ne može se dokazati da je utisak od stvarnog kupca — unosi ga admin. Veza dolazi sa ocjenom isporuke u P6, kao nullable kolona. |
| Admin forme bez `label`/`htmlFor`, `role="dialog"` i Escape | `utisak-form.tsx`, `producer-form.tsx` i ostale | Nova forma utisaka prati postojeći obrazac; pristupačnost treba popraviti za sve admin forme odjednom. |
| Sitnice iz recenzija P4 | razno | Test granice godine ide na `SedmicaHelper`, ne kroz handler; update test utiska ne provjerava `Mjesto`/`Tekst`; nema testa za obrisan artikal u `Artikli`; mock vraća prazne `artikli`; red u footeru se pojavi tek kad stigne podatak. |
| Četiri slaba testa | `SedmicaZaNarudzbuTests`, `CreateOrderSedmicaTests`, `AdminKorpiceTests` | Jedan dokazuje samo da funkcija ne puca; jedan je djelimično cirkularan; jedan ima suvišan assert; jedan je tautološki. Nijedan nije pogrešan, samo dokazuju manje nego što im ime obećava. |
| Sitnice iz završne recenzije P2b | razno | mrtva `undefined` grana u praznom stanju admina; zastarjeli komentari. |

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
- **Od 28.10.:** na `:3000` je korisnikov `next start` (produkcijski build) — ne vidi izmjene ni nove fajlove dok
  se ne uradi `npm run build` i restart. Za ručne provjere je korišten par **API `:5081`** (build u scratch folder:
  `dotnet build src/EPiljara.API -o <scratch>`, jer API na `:5080` drži `bin/Debug` zaključan) i **Next dev
  `:3001`** sa `NEXT_PUBLIC_API_URL=http://localhost:5081` i `CORS_ORIGINS=http://localhost:3001`.
- Svaka izmjena `next.config.ts` (i privremeni `distDir` za build provjeru) restartuje dev server i on gubi
  rewrite `/slike` → poslije build provjere ručno restartovati dev server.
- Claude Code gasi pozadinske servere kad ponestane memorije (vidi F3); ugašeni dev se diže ponovo samo na zahtjev.
- Migracije: `dotnet dotnet-ef migrations add <Ime> -p src/EPiljara.Infrastructure -s src/EPiljara.Infrastructure -o Persistence/Migrations`.

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

Iz 28.–30.10.: na dev bazu su primijenjene sve nove migracije (slike, izrezi, kalendar, prevodi, Špajz);
u `Slike` stoje početne slike i **jedna probna** `products/dunja-dabbe80f.jpg` (test uploada, ništa ne pokazuje
na nju); upisano je 5 demo utisaka i objavljen sadržaj korpica za sedmice 44–45. Kolačić `jezik` u Chromeu je
mijenjan tokom provjere engleskog.
