# Stanje projekta — 2026-10-13

Predaja sesije. Šta je urađeno, šta nije, i gdje tačno stati nogom kad se nastavi.

## Gdje je kod

| | |
|---|---|
| Grana | `main` i samo ona. `redizajn-opcija3` je spojena i obrisana. |
| HEAD | `01ea0f4` |
| Radno stablo | čisto |
| Nespojeno | ništa |
| `main` vs `origin/main` | **164 commita ispred, NIJE pushovano** |
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

Stanje provjera na `main`-u: **`dotnet test` 151/151**, `tsc` čisto, `lint` čisto,
`npm run build` ✓ 54/54 stranica.

## Nije urađeno

**P3 — Pretplate.** Tek započeto: pročitao sam pozadinske poslove i to je sve. Nema ni specа
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
| Birač adrese na mapi | `adresa-forma.tsx`; `components/shop/address-picker.tsx` već postoji sa pravim propovima | Spec P2a ga izričito stavlja izvan opsega. Posljedica: adresar čuva adrese bez pina, pa se ruta i dalje optimizuje po zonama. **Prva stavka nastavka.** |
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
