# Wolt-čisti vizuelni redizajn — E piljara

**Datum:** 2026-08-06
**Status:** Odobren dizajn, spreman za plan implementacije
**Obim:** Vizuelno dotjerivanje (frontend) — cijela aplikacija (kupovni dio + admin)

## Cilj

Aplikacija treba da izgleda i djeluje kao moderna aplikacija za dostavu hrane u stilu
**Wolt-a** (čisto, minimalistički, puno bijelog prostora, slika kao heroj), uz zadržavanje
**maslinasto zelenog** brend identiteta E piljare kao akcenta ("domaće sa Manjače").

**Ne mijenjamo:** strukturu ekrana, poslovni model (sedmična dostava), API/HTTP contract,
funkcionalnost. Ovo je čisto vizuelni/UX sloj.

**Van obima (mogući kasniji koraci):** push notifikacije, ekran praćenja narudžbe,
model "dostava odmah", ocjene/napojnice.

## Vizuelni pravac (odlučeno kroz mockupove)

- **Struktura:** Wolt-čisto — bijela pozadina, tanke hairline linije, mekane hladne sjenke,
  velike slike proizvoda, dosta praznog prostora.
- **Akcenat:** maslinasto zelena (`brand-600` `#2a7a3a` / `brand-700` `#246330`).
- **Med (honey):** degradira se — više NIJE za primarne CTA, samo za istaknuto ("Izdvojeno").
- **Animacije:** bogate mikrointerakcije, pogonjene `motion` bibliotekom (Framer Motion,
  kompatibilan s React 19). Sve poštuje `prefers-reduced-motion`.

## 1. Temelj — dizajn tokeni (`frontend/src/app/globals.css`, `@theme`)

Najveći efekat uz najmanje rizika: izmjena centralizovanih tokena propagira se kroz cijelu app.

| Token | Sada (toplo) | Novo (Wolt-čisto) |
|-------|--------------|-------------------|
| `--color-cream` (page bg) | `#faf7f0` | `#ffffff` |
| `--color-sand` (sekcije/hover) | `#f1ece0` | `#f4f5f7` (hladan neutral) |
| `--color-ink` (tekst) | `#23271f` | `#16181d` (hladniji) |
| `--color-muted` | `#6b7263` | `#8a9099` (čista siva) |
| `--radius-card` | `1rem` | ujednačen 12–16px sistem |

Dodatno:
- **Novi neutralni tonovi** za granice i površine (npr. `--color-line: rgba(0,0,0,.07)`,
  `--color-surface: #ffffff`, `--color-surface-muted: #f4f5f7`).
- **Elevacije (sjenke):** dodati skalu mekih, hladnih sjenki (npr. `--shadow-sm/-md/-lg`)
  umjesto oslanjanja na `ring-1 ring-black/5`.
- **Tipografija:** Inter + Jakarta ostaju. Naslovi `semibold` umjesto `extrabold`, tješnji
  `letter-spacing`, više line-heighta.

Pošto se postojeće klase `bg-cream`/`bg-sand`/`text-ink`/`text-muted` preusmjeravaju kroz
tokene, veliki dio ekrana se osvježava automatski, bez diranja svakog fajla.

## 2. Zajedničke komponente

- **`ProductCard`** (`components/shop/product-card.tsx`): bijela kartica, veća slika,
  ime `semibold`, proizvođač muted, cijena + okrugli zeleni "+". Hover: lift + mekana
  sjenka + suptilni zoom slike. "Izdvojeno" badž ostaje (honey), "nedostupno" overlay čistiji.
- **`Button`** (`components/ui/button.tsx`): `primary` zelena ostaje; `secondary` čistiji
  sivi (hladan neutral umjesto `sand`); `honey` varijanta se povlači iz primarne upotrebe;
  press-scale efekat; zaobljenije pilule.
- **Čipovi** (kategorije, lokacijski chip, dostavne zone): hladne sive pilule.
- **Search / inputi** (`shop-navbar.tsx`, `components/ui/field.tsx`): čist `#f4f5f7` bg,
  zeleni focus ring, čistije stanje greške.

## 3. Ekrani

### Kupovni dio (`src/app/(shop)`, `components/shop`, `components/layout`)
- **Početna** (`(shop)/page.tsx`): banner → čistiji (flatter, manje "teški"); karuseli
  kategorija i izdvojenog; sekcija domaćina; "kako funkcioniše"; dostavne zone.
- **Lista proizvoda** (`(shop)/products/page.tsx`): čist grid, filteri kao čipovi.
- **Detalj proizvoda** (`(shop)/products/[slug]/page.tsx`): velika slika, čist raspored.
- **Korpa + cart drawer** (`(shop)/cart/page.tsx`, `components/shop/cart-drawer.tsx`):
  čiste stavke, glatki slide.
- **Checkout** (`(shop)/checkout/page.tsx`): čist formular; OSM/Leaflet mapa ostaje.
- **Narudžbe** (`(shop)/orders/page.tsx`): čiste kartice + status pilule.
- **Navbar + donji tab bar** (`shop-navbar.tsx`, `mobile-tab-bar.tsx`): bijeli, tanka
  linija, aktivna stavka zelena.

### Admin (`src/app/admin`, `components/admin`)
- Naslijeđuje nove tokene automatski.
- Ciljana dorada: order-kartice (`order-card.tsx`), tabele/liste, drawer forme
  (`*-form.tsx`), dashboard — isti čisti stil.

## 4. Pokret / animacije (bogato)

Motor: **`motion`** (Framer Motion). Sve iza provjere `prefers-reduced-motion`.

- Hover-lift + zoom slike na karticama; press-scale na dugmadima.
- **Animirano "dodaj u korpu"**: leteći efekat ka korpi + poskok badža na tab baru /
  cart dugmetu.
- Staggered `fade-in-up` sekcija i kartica pri učitavanju (mount / scroll reveal).
- **Shimmer skeleton** (nadogradnja postojećeg `animate-pulse`).
- Glatki slide cart drawer-a i toast obavještenja.

## 5. Pristup implementacije (fazno, bezbjedno)

1. **Faza 1 — Temelj:** tokeni (`globals.css`) + zajedničke komponente
   (`Button`, `ProductCard`, čipovi, inputi). Najveći vizuelni pomak, propagira se svuda.
2. **Faza 2 — Ekrani:** dorada ekran-po-ekran; prvo kupovni dio, zatim admin.
3. **Faza 3 — Animacije:** dodati `motion`, implementirati mikrointerakcije.

**Verifikacija:** nema test suite-a. Nakon svake faze pokrenuti frontend (`npm run dev`),
`npx tsc --noEmit`, `npm run lint`, i vizuelno proći kroz izmijenjene ekrane
(desktop + mobilni breakpoint).

## Rizici / napomene

- **Kontrast/pristupačnost:** pri prelasku na bijelo paziti na kontrast muted teksta.
- **`motion` + React 19:** koristiti verziju kompatibilnu s React 19.2 (paket `motion`,
  ne stari `framer-motion` ako pravi problem s peer deps).
- **Dosljednost:** honey se povlači postupno — provjeriti sva mjesta gdje je trenutno CTA.
- **Bez promjene contracta:** nijedan API poziv, tip, ni envelope se ne dira.
