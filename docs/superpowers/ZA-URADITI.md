# E piljara — šta nije urađeno ili nije dobro urađeno

Ažurirano **2026-10-30** (sve u `main`). Plan P1–P7, redizajn R1–R4 i rad od 28.–30.10. (deploy, slike u bazi,
kalendar, engleski, Špajz) su završeni; ovo je sve što je usput svjesno odloženo, nađeno u recenzijama ili fali do produkcije.
Detalji i istorija su u `STANJE.md`; ovaj fajl je radna lista — **kad se stavka uradi, briše se
odavde** (i upiše u `STANJE.md`).

Veličina: **S** = do sat-dva, **M** = pola dana, **L** = dan i više / treba spec.

---

## A. Prioritet — kupac ili admin to primijeti

| # | Šta | Gdje | Prijedlog | Vel. |
|---|---|---|---|---|

## B. Novac i istovremenost (rijetko, ali dira iznos)

| # | Šta | Gdje | Prijedlog | Vel. |
|---|---|---|---|---|
| B6 | Javna provjera promo koda otkriva da li je e-mail ranije naručivao („samo prva narudžba"). Pod rate limitom. | `POST /promo-kodovi/provjera` | Prihvaćeno; ako smeta — generička poruka „kod ne važi za ovu narudžbu". | S |

## C. Adrese i mapa

| # | Šta | Gdje | Prijedlog | Vel. |
|---|---|---|---|---|
| C1 | **Adrese sačuvane prije P2c nemaju pin** (i one prenesene iz starog `User.Address`). | tabela `Addresses` | Batch geokodiranje kao `admin/orders/geocode-missing`. Treba radno geokodiranje (C3/D2). | S |
| C2 | Niko ne provjerava da **pin i tekst adrese** govore o istom mjestu (kad reverzno geokodiranje ne uspije). | `address-picker.tsx`, `AddressRules` | Upozorenje već postoji; opcija: server odbije tačku dalje od X km od geokodiranog teksta. | M |
| C3 | **Lokalno geokodiranje povremeno ne radi** — dugo pokrenut API (`dotnet run`) zna dobiti socket 10013 za odlazni HTTPS, `/geocode` tada vraća `{}`. 30.10. je svježe pokrenut API (`dotnet EPiljara.API.dll`) normalno dosezao Nominatim i Photon. | okruženje | Prvo restart API-ja; trajno: vlastiti Nominatim kontejner (`backend/nominatim/README.md`, D2). Prijedlozi (Photon) bi tad i dalje izlazili — za njih postoji i Photon docker image. | S |
| C4 | **Grad dostave se provjerava samo u pregledaču** — `CreateOrder` ne odbija narudžbu sa adresom van `GradDostave` (npr. poziv API-ja mimo sajta, ili adresa bez tačke). | `CreateOrderValidator` | Na serveru reverse-geokodirati tačku narudžbe (ili poligon grada u bazi) i odbiti van područja. Zone dostave već ograničavaju admin izborom. | M |

## D. Do produkcije (`PRODUCTION.md`)

| # | Šta | Prijedlog | Vel. |
|---|---|---|---|
| D1 | **Viber je stub** (`LoggingViberService` samo loguje). Kupac bez e-maila ne dobija potvrdu. | Implementirati `IViberService` za BSP (Infobip/Twilio), `Viber__ApiKey`; poslovna odluka + ugovor sa provajderom. | M |
| D2 | **Geokodiranje ide na javni Nominatim** (adrese kupaca idu trećoj strani, 1 zahtjev/s). | Self-host (vidi C3), `Nominatim__BaseUrl`, `ThrottleMs=0`. | M |
| D3 | JWT bez opoziva — nema prisilne odjave. | Po potrebi: lista opozvanih ili kratki token + refresh. | M |
| D4 | Migracije se primjenjuju pri startu API-ja — za više instanci zasebni korak. | Kad bude više instanci. | S |
| D5 | Lokacija magacina i ostatak checkliste u `PRODUCTION.md` (prvi admin je riješen: `ADMIN_EMAIL`/`ADMIN_PASSWORD`). | Proći checklistu pri pravom deployu. | S |
| D7 | **Mejl na test deployu ne radi** — Render free blokira SMTP 25/465/587 (registracija, gost-narudžba). | SMTP provajder na portu 2525 (Brevo, SendGrid…) u `Email__*` (`DEPLOY.md`). | S |
| D8 | SEO je isključen na test sajtu. | Na pravom domenu: `NEXT_PUBLIC_SITE_URL`, `SEO_INDEX=true`, Redeploy, sitemap u Search Console. | S |

## E. Kvalitet koda i testova

| # | Šta | Gdje |
|---|---|---|
| E3 | Četiri slaba testa (jedan samo provjerava da ne puca, jedan djelimično cirkularan, jedan suvišan assert, jedan tautološki). | `SedmicaZaNarudzbuTests`, `CreateOrderSedmicaTests`, `AdminKorpiceTests` |
| E4 | Razlika „broj narudžbi vs. broj redova" na pakerskoj listi nije testirana; editor ne provjerava da je red korpica. | `AdminKorpice.cs`, `BrojNarudzbi` |
| E5 | Klijent predviđa količinu 1 za ponovo dodat šablonski artikal (traka budžeta pokaže manje od servera). | `korpica-editor.tsx`; treba `PonudaArtiklaDto` sa šablonskom količinom |
| E6 | Pola feninga razlike JS/.NET zaokruživanja u editoru korpice (server mjerodavan). | `korpica-editor.tsx` |
| E7 | Sitnice P4: test granice godine ide na `SedmicaHelper` a ne kroz handler; update test utiska ne provjerava `Mjesto`/`Tekst`; nema testa za obrisan artikal u `Artikli`; mock vraća prazne `artikli`; red u footeru se pojavi tek kad stigne podatak. | razno |
| E8 | Sitnice P2b: mrtva `undefined` grana u praznom stanju admina; zastarjeli komentari. | razno |
| E9 | Admin putanje `Order.ToDto` ne učitavaju `Ocjena` (polje tamo nije u upotrebi). | `AdminOrders` |
| E10 | Utisak (P4) se ne može dokazati kao utisak stvarnog kupca kad ga admin unese ručno; samo oni iz ocjene nose `OcjenaId`. | `Utisak` |

## G. Katalog, slike i jezik (otvoreno poslije 28.–30.10.)

| # | Šta | Prijedlog | Vel. |
|---|---|---|---|
| G1 | **Cijene 32 nove kulture** iz sezonskog kalendara su okvirna procjena. | Admin provjeri u Proizvodima. | S |
| G2 | **7 kultura bez slike** (čičoka, crna rotkva, rukola, pak čoj, zelena i žuta šilja, kineski kupus); 10 koristi sliku srodne kulture. | Vlastite fotografije ili kupljeni izrezi, upload u formi proizvoda. | S |
| G3 | **Engleski — sadržaj iz baze**: opisi proizvoda (`OpisEn`), utisci, recepti, analize ostaju na srpskom; mejlovi samo srpski. | Popuniti u adminu; za utiske/recepte trebaju EN polja ako zatreba. | M |
| G4 | Engleski je rječnik nad DOM-om — tekst koji nije u `lib/jezik/rjecnik*.ts` ostaje srpski (bez greške). | Dopunjavati rječnik kad se primijeti. | S |
| G5 | **Meso je van Špajza** (odluka 29.10., farmit ga nema); „Domaći ajvar" je u seedu nedostupan. | Ako treba — u formi proizvoda „Prodaje se pojedinačno u Špajzu" / dostupnost. | S |
| G6 | Generator može malo povećati količine (dopuna do cilja) ili smanjiti (zaliha, marža) u odnosu na prikazane okvirne količine. | Prihvaćeno — količine su „okvirne". | — |
| G7 | Voće nema sezonski kalendar po uzoru (farmit ga nema). | Po potrebi ručno u Sezonskom kalendaru. | S |

## F. Okruženje i repozitorijumi

| # | Šta |
|---|---|
| F1 | `Desktop\Kumov Web Shop` je detached HEAD daleko iza `main` — stara kopija. Ili obrisati ili `git switch main` pa pull, da ne zbunjuje. Radi se u `Desktop\epiljara-backend-wt`. |
| F2 | Vozačka aplikacija (`Desktop\EPiljaraVozac`) je na grani `dizajn-po-mockupu`; u testu `ApiClientTests.cs` ostalo je ime „Haso Begić" (demo imena treba da budu srpska). Promijeniti kad se ta grana završi. |
| F3 | Računar ima malo slobodne memorije (Visual Studio + tsserver zauzimaju ~4 GB) — API i Next dev server su gašeni zbog toga. Za ručne provjere zatvoriti nepotrebno. |
| F4 | Snimci ekrana preko Chrome ekstenzije povremeno padaju („renderer ne odgovara") iako stranica radi — provjere raditi skriptama (`javascript_tool`, `get_page_text`). |

---

## Prijedlog redoslijeda

Urađeno 2026-10-19 na grani `zavrsetak` (vidi `STANJE.md`): A1–A10, A2b, B1–B5, B7, B8, E1, E2 (A1, A3, A7
i E2 su bili zatvoreni već u redizajnu; A5 i B8 su zatvoreni kao odluka).

1. **B4b** (poslovna odluka) — da li poeni ističu; ako da, posao koji upisuje negativnu `Korekcija`/`Istek` stavku.
2. **C3/D2** — vlastiti Nominatim, pa **C1** (traži memoriju i desetine minuta uvoza; vidi F3).
3. **D1** kad postoji ugovor sa Viber provajderom.
