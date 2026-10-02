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
| 0 | Brainstorming, spec, plan | **u toku** |
| 1 | Tokeni, header, bottom nav, product card, dugmad, section header, početna | nije počela |
| 2 | Proizvodi (sidebar filteri, klijentsko sortiranje/paginacija), detalj proizvoda | nije počela |
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
