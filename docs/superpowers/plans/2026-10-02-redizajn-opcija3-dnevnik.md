# Redizajn „Opcija 3" — dnevnik rada

**Počeo:** 2026-10-02
**Cilj:** Redizajn web shopa E piljara po screenshotu „E-piljara Grocery UI Showcase complete.png"
(`C:\Users\soviljn\Desktop\`). Screenshot je **vizuelni source of truth** — ne improvizovati drugi stil.

> Ovaj fajl se dopisuje **u toku rada**, da se poslije prekida (potrošen kredit, zatvorena sesija)
> zna dokle se stiglo. Novi unos ide na kraj „Hronologije", a tabela statusa se ažurira u mjestu.

---

## Odluke korisnika (2026-10-02)

| Pitanje | Odluka |
|---|---|
| Wishlist (srce na kartici) | **Dodati pravi backend za omiljene** — nova tabela + endpointi, ne localStorage |
| Moj nalog | **Nalog + Adresar**, na postojećem `/account/addresses` backendu |
| Checkout | **Zadržati zonu i termin dostave**, prestilizovati ih u jezik mockupa |
| Tempo | **Faza po faza**, uz pregled u browseru poslije svake faze |

Pristup: **pretakanje postojećih tokena** (`globals.css`) — mijenjaju se vrijednosti, ne imena,
jer ih koristi 33 fajla. Odobreno od korisnika.

## Paleta (odobrena, sekcija 1)

| Token | Staro | Novo | Kontrast |
|---|---|---|---|
| `cream` | `#f7f2e9` | `#f8f5ed` | pozadina |
| `surface` | `#fffdf8` | `#ffffff` | kartice |
| `sand` | `#efe8da` | `#eef5ee` | svijetlo zelena površina |
| `brand-600` | `#2d5016` | `#0e5b38` | 8.28:1 sa bijelim |
| `brand-700` | `#24400f` | `#083f29` | hover |
| `ink` | `#1a1710` | `#171914` | 16.3:1 na `cream` |
| `muted` | `#5a5244` | `#5f6459` | 5.66:1 na `cream` |
| `line` | `#e2d9c8` | `#e5e1d7` | borderi |
| `radius-card` | `1rem` | `0.875rem` | 14px |

`brand-50…900` se izvodi oko novog 600. `honey-*` ostaje (uvozi ga 33 fajla) ali se svodi na
jedan akcent za badge. Fontovi nepromijenjeni: Playfair + sans.

---

## Zatečeno stanje (izmjereno, ne pretpostavljeno)

**Backend nema** (mockup to traži — ne izmišljati sadržaj):
ocjene/recenzije, nutritivne vrijednosti, popust/„Akcija", kod za popust, polje „brend",
više slika po proizvodu, kartično plaćanje, preuzimanje u prodavnici.

**`GET /products`** vraća cijelu listu — nema `page`, `sort`, filtera po cijeni ni po proizvođaču
(`CatalogQueries.cs`, `GetProductsQuery(Category, Search, Featured)`). Katalog je mali
(18 proizvoda, 7 kategorija), pa sortiranje/paginacija/filteri idu **klijentski**.

**Stvarne kategorije:** Povrće, Voće, Mliječni proizvodi, Meso i suhomesnato, Jaja,
Med i pčelinji proizvodi, Zimnica i džemovi. Mockupovih „Pekara / Zdrava hrana / Piće / Ostalo"
**nema** — koriste se stvarne.

**Checkout** obavezno traži `deliveryZoneId` + `deliverySlotId` (+ pin); gost ide kroz potvrdu
mejlom u dva koraka (`/orders/guest-request` → `/orders/confirm`).

**Adresar** (`docs/superpowers/plans/2026-09-04-moj-nalog-adresar.md`): Task 0–3 su **urađeni**
(entitet, migracija, `GET/POST/PUT/DELETE /account/addresses`, `.../default`, testovi
`AddressCommandsTests`). **Task 4 nije** — `PUT /account/profile` ne postoji, pa izmjena
imena/telefona nema endpoint. Task 5–7 (stranica `/account`, `AddressChooser` na `Address[]`,
čišćenje migracije) takođe nisu urađeni. Čekboksi u tom planu nisu održavani — ne vjerovati im.

**Ne postoje rute:** `/kategorije`, `/moj-nalog`, „Domaćini", „Kako radi", hamburger drawer.

**Postoji i koristi se:** `MobileTabBar` (4 taba), `CartDrawer`, `ProductCard`, `CategoryChips`,
`QuantityStepper`, `AddressChooser`, `AddressPicker`, toast, `flyToCart`.

---

## Status faza

| Faza | Sadržaj | Status |
|---|---|---|
| 0 | Brainstorming, spec, plan | **gotova** |
| 1 | Tokeni, header, bottom nav, product card, dugmad, section header, početna | **gotova** |
| 2 | Proizvodi (sidebar filteri, klijentsko sortiranje/paginacija), detalj proizvoda | **na redu** |
| 3 | Korpa, checkout | nije počela |
| 4 | Kategorije, Moj nalog + Adresar, mobile drawer | nije počela |
| 5 | Backend za omiljene + srce na kartici | nije počela |

---

## Hronologija

### 2026-10-02

- Pokrenut stek i provjeren: Postgres + OSRM u Dockeru (up 8 dana), `EPiljara.API` na :5080
  (PID 5336), Next.js na :3000 (PID 9620), `frontend/.env.local` gađa pravi backend.
  Početna stranica radi, bez grešaka u konzoli.
- Pročitan `CLAUDE.md`, `domain.ts`, `endpoints.ts`, `contracts.ts`, `CatalogController.cs`,
  `CatalogQueries.cs`, `globals.css`, `(shop)/layout.tsx`, `mobile-tab-bar.tsx`, `public/`.
- Popisane razlike mockupa i backenda (gore, „Zatečeno stanje").
- Korisnik odgovorio na 4 pitanja (gore, „Odluke korisnika") i potvrdio da je screenshot
  vizuelni source of truth.
- **Sekcija 1 dizajna (paleta + obim Faze 1) — odobrena.**
- Napravljen ovaj dnevnik.

### 2026-10-02 (nastavak) — sekcija 2 dizajna odobrena

Dodatne odluke korisnika:

| Pitanje | Odluka |
|---|---|
| Srce za neprijavljene | **Srce vide samo prijavljeni** — gost ga uopšte ne vidi na kartici |
| „Lični podaci" u Mom nalogu | **Dodati `PUT /account/profile`** (dovršava Task 4 plana od 2026-09-04) |
| „O nama" i „Kontakt" iz drawera | **Napraviti obje stranice**, kratke |

Riješeno u sekciji 2:

- **Header ide po screenshotu, ne po tekstu briefa.** Brief traži desktop nav
  (Početna/Proizvodi/Kategorije/Domaćini/Kako radi/Kontakt); na screenshotu ga nema —
  desktop je logo + search + ikone + traka kategorija. Tekstualni meni postoji samo u
  mobilnom draweru. Screenshot je izvor istine → nema desktop tekstualnog nava.
- **Nove rute: 3** — `/kategorije`, `/account`, plus `/o-nama` i `/kontakt` (kratke).
  „Domaćini" ostaje sekcija `#domacini` na početnoj (već radi preko `useProducers()`).
  „Kako radi" ispada — nema ga nigdje na screenshotu.
- **`/products` dobija klijentski sloj**: filteri, sortiranje i paginacija u `useMemo`
  nad punom listom; pretraga ostaje serverska. Stanje u URL-u
  (`?category=&cijena=&proizvodjac=&sort=&str=`). **Obavezno `<Suspense>`** oko
  `useSearchParams()` — inače pada `npm run build`.
- **Sidebar filteri**: Kategorije ✓, Cijena (klijentski) ✓, Brend → **Proizvođač**,
  Ocjena → **Dostupnost**. Grid 4×3 = 12 po strani.
- **Detalj proizvoda**: od 4 taba ostaju **dva** — Opis i Dostava (prave zone i termini).
  Nutritivne vrijednosti i Recenzije otpadaju (nema podataka). Bez thumbnaila i zvjezdica.
- **Omiljeni**: novi entitet `Favorite` (`UserId`, `ProductId`, `CreatedAt`), jedinstveni
  indeks na par, kaskadno uz korisnika. `GET /account/favorites`,
  `POST|DELETE /account/favorites/{productId}`, svi `[Authorize]`.

### 2026-10-02 (nastavak) — spec napisan

- Napravljena grana `redizajn-opcija3` od `main`.
- **Sekcija 3 dizajna odobrena** (komponente, granice, provjera).
- Napisan spec: `docs/superpowers/specs/2026-10-02-redizajn-opcija3-design.md`.
- Samopregled speca našao i ispravio: pogrešna imena kategorija u skici headera
  (bila mockupova umjesto naših), nedostajao opis početne i korpe, nedefinisan sadržaj
  `/o-nama` i `/kontakt` (sad `{{ZAMIJENITI}}`), i nerazriješen sudar faza — srce se
  gradi u Fazi 1 a radi tek od Faze 5.
- Commit `40bf8dd`. **Čeka se korisnikov pregled speca prije pisanja plana.**

### 2026-10-02 (nastavak) — plan Faze 1 napisan

- Spec odobren bez izmjena.
- Odluka: **plan po fazi**, ne jedan plan za svih pet — svaka faza daje nešto što radi,
  a detalji kasnijih faza zavise od ishoda Faze 1.
- Napisan `docs/superpowers/plans/2026-10-02-redizajn-opcija3-faza1.md` — 8 taskova:
  tokeni → dugmad/logo/section-header → kartica → header+traka → donja navigacija →
  benefiti/pločice → početna → provjera.
- Provjereno u kodu prije pisanja (da plan ne sadrži pretpostavke): `ProductGridSkeleton`
  prima `count?`, lucide 1.23 ima `Grid2x2`/`Leaf`/`ShieldCheck`/`Truck`/`User`,
  `tone="dark"` se šalje na tačno dva mjesta u `(shop)/page.tsx` (285, 291).
- Samopregled plana našao i ispravio: Task 4 je navbar opisivao riječima umjesto kodom
  (sad je cio fajl ispisan), `MREZA` u `product-grid.tsx` ima `gap-8` uz komentar koji
  tvrdi da kartica nema okvir — a nova ga ima, pa se i razmak i komentar mijenjaju;
  mreža na početnoj se razilazila sa skeletonom; pobrkana numeracija stepova u Tasku 3.

### 2026-10-02 (nastavak) — Task 1 (tokeni) gotov

- **Task 1 (tokeni) gotov.** `globals.css` na novoj paleti; obrisana `.animate-traka`.
  Tamne sekcije na početnoj još stoje — one su Task 7.

### 2026-10-02 (nastavak) — Task 2 (dugmad, logo, section header) gotov

- **Task 2 gotov.** Dugmad na 10px radijusu umjesto pilule, logo „piljara" iz amber u
  brand zelenu, nov `ui/section-header.tsx`.

### 2026-10-02 (nastavak) — Task 3 (kartica proizvoda) gotov

- **Task 3 gotov.** Kartica proizvoda po mockupu: bijela sa borderom, slika 1:1, badge
  gore lijevo, dugme pune širine. **`tone` prop uklonjen** — mockup nema tamnu karticu.
  Gornji desni ugao ostavljen prazan za srce iz Faze 5.

### 2026-10-02 (nastavak) — Task 4 (header i traka kategorija) gotov

- **Task 4 gotov.** Nova `layout/category-rail.tsx` (okrugle slike, naše stvarne kategorije,
  aktivna iz `?category=`). Header više ne pluta nad herojem — svijetao i sticky svuda;
  obrisani `skrolovano`, `nadHerojem`, gradijent i `locationChip`. `ShopNavbar` je sada
  omotan `<Suspense>` jer traka čita `useSearchParams()`. Obrisana `shop/category-chips.tsx`.

### 2026-10-02 (nastavak) — Task 5 (peti tab) gotov

- **Task 5 gotov.** Donja navigacija ima pet tabova (Početna, Proizvodi, Kategorije,
  Korpa, Profil), safe-area i bijelu podlogu. Layout `pb-16` → `pb-20`.
  **Otvoreno:** tab „Kategorije" vodi na `/kategorije` koja postoji tek od Faze 4 — do
  tada daje 404. „Profil" privremeno vodi na `/orders` (prijavljen) ili `/login` (gost).

### 2026-10-02 (nastavak) — Task 6 (komponente benefita i pločica) gotov

- **Task 6 gotov.** Nove `shop/benefit-cards.tsx` (četiri benefita, tekstovi na jednom
  mjestu) i `shop/category-tile.tsx`. Koristi ih Task 7.

### 2026-10-06 — Task 7 (početna stranica) gotov — Faza 1 završena

- **Task 7 gotov — Faza 1 završena.** Početna prepisana na sedam svijetlih sekcija;
  uklonjene obje tamne sekcije (hero i izdvojeni proizvodi). Tamno je ostalo samo na
  promo baneru i u footeru.

### 2026-10-06 (nastavak) — Task 7, fix runda 1

- Recenzija je potvrdila stranicu kao ispravnu osim jedne regresije iz briefa:
  Step-2 kod je ispustio prazno/grešno stanje koje je stara stranica imala za
  "Popularne kategorije" i "Izdvojeni proizvodi" — bez njega bi naslov sekcije
  lebdio iznad praznog prostora ako zahtjev padne ili vrati praznu listu.
  Dodata treća grana (`categories.data?.length ? … : <p>Kategorije trenutno nisu
  dostupne.</p>`, analogno za proizvode: `Trenutno nema izdvojenih proizvoda.`).
- Dodatno (minor), sekcija domaćina nije imala `isLoading` skeleton kao
  kategorije i proizvodi — dodat skeleton (4 kartice, ista mreža kao učitano
  stanje) i prazno/grešno stanje `Podaci o domaćinima trenutno nisu dostupni.`
- `npx tsc --noEmit`, `npm run lint`, `npm run build` — sve zeleno.

### 2026-10-06 (nastavak) — Task 8 (provjera cijele faze) gotov — **Faza 1 završena**

**Komandna linija i statička provjera** (bez pisanja koda, bez restarta servera):

- `npx tsc --noEmit` — čisto, bez izlaza, exit 0.
- `npm run lint` — čisto, `eslint` bez grešaka/upozorenja, exit 0.
- `npm run build` — čisto, `Compiled successfully in 6.5s`, 47/47 statičkih strana,
  exit 0.
- `grep -rn "night-card\|muted-dark\|animate-traka" src --include="*.tsx"
  --include="*.css"`: `night-card` 2 pogotka u `globals.css` (definicija tokena,
  ne upotreba) — očekivano; `muted-dark` 6 pogodaka (1 definicija +
  5 u `shop-footer.tsx`, koji ova faza nije dirala) — očekivano. `animate-traka`
  1 pogodak — `category-rail.tsx:12`, unutar komentara koji **imenuje** obrisanu
  klasu da objasni šta je zamijenila; nema CSS pravila ni `className` upotrebe.
  **Provjera prolazi** — pravilo iz plana ("nijedan pogodak") bilo je prestrogo
  formulisano (to je greška plana, ne koda); komentar koji dokumentuje obrisanu
  klasu nije "ostatak u upotrebi".
- **Statička provjera širina** (čitanje klasa, bez renderovanja — automatska
  emulacija viewporta nije radila, vidi dolje): `grid-cols-2` bez breakpoint
  prefiksa potvrđuje **2 proizvoda po redu na telefonu**, i na `product-grid.tsx`
  i na početnoj ("Izdvojeni proizvodi"). Tap targeti uglavnom `h-10`/`h-11`
  (40–44px). `.no-scrollbar` u `globals.css` sakriva scrollbar na traci kategorija
  (webkit/IE/Firefox). Dva konkretna nalaza (vidi "Otvoreno" dolje): `text-[10px]`
  na badge-u broja u korpi (ispod ~11px praga), i `main`-ov `pb-20` (80px) koji ne
  uključuje `env(safe-area-inset-bottom)` dok donja traka to radi.

**Browser provjera (kontroler, na živom steku, API + dev server up):**

- Nema vodoravnog skrola: `/products` na 1905px, `scrollWidth` = `clientWidth` = 1905.
- Tok korpe radi end-to-end: dodavanje sa početne → toast → `localStorage` (`epiljara.cart`)
  → `/cart` prikazuje stavku, Međuzbir, **"Dostava — računa se na naplati"** (prava
  fraza, ne izmišljena 0.00 KM), Ukupno. (Prvi klik nije upalio jer je stigao prije
  hidracije — artefakt testa, ne regresija.)
- Error stanja rade: API zaustavljen, početna prikazala sva tri fallback teksta
  ("Kategorije trenutno nisu dostupne.", "Trenutno nema izdvojenih proizvoda.",
  "Podaci o domaćinima trenutno nisu dostupni."), naslovi sekcija ostali.
- Početna odgovara mockupu end-to-end (svijetli hero, četiri benefita, šest
  pločica kategorija **jednake visine**, svijetla sekcija proizvoda, domaćini,
  tamni promo baner, footer — tamno samo na baneru i footeru).
- Header geometrija stabilna (header 164px, traka 98px, svaka stavka trake 78px);
  aktivno stanje trake i donje navigacije ispravno na `/products?category=povrce`,
  `/cart`, `/login`.
- Donja traka: pet tabova, ispravni nazivi.

**Report:** `.superpowers/sdd/2026-10-02-redizajn-opcija3-faza1/task-8-report.md`
(puni izlaz sve tri komande, kompletan grep, po-fajl statička analiza širina sa
citiranim klasama, i dodatak sa browser nalazima kontrolera).

#### Otvoreno za Fazu 2 (i dalje)

1. **Donja traka vs. safe-area-inset — potvrđeno, nije samo statička pretpostavka.**
   Izmjereno: traka 56px, `main`-ov `padding-bottom` 80px. Na desktopu
   `env(safe-area-inset-bottom)` je 0 (24px rezerve), ali traka raste za inset
   (`pb-[env(safe-area-inset-bottom)]`) dok je `main`-ov `pb-20` statičan. Na
   telefonu sa ~34px insetom traka postaje ~90px naspram 80px rezerve — zadnjih
   ~10px sadržaja može biti ispod trake. **Popravka (nije urađena ovdje, ide u
   finalnu fix rundu cijele grane):** `pb-[calc(5rem+env(safe-area-inset-bottom))]`
   na `<main>` u `frontend/src/app/(shop)/layout.tsx`.
2. **`/products` trenutno ima dvije trake kategorija.** Header je dobio
   `CategoryRail` (Task 4), a `/products` i dalje crta svoje stare chipove ispod
   nje — samo header-ova nosi `nav[aria-label="Kategorije"]`, dvije stvarno
   različite komponente. Faza 2 prepisuje `/products` i uklanja stare chipove, pa
   se ovo samo razrješava — ali je vidljivo sada i zapisano da se ne otkriva
   iznova.
3. **Badge broja u korpi (donja traka) koristi `text-[10px]`** — ispod ~11px
   praga iz provjere. Mala je numerička oznaka, ne tijelo teksta, ali je
   konkretan pogodak koji treba riješiti ili svjesno prihvatiti u finalnoj rundi.
4. **Mobilni raspored nije vizuelno provjeren — ni sada, ni u prethodnoj sesiji.**
   Tri pristupa nisu radila za kontrolera: `resize_window` javlja uspjeh ali
   `window.innerWidth` ostaje 1920; sized popup je blokiran; Playwright/Puppeteer
   nisu instalirani. Jedina provjera koju je mobilni raspored dobio je statičko
   čitanje klasa (vidi report, Task 8). Dodatno: Chrome prozor se dvaput skupio na
   0×0 i morao biti ponovo otvoren, jedna JS evaluacija je timeout-ovala — browser
   alat u ovom okruženju je nepouzdan, vrijedi znati za Fazu 2.
5. (Prethodno poznato, stoji i dalje) `/kategorije` i `/account` ne postoje —
   rješava Faza 4. `(shop)/products/page.tsx` čeka puni redizajn u Fazi 2. Srce za
   omiljene stiže u Fazi 5. `shop-footer.tsx` ostaje tamni (nije diran, mockup i
   ima tamni footer).

**Faza 1 (Taskovi 1–8) je završena.** Sve tri komande (`tsc`, `lint`, `build`)
čiste; grep provjera prolazi; tok korpe i prijave radi na pravom steku; jedino
mobilni raspored ostaje nepotvrđen zbog ograničenja alata, ne zbog koda.

---

# ⏸ GDJE SMO STALI — 2026-10-02

> **Prevaziđeno.** Ovaj odjeljak je snimak stanja usred Faze 1 (poslije Taska 4).
> Rad je od tada nastavljen i **Faza 1 je u cijelosti završena** (Task 8, zapis gore,
> 2026-10-06). Odjeljak je ostavljen kao istorijski trag, ne kao trenutno stanje.

Rad **pauziran na korisnikov zahtjev** usred Faze 1. Radno stablo je **čisto**, sve je
commitovano, ništa nije u pola koraka.

## Stanje

**Grana:** `redizajn-opcija3`, HEAD = `38865e2`, 8 commita ispred `main`.
**Faza 1: taskovi 1–4 od 8 su urađeni i commitovani.**

| Task | Šta | Commit | Recenzija |
|---|---|---|---|
| 1 | Tokeni u `globals.css` | `e7b5bc8` + fix `23f266a` | ✅ čista nakon 1 fix runde |
| 2 | Dugmad, logo, `section-header.tsx` | `c57d036` | ✅ čista iz prve |
| 3 | Kartica proizvoda, `MREZA`, uklonjen `tone` | `934fb6e` | ✅ čista iz prve |
| 4 | Header + `category-rail.tsx`, obrisan `category-chips.tsx` | `38865e2` | ⏸ **NIJE RECENZIRAN** |
| 5 | Donja navigacija na 5 tabova | — | nije počeo |
| 6 | `benefit-cards.tsx`, `category-tile.tsx` | — | nije počeo |
| 7 | Početna stranica | — | nije počeo |
| 8 | Provjera cijele faze | — | nije počeo |

## PRVI KORAK PRI NASTAVKU

**Task 4 je implementiran ali recenzija je prekinuta u pola.** Ne preskakati je.

```
BASE = 52e00cf   HEAD = 38865e2
diff paket: .superpowers/sdd/2026-10-02-redizajn-opcija3-faza1/review-52e00cf..38865e2.diff
brief:      .superpowers/sdd/2026-10-02-redizajn-opcija3-faza1/task-4-brief.md
report:     .superpowers/sdd/2026-10-02-redizajn-opcija3-faza1/task-4-report.md
```

Implementer je prijavio `DONE_WITH_CONCERNS`: tsc/lint/build sve zeleno (build 47/47 strana,
**bez `Suspense` greške**), a jedino što nije uradio je provjera u browseru — jer `npm run
build` obori dev server, a njemu nije dozvoljeno da gasi procese.

**Što na Tasku 4 treba posebno provjeriti:** da `<Suspense>` stvarno obuhvata komponentu
koja čita `useSearchParams()`; da nije ostao nijedan trag `nadHerojem` / `skrolovano` /
`MapPin` / `locationChip`; da `cn` import nije ostao neiskorišten; i da imena kategorija
dolaze iz `useCategories()` a nisu hardkodovana.

**Vizuelna provjera Task 4 takođe nije urađena** — dev server je restartovan poslije
build-a i treba pogledati izgleda li header kao na mockupu (logo, search, ikone, ispod
traka sa 7 okruglih kategorija, sticky, bez plutanja nad herojem).

## Okruženje

- Dev server je restartovan i radi u pozadini; log: `%TEMP%\epiljara-dev.log`.
  **Napomena:** `npm run build` ga svaki put obori (dijele `.next`) — vidi Global
  Constraints u planu Faze 1. Poslije svakog build-a treba restart.
- Backend `EPiljara.API` na :5080, Postgres i OSRM u Dockeru — svi su radili.

## Otvoreno, za finalnu fix rundu Faze 1

Minor nalazi skupljeni dosad (nijedan ne blokira):

1. `logo.tsx` — komentar tvrdi `brand-300` na `night` daje **7.2:1**; stvarno je **7.69:1**.
   Moja greška u planu, ne implementerova.
2. `product-card.tsx` — `justify-between` na divu koji obavija samo jedno dijete (cijenu);
   mrtva klasa. Takođe greška u planu.
3. `section-header.tsx` — koristi globalni `React.ReactNode` bez `import type`.
4. `product-card.tsx` — badge se renderuje bezuslovno, bez stare `categoryName &&` straže
   (u praksi bezopasno, polje je obavezno).
5. Dva `Link`-a na isto odredište u kartici → duplirani linkovi za čitače ekrana
   (zatečeno, nije regresija).
6. Task 4 commit trailer kaže „Claude Sonnet 5" umjesto „Claude Opus 5" — implementer je
   slijedio svoj session reminder. Bezopasno.

## Poznata ograničenja

- **Mobilni raspored nije provjeren i ne smije se tvrditi da jeste.** `resize_window`
  vraća „Successfully resized" ali `window.innerWidth` ostaje 1920 (probano na 375 i 390),
  pa screenshot pokazuje desktop. Izmjereno je samo da na 1920px nema vodoravnog skrola
  (`scrollWidth` 1905 = `clientWidth` 1905). Task 8 treba naći metod koji radi ili tražiti
  od korisnika da pogleda u devtools-u.
- Tabovi „Kategorije" i „Profil" u donjoj navigaciji (Task 5) vodiće na rute koje postoje
  tek u Fazi 4 — namjerno, zapisano u planu.

## Dokumenti

- Spec: `docs/superpowers/specs/2026-10-02-redizajn-opcija3-design.md`
- Plan Faze 1: `docs/superpowers/plans/2026-10-02-redizajn-opcija3-faza1.md`
- SDD ledger (git-ignored, detaljniji): `.superpowers/sdd/2026-10-02-redizajn-opcija3-faza1/progress.md`
- Vizuelni izvor istine: `C:\Users\soviljn\Desktop\E-piljara Grocery UI Showcase complete.png`
