# Stanje projekta — 2026-10-13

Predaja sesije. Šta je urađeno, šta nije, i gdje tačno stati nogom kad se nastavi.

## Gdje je kod

| | |
|---|---|
| Grana | `main` i samo ona. `redizajn-opcija3` i `adresar-mapa` su spojene i obrisane. |
| HEAD | `c731eba` — merge P2c (birač adrese na mapi u adresaru) |
| Radno stablo | čisto |
| Nespojeno | ništa |
| `main` vs `origin/main` | **main je ispred za P2c commit — nije pushovan**, push čeka odobrenje. |
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

Stanje provjera na `main`-u: `tsc` čisto, `lint` čisto, `npm run build` ✓ 54/54 stranica.
**`dotnet test` nije ponovo pokrenut uz P2c** — nijedan fajl backenda nije dirnut; posljednji
izmjereni rezultat je 151/151 na `01ea0f4`.

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
```

**Javno** — `GET /korpice`, `/korpice/sedmica`, `/korpice/{slug}`, `/korpice/{slug}/sadrzaj`

**Admin** — `/admin/korpice/sadrzaj`, `/sadrzaj/generisi`, `/sadrzaj/{id}`,
`/sadrzaj/{id}/objavi`, `/admin/korpice/pakerska-lista?godina=&sedmica=`

**Entiteti dodati kroz P1–P2b** — `SedmicniSadrzaj`, `StavkaSadrzaja`, `KorpicaKategorija`
(P1); `StavkaKorpiceNarudzbe` (P2b). `Product` je dobio `Vrsta`, `TipKorpice`, `Velicina`,
`MozeUKorpicu`, `PrioritetUKorpici`, `Mjeseci`. `Order` je dobio `Godina` i `Sedmica`.

**Ekrani kupca** (adresar od P2c nosi mapu u formi) — `/korpice`, `/korpice/[slug]`,
`/moj-nalog` i pod njim `/podaci`,
`/adrese`, `/narudzbe`, `/korpica`. `/orders` je serverski redirect na `/moj-nalog/narudzbe`.

**Pozadinski poslovi** — `ZakljucavanjeSadrzajaService` (svakih 15 minuta zaključava sadržaje
kojima je rok prošao), `ExpiredGuestRequestsCleaner`.

**Zajedničke tačke koje se ne smiju duplirati** — `KorpicaPodobnost` (jedini pojam „smije u
korpicu"), `SnapshotKorpice` (kopiranje sadržaja pri nastanku narudžbe; zovu ga oba mjesta koja
stvarno prave `Order`), `SedmicaHelper` (sve o sedmicama i rokovima), `lib/zone.ts`
(`pogodiZonu` — jedino pogađanje zone po imenu naselja; zovu ga checkout i forma adresara).

## Nije urađeno

**Sljedeće na redu je P3**, jer je ono što je u prethodnoj predaji stajalo kao „prva stavka
nastavka" (birač na mapi) sada gotovo i spojeno.

**P3 — Pretplate.** Tek započeto: pročitao sam pozadinske poslove i to je sve. Nema ni speca
ni plana. Stavka „Pretplate" već stoji zaključana u navigaciji naloga i čeka da se otključa,
isto kao što je „Moja korpica" čekala P2b.

Jedna stvar koju P3 mora riješiti odmah, jer ju je P2b naučio na teži način: pretplata pravi
narudžbe **bez kupca pred ekranom**, a u repou postoje **tri mjesta koja prave `Order`**
(`CreateOrder.cs`, `GuestOrderRequests.cs`, `DbSeeder.cs`). P2b je bio pisan samo protiv prvog
i gostove korpice su ostajale nevidljive pakerskoj listi — našla je to tek završna recenzija
grane. U `CLAUDE.md` stoji uputstvo da se prije svake invarijante na nivou narudžbe pokrene
`grep -rn "new Order {" backend/src --include=*.cs` i svjesno odluči o svakom pogotku. Za P3 to
pitanje ide u spec, ne u recenziju.

**P4–P7** nisu dirani: marketinška početna; Analize/Recepti/O nama/Postani partner; poeni,
referral, promo kodovi, ocjena isporuke; višejezičnost.

## Otvoreni nalazi, nošeni dalje

Ništa od ovoga ne blokira ništa; sve je svjesno odloženo uz razlog.

| Nalaz | Gdje | Zašto je odloženo |
|---|---|---|
| Adrese sačuvane prije P2c nemaju tačku | postojeći redovi u `Addresses` | Birač postoji od P2c, ali ranije sačuvane adrese (i one prenesene iz starog `User.Address`) i dalje stoje bez pina. Kupac to vidi kao prigušenu napomenu na kartici i zatvara sam kroz „Izmijeni"; batch-geokodiranje adresara, kakvo narudžbe imaju (`admin/orders/geocode-missing`), nije pravljeno. |
| Niko ne provjerava da pin i tekst adrese govore o istom mjestu | `address-picker.tsx`, `AddressRules` | Kad reverzno geokodiranje ne uspije, tekst ostaje kako je otkucan a pin tamo gdje je kliknut; birač na to i upozorava rečenicom ispod mape. Server provjerava samo da je tačka u granicama BiH. Isto važi i na checkoutu, nije uvedeno u P2c. |
| Razlika broj-narudžbi naspram broj-redova nije testirana | `AdminKorpice.cs`, `BrojNarudzbi` | Pokrivanje traži drugu korpicu u dijeljenom test helperu dok su dva taska gradila na njemu. Završna recenzija je potvrdila da je odluka branjiva ali da sam rizik potcijenio — baš taj nedostajući fixture je ostavio neispitanim da editor ne provjerava je li red korpica. |
| Klijent predviđa količinu 1 za ponovo dodat šablonski artikal | `korpica-editor.tsx` | Zatvaranje traži da `PonudaArtiklaDto` nosi šablonsku količinu — izmjena ugovora backenda. Server ostaje mjerodavan; posljedica je samo da traka budžeta pokaže manje nego server. |
| Razlika na pola feninga između JS i .NET zaokruživanja | `korpica-editor.tsx` | JS zaokružuje od nule, .NET na parno. Zabilježeno u komentaru, server mjerodavan. |
| Vremenska zona na `DeliverySlot` | `SedmicaHelper` | **Naslijeđeno iz P1.** `TimeFrom` je „16:00" bez zone, backend ga gradi kao UTC, pa se rok zatvara dva sata kasnije od nominalnog. Prihvaćeno, zapisano u `CLAUDE.md`. |
| `/korpice` izlog govori o tekućoj sedmici, a checkout prodaje za narednu | `GetTekucaSedmicaHandler` | Poslije isteka roka izlog piše „rok istekao" dok naplata ide za sljedeću sedmicu. Naplata je tačna; izlog zaostaje. |
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
uređivanje — to je očekivano, ne kvar. Ako P3 traži svježu korpicu za ručnu provjeru,
generisati i objaviti novu sedmicu kroz admin endpointe.

Iz P2c je ostala Mujina adresa „Kuća" (Kralja Petra I Karađorđevića 93), sa pinom
postavljenim klikom na mapu — jedini red u `Addresses`. Ostavljena je namjerno: to je jedina
adresa sa tačkom u bazi, pa je zgodna i za provjeru rute i kao polazna adresa za checkout.
Geokodiranje je i dalje mrtvo lokalno (`GET /geocode` vraća `{}`, izlazni soket 10013), pa se
pin u ručnoj provjeri postavlja klikom na mapu, a ne kucanjem adrese.

**Dev server je ostavljen upaljen** (`npm run dev` u zasebnom prozoru), isti kakav je i
zatečen, zato što je `npm run build` morao da ga ugasi i obriše `.next`.
