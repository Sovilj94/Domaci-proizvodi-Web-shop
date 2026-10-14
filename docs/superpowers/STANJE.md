# Stanje projekta — 2026-10-14 (P4)

Predaja sesije. Šta je urađeno, šta nije, i gdje tačno stati nogom kad se nastavi.

## Gdje je kod

| | |
|---|---|
| Grane | `main` i **`pocetna-p4`** (P4, gotova i recenzirana, **nije spojena** — merge čeka odobrenje). `redizajn-opcija3`, `adresar-mapa` i `pretplate-p3` su spojene i obrisane. |
| HEAD `main` | `092223a` — STANJE poslije merge-a P3 |
| HEAD `pocetna-p4` | vrh grane, 13 commita ispred `main`-a (spec, plan, 7 taskova, popravke recenzija, dokumentacija) |
| Radno stablo | čisto |
| Nespojeno | `pocetna-p4` |
| `main` vs `origin/main` | **main je ispred za 12 commita (P2c i P3) — nije pushovan**, push čeka odobrenje. |
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

## Gotovo, na grani `pocetna-p4` (nije spojeno)

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

Stanje provjera na `pocetna-p4`: **`dotnet test` 215/215**, `tsc` čisto, `lint` čisto,
`npm run build` ✓ 56/56 stranica.

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

**Admin** — `/admin/korpice/sadrzaj`, `/sadrzaj/generisi`, `/sadrzaj/{id}`,
`/sadrzaj/{id}/objavi`, `/admin/korpice/pakerska-lista?godina=&sedmica=`,
`/admin/pretplate?status=` (P3 — koliko je korpica obećano za narednu sedmicu i koliko njih
još čeka sadržaj), `GET/POST /admin/utisci`, `PUT/DELETE /admin/utisci/{id}` (P4, ekran
`/admin/utisci`).

**Entiteti dodati kroz P1–P4** — `SedmicniSadrzaj`, `StavkaSadrzaja`, `KorpicaKategorija`
(P1); `StavkaKorpiceNarudzbe` (P2b); `Pretplata` (P3); `Utisak` (P4, migracija `DodajUtiske`). `Product` je dobio `Vrsta`,
`TipKorpice`, `Velicina`, `MozeUKorpicu`, `PrioritetUKorpici`, `Mjeseci`. `Order` je dobio
`Godina`, `Sedmica` i `PretplataId`.

**Ekrani kupca** (adresar od P2c nosi mapu u formi) — `/korpice`, `/korpice/[slug]` (od P3 ima
i „Pretplati se"), `/moj-nalog` i pod njim `/podaci`, `/adrese`, `/narudzbe`, `/korpica`,
`/pretplate`. `/orders` je serverski redirect na `/moj-nalog/narudzbe`. U navigaciji naloga
više nema zaključanih stavki.

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
`lib/cesta-pitanja.ts` (P4 — pitanja i jedino izvođenje brojeva za njih).

## Nije urađeno

**Prvo: merge `pocetna-p4` u `main`** (čeka odobrenje), pa **P5** — Analize, Recepti, O nama,
Postani partner. Zasebna stranica proizvođača (`/proizvodjaci/[id]`) je iz P4 prebačena u P5.

**P5–P7** nisu dirani: Analize/Recepti/O nama/Postani partner; poeni, referral, promo kodovi,
ocjena isporuke (iz nje će u P6 utisak moći nastati sam); višejezičnost.

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
- Seed lozinke: `admin@epiljara.ba`/`admin123`, `mujo@example.com`/`mujo123`.

## Podaci koje sam ostavio u dev bazi

Sedmica 42/2026 je generisana i objavljena za „Malu domaćinsku korpicu", i na njoj stoji
narudžba `EP-2610-0052` (Mujo) čiji sam sadržaj mijenjao kroz ekran dok sam ga provjeravao.
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
