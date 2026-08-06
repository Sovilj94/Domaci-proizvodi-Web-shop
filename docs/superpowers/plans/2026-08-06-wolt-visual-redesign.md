# Wolt-čisti vizuelni redizajn — Implementacioni plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redizajnirati cijeli frontend E piljare u čisti, minimalistički Wolt-stil (bijelo, tanke linije, mekane sjenke, velika slika kao heroj) uz zadržavanje maslinasto zelenog brend akcenta, plus bogate mikrointerakcije.

**Architecture:** Najveći dio postiže se izmjenom centralizovanih dizajn-tokena u `globals.css` (`@theme`), što se automatski propagira kroz sve `bg-cream`/`bg-sand`/`text-ink`/`text-muted`/`ring`/`rounded-card` klase. Zatim se dorađuju dijeljene komponente (Button, ProductCard, inputi, toast), pa ekran-po-ekran, i na kraju sloj animacija pogonjen `motion` bibliotekom.

**Tech Stack:** Next.js 15 (App Router, React 19.2), TypeScript, Tailwind CSS v4 (`@theme` tokeni), lucide-react ikone, `motion` (Framer Motion za React 19).

## Global Constraints

- **Bez promjene HTTP contracta:** nijedan API poziv, tip (`types/domain.ts`), ni envelope se ne dira. Ovo je čisto vizuelni sloj.
- **Bosanski (BHS):** svi novi/izmijenjeni korisnički tekstovi ostaju na bosanskom.
- **Tailwind v4:** boje/sjenke/radijusi se definišu kao CSS varijable u `@theme` bloku `globals.css` (postojeći obrazac). NE dodavati `tailwind.config.js`.
- **React 19.2:** animacijska biblioteka mora biti kompatibilna — koristiti paket `motion` (import `motion/react`), NE stari `framer-motion`.
- **`prefers-reduced-motion`:** sve animacije moraju se isključiti kad je korisnik to zatražio.
- **Nema test suite-a** (per CLAUDE.md). Verifikacija svakog taska = `npx tsc --noEmit` (iz `frontend/`) prolazi bez grešaka + `npm run lint` prolazi + `npm run dev` i vizuelna provjera izmijenjenog ekrana na desktop I mobilnom breakpointu (DevTools ~390px).
- **Radi iz `frontend/`** za sve npm komande.
- **Akcenat = zelena** (`brand-600`/`brand-700`). **Med (honey) NIJE za CTA** — samo za istaknuto ("Izdvojeno").

---

## FAZA 1 — Temelj (tokeni + dijeljene komponente)

### Task 1: Dizajn tokeni — Wolt-čista paleta, sjenke, radijusi

**Files:**
- Modify: `frontend/src/app/globals.css` (cijeli `@theme` blok + body + dodatak keyframes)

**Interfaces:**
- Produces: nove utility klase dostupne svuda — `shadow-card`, `shadow-md`, `shadow-lg`, `border-line`/`bg-line`, `animate-shimmer`; preusmjerene vrijednosti za `bg-cream` (→ bijela), `bg-sand` (→ hladan neutral `#f4f5f7`), `text-ink` (→ `#16181d`), `text-muted` (→ `#8a9099`).

- [ ] **Step 1: Zamijeni sadržaj `globals.css`**

```css
@import "tailwindcss";

/*
 * E piljara design tokens — Wolt-čista paleta.
 * Tailwind v4 čita ove iz @theme i generiše utilitije
 * (bg-brand-500, text-muted, rounded-card, shadow-card, font-display, ...).
 */
@theme {
  /* Brand — domaća maslinasto zelena (akcenat) */
  --color-brand-50: #f0f9f1;
  --color-brand-100: #dcf0df;
  --color-brand-200: #bce3c2;
  --color-brand-300: #8fce9a;
  --color-brand-400: #5bb06b;
  --color-brand-500: #379548;
  --color-brand-600: #2a7a3a;
  --color-brand-700: #246330;
  --color-brand-800: #204f2a;
  --color-brand-900: #1c4125;

  /* Med — SAMO za istaknuto ("Izdvojeno"), ne za CTA */
  --color-honey-100: #fdf1d6;
  --color-honey-300: #f8cf7a;
  --color-honey-400: #f6b545;
  --color-honey-500: #ef9d1f;
  --color-honey-600: #d98209;

  /* Neutrali — čisti, hladni (Wolt) */
  --color-cream: #ffffff;   /* pozadina stranice */
  --color-sand: #f4f5f7;    /* prigušena površina / hover */
  --color-ink: #16181d;     /* skoro-crn tekst */
  --color-muted: #8a9099;   /* sekundarni tekst */
  --color-line: #ececef;    /* hairline granice */

  --font-sans: var(--font-inter), ui-sans-serif, system-ui, sans-serif;
  --font-display: var(--font-jakarta), var(--font-inter), ui-sans-serif, sans-serif;

  --radius-card: 0.875rem; /* 14px, Wolt-tijesno */

  /* Elevacije — mekane, hladne sjenke */
  --shadow-sm: 0 1px 2px rgba(16, 24, 40, 0.05);
  --shadow-card: 0 1px 3px rgba(16, 24, 40, 0.06), 0 1px 2px rgba(16, 24, 40, 0.04);
  --shadow-md: 0 6px 20px rgba(16, 24, 40, 0.08);
  --shadow-lg: 0 14px 36px rgba(16, 24, 40, 0.12);

  /* Animacije */
  --animate-shimmer: shimmer 1.5s infinite;
}

@keyframes shimmer {
  0% { background-position: -200% 0; }
  100% { background-position: 200% 0; }
}

html,
body {
  height: 100%;
}

body {
  background: var(--color-cream);
  color: var(--color-ink);
  font-family: var(--font-sans);
  -webkit-font-smoothing: antialiased;
}

/* Hide scrollbars on horizontal rails (category chips, etc.) */
.no-scrollbar::-webkit-scrollbar {
  display: none;
}
.no-scrollbar {
  -ms-overflow-style: none;
  scrollbar-width: none;
}

/* Shimmer skeleton površina */
.skeleton {
  background: linear-gradient(90deg, #eef0f3 25%, #f6f7f9 37%, #eef0f3 63%);
  background-size: 200% 100%;
  animation: var(--animate-shimmer);
}

/* Poštuj korisnikovu želju za manje pokreta */
@media (prefers-reduced-motion: reduce) {
  *,
  *::before,
  *::after {
    animation-duration: 0.001ms !important;
    animation-iteration-count: 1 !important;
    transition-duration: 0.001ms !important;
  }
}
```

- [ ] **Step 2: Typecheck + lint**

Run (iz `frontend/`): `npx tsc --noEmit && npm run lint`
Expected: prolazi bez grešaka.

- [ ] **Step 3: Vizuelna provjera**

Run: `npm run dev`, otvori `http://localhost:3000`. Očekivano: pozadina je sada bijela umjesto krem; kartice/sekcije koje su koristile `bg-sand` sada su hladno-sive; tekst je hladniji. Ništa ne smije biti "pokvareno" (nevidljiv tekst, bijelo-na-bijelo). Provjeri Početnu, `/products`, `/orders`.

- [ ] **Step 4: Commit** *(napomena: repo trenutno NIJE pod gitom; ako `git status` javi grešku, preskoči commit korake u cijelom planu)*

```bash
git add frontend/src/app/globals.css
git commit -m "feat(ui): Wolt-clean design tokens (white bg, cool neutrals, soft shadows)"
```

---

### Task 2: Dugmad — press-scale, čist secondary, povlačenje honey CTA

**Files:**
- Modify: `frontend/src/components/ui/button.tsx`

**Interfaces:**
- Consumes: tokene iz Task 1 (`shadow-sm`, `border-line`).
- Produces: `buttonVariants({variant,size})` i `<Button>` — iste potpise (`Variant = "primary"|"secondary"|"ghost"|"danger"|"honey"`, `Size = "sm"|"md"|"lg"|"icon"`), samo osvježene klase.

- [ ] **Step 1: Zamijeni `base` i `variants`**

U `frontend/src/components/ui/button.tsx` zamijeni konstante `base` i `variants`:

```tsx
const base =
  "inline-flex items-center justify-center gap-2 rounded-xl font-semibold transition active:scale-[0.97] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-50";

const variants: Record<Variant, string> = {
  primary: "bg-brand-600 text-white shadow-sm hover:bg-brand-700",
  secondary: "bg-white text-ink border border-line hover:bg-sand",
  ghost: "text-ink hover:bg-sand",
  danger: "bg-red-600 text-white hover:bg-red-700",
  honey: "bg-honey-500 text-ink hover:bg-honey-600",
};
```

(`honey` varijanta ostaje definisana radi kompatibilnosti, ali se u ekranima više ne koristi kao primarni CTA — vidi Task 5–8.)

- [ ] **Step 2: Typecheck + lint**

Run: `npx tsc --noEmit && npm run lint`
Expected: prolazi.

- [ ] **Step 3: Vizuelna provjera**

`npm run dev` → na `/products` klikni bilo koje dugme; očekivano: lagani "press" (skala 0.97) na klik, mekana sjenka na primarnom, tanka linija na secondary.

- [ ] **Step 4: Commit**

```bash
git add frontend/src/components/ui/button.tsx
git commit -m "feat(ui): cleaner button variants with press-scale"
```

---

### Task 3: Kartica proizvoda + skeleton (Wolt kartica, shimmer)

**Files:**
- Modify: `frontend/src/components/shop/product-card.tsx`
- Modify: `frontend/src/components/shop/product-grid.tsx`

**Interfaces:**
- Consumes: `shadow-card`, `shadow-md`, `.skeleton` iz Task 1.
- Produces: `<ProductCard product>` i `<ProductGrid products>` / `<ProductGridSkeleton count>` — nepromijenjeni propovi.

- [ ] **Step 1: Osvježi karticu**

U `product-card.tsx` zamijeni wrapper `div` (linija 17) i dugme "+" (linije 55–66):

Wrapper:
```tsx
<div className="group flex flex-col overflow-hidden rounded-card bg-white shadow-card transition hover:shadow-md">
```

Dugme "+" (klase):
```tsx
className="grid h-10 w-10 place-items-center rounded-full bg-brand-600 text-white shadow-sm transition hover:bg-brand-700 active:scale-90 disabled:cursor-not-allowed disabled:bg-zinc-200 disabled:text-zinc-400"
```

(Slika ostaje `aspect-square`; "Izdvojeno" honey badž ostaje — to je dozvoljena upotreba honey-a.)

- [ ] **Step 2: Shimmer skeleton**

U `product-grid.tsx` zamijeni `ProductGridSkeleton` telo tako da koristi `.skeleton` umjesto `animate-pulse bg-sand`:

```tsx
export function ProductGridSkeleton({ count = 8 }: { count?: number }) {
  return (
    <div className="grid grid-cols-2 gap-4 md:grid-cols-3 lg:grid-cols-4">
      {Array.from({ length: count }).map((_, i) => (
        <div key={i} className="overflow-hidden rounded-card bg-white shadow-card">
          <div className="skeleton aspect-square" />
          <div className="space-y-2 p-4">
            <div className="skeleton h-3 w-2/3 rounded" />
            <div className="skeleton h-4 w-full rounded" />
            <div className="skeleton h-5 w-1/2 rounded" />
          </div>
        </div>
      ))}
    </div>
  );
}
```

- [ ] **Step 3: Typecheck + lint**

Run: `npx tsc --noEmit && npm run lint`
Expected: prolazi.

- [ ] **Step 4: Vizuelna provjera**

`/products` (i osvježi za skeleton): kartice imaju mekanu sjenku umjesto prstena, hover ih blago podigne; skeleton "svjetluca" umjesto pulsiranja.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/components/shop/product-card.tsx frontend/src/components/shop/product-grid.tsx
git commit -m "feat(ui): Wolt product card + shimmer skeleton"
```

---

### Task 4: Dijeljena hroma — badž korpe, inputi, toast

**Files:**
- Modify: `frontend/src/components/shop/cart-button.tsx`
- Modify: `frontend/src/components/layout/mobile-tab-bar.tsx`
- Modify: `frontend/src/components/ui/field.tsx`
- Modify: `frontend/src/components/ui/toast.tsx`

**Interfaces:**
- Consumes: tokeni iz Task 1.
- Produces: nepromijenjeni eksporti (`CartButton`, `MobileTabBar`, `fieldClass`, `Field`, `Input`, `Textarea`, `Select`, `Label`, `ToastProvider`, `useToast`).

- [ ] **Step 1: Badž korpe → zelena**

U `cart-button.tsx` (linija 16) zamijeni `bg-honey-500 text-ink` sa `bg-brand-600 text-white`:
```tsx
className="absolute -right-0.5 -top-0.5 grid h-5 min-w-5 place-items-center rounded-full bg-brand-600 px-1 text-[11px] font-bold text-white"
```
U `mobile-tab-bar.tsx` (linija 50) isto — zamijeni `bg-honey-500 text-ink` sa `bg-brand-600 text-white`.

- [ ] **Step 2: Inputi → čist neutral**

U `field.tsx` zamijeni `fieldClass` (linija 3–4):
```tsx
export const fieldClass =
  "w-full rounded-xl border border-line bg-sand px-3.5 py-2.5 text-sm text-ink transition placeholder:text-muted/70 focus:border-brand-400 focus:bg-white focus:outline-none focus:ring-2 focus:ring-brand-400 disabled:opacity-60";
```

- [ ] **Step 3: Toast → mekana sjenka**

U `toast.tsx` (linija 40) zamijeni `shadow-lg ring-1 ring-black/5` sa `shadow-lg` (sjenka je sada mekša token-varijanta iz Task 1; prsten se uklanja):
```tsx
"pointer-events-auto flex items-center gap-2 rounded-full px-4 py-2.5 text-sm font-medium shadow-lg",
```

- [ ] **Step 4: Typecheck + lint**

Run: `npx tsc --noEmit && npm run lint`
Expected: prolazi.

- [ ] **Step 5: Vizuelna provjera**

Dodaj proizvod u korpu (badž je sada zelen); otvori `/checkout` (inputi su čisti, zeleni focus); toast na dnu ima mekanu sjenku.

- [ ] **Step 6: Commit**

```bash
git add frontend/src/components/shop/cart-button.tsx frontend/src/components/layout/mobile-tab-bar.tsx frontend/src/components/ui/field.tsx frontend/src/components/ui/toast.tsx
git commit -m "feat(ui): green cart badge, clean inputs, softer toast"
```

---

## FAZA 2 — Ekrani (kupovni dio, pa admin)

> Napomena: nakon Faze 1 većina ekrana je već znatno čistija (tokeni se propagiraju). Faza 2 su ciljane dorade rasporeda i preostalih toplih detalja.

### Task 5: Početna stranica

**Files:**
- Modify: `frontend/src/app/(shop)/page.tsx`
- Modify: `frontend/src/components/layout/shop-navbar.tsx`

**Interfaces:**
- Consumes: `shadow-card`, `border-line`, osvježene komponente iz Faze 1.

- [ ] **Step 1: Banner — čistiji**

U `page.tsx` promo banner (linije 21) — zamijeni tešku radial-gradient pozadinu čistijim izgledom i tanjim naslovom:
```tsx
<div className="relative overflow-hidden rounded-3xl bg-brand-700 px-6 py-8 text-white sm:px-10 sm:py-11">
  <div className="absolute inset-0 bg-[radial-gradient(60%_80%_at_90%_10%,rgba(255,255,255,0.10),transparent)]" />
```
I naslov (linija 27) `font-extrabold` → `font-bold`:
```tsx
<h1 className="mt-3 font-display text-2xl font-bold leading-tight sm:text-4xl">
```

- [ ] **Step 2: Kartice domaćina i "kako radi" — sjenka umjesto prstena**

U `page.tsx` zamijeni SVE pojave `ring-1 ring-black/5` (linije 114, 119, 154, 171) sa `shadow-card`. Za "zone dostave" pilule (linija 154) i `Step` kartice (linija 171): `ring-1 ring-black/5` → `shadow-card`.

- [ ] **Step 3: "Delivery strip" — hladan neutral**

U `page.tsx` (linija 147) zamijeni toplu `bg-honey-100` sekciju sa čistim neutralom:
```tsx
<section className="mt-12 bg-sand">
```

- [ ] **Step 4: Navbar — čist, tanka linija**

U `shop-navbar.tsx` header (linija 38) — pozadina je već `bg-cream/85` (sada bijela preko tokena); zamijeni `border-black/5` sa `border-line` (linije 38, 126). Lokacijski chip (linija 31) `ring-1 ring-black/5` → `border border-line`.

- [ ] **Step 5: Typecheck + lint + vizuelna provjera**

Run: `npx tsc --noEmit && npm run lint`, pa `npm run dev` → Početna izgleda čisto: bijeli navbar s tankom linijom, banner lakši, kartice s mekanim sjenkama, zone-sekcija hladno siva.

- [ ] **Step 6: Commit**

```bash
git add frontend/src/app/\(shop\)/page.tsx frontend/src/components/layout/shop-navbar.tsx
git commit -m "feat(ui): clean up home page + navbar"
```

---

### Task 6: Lista proizvoda + detalj proizvoda

**Files:**
- Modify: `frontend/src/app/(shop)/products/page.tsx`
- Modify: `frontend/src/app/(shop)/products/[slug]/page.tsx`

**Interfaces:**
- Consumes: Faza 1 komponente/tokeni.

- [ ] **Step 1: Pročitaj oba fajla**

Run: otvori `products/page.tsx` i `products/[slug]/page.tsx` i pronađi svaku pojavu `ring-1 ring-black/5`, `bg-cream`, `bg-honey`, `rounded-card`, `font-extrabold`.

- [ ] **Step 2: Zamjene (mehanički, dosljedno)**

Na oba ekrana primijeni:
- `ring-1 ring-black/5` → `shadow-card` (za kartice/panele) ili `border border-line` (za filter/čip kontejnere).
- `bg-honey-500`/`bg-honey-100` korišten kao dugme/CTA → `bg-brand-600 text-white` (badževi "Izdvojeno" ostaju honey).
- `font-extrabold` na naslovima → `font-bold`.
- Filter čipove (kategorije/pretraga) stilizuj kao hladne pilule: aktivan `bg-brand-600 text-white`, neaktivan `bg-sand text-ink border border-line`.

- [ ] **Step 3: Typecheck + lint + vizuelna provjera**

Run: `npx tsc --noEmit && npm run lint`; `npm run dev` → `/products`: čist grid, čipovi kao pilule; klik na proizvod → detalj ima veliku sliku, čist raspored, zeleni "Dodaj u korpu".

- [ ] **Step 4: Commit**

```bash
git add frontend/src/app/\(shop\)/products
git commit -m "feat(ui): clean product list + detail"
```

---

### Task 7: Korpa (stranica + drawer) + Checkout

**Files:**
- Modify: `frontend/src/components/shop/cart-drawer.tsx`
- Modify: `frontend/src/app/(shop)/cart/page.tsx`
- Modify: `frontend/src/app/(shop)/checkout/page.tsx`

**Interfaces:**
- Consumes: Faza 1 komponente/tokeni.

- [ ] **Step 1: Cart drawer**

U `cart-drawer.tsx`:
- Panel (linija 30): `bg-cream` (sada bijelo) OK; `border-black/5` → `border-line` (linije 31, 96).
- Stavke (linija 62): `ring-1 ring-black/5` → `border border-line`.
- Prazan-stanje krug (linija 44): ostaje `bg-sand text-muted`.

- [ ] **Step 2: Cart stranica + Checkout**

Otvori `cart/page.tsx` i `checkout/page.tsx`; primijeni iste mehaničke zamjene kao Task 6 Step 2 (`ring-1 ring-black/5` → `shadow-card`/`border border-line`, `font-extrabold` → `font-bold`, honey-CTA → zelena). OSM/Leaflet mapa i sva logika ostaju netaknuti.

- [ ] **Step 3: Typecheck + lint + vizuelna provjera**

Run: `npx tsc --noEmit && npm run lint`; `npm run dev` → dodaj proizvod, otvori drawer (čiste stavke), pređi na `/checkout`: čist formular, mapa radi, zeleni CTA "Naruči".

- [ ] **Step 4: Commit**

```bash
git add frontend/src/components/shop/cart-drawer.tsx frontend/src/app/\(shop\)/cart frontend/src/app/\(shop\)/checkout
git commit -m "feat(ui): clean cart drawer, cart page, checkout"
```

---

### Task 8: Narudžbe (kupac) + login/register

**Files:**
- Modify: `frontend/src/app/(shop)/orders/page.tsx`
- Modify: `frontend/src/app/(shop)/login/page.tsx`
- Modify: `frontend/src/app/(shop)/register/page.tsx`

**Interfaces:**
- Consumes: Faza 1 komponente/tokeni; `Field`/`Input` (već osvježeni).

- [ ] **Step 1: Zamjene**

Na sva tri ekrana primijeni mehaničke zamjene: `ring-1 ring-black/5` → `shadow-card`/`border border-line`, `font-extrabold` → `font-bold`, honey-CTA → zelena. Status pilule na `/orders` koriste postojeći `StatusBadge` — ne dirati logiku, samo ako panel ima topli prsten zamijeni `shadow-card`.

- [ ] **Step 2: Typecheck + lint + vizuelna provjera**

Run: `npx tsc --noEmit && npm run lint`; `npm run dev` → prijavi se (`mujo@example.com`/`mujo123`), otvori `/orders`: čiste kartice; `/login` i `/register` čiste forme.

- [ ] **Step 3: Commit**

```bash
git add frontend/src/app/\(shop\)/orders frontend/src/app/\(shop\)/login frontend/src/app/\(shop\)/register
git commit -m "feat(ui): clean orders, login, register"
```

---

### Task 9: Admin panel

**Files:**
- Modify: `frontend/src/components/admin/order-card.tsx`
- Modify: `frontend/src/app/admin/dashboard/page.tsx`
- Modify: `frontend/src/app/admin/orders/page.tsx`, `frontend/src/app/admin/orders/new/page.tsx`
- Modify: `frontend/src/app/admin/products/page.tsx`, `categories/page.tsx`, `producers/page.tsx`, `routes/page.tsx`
- Modify: `frontend/src/components/admin/*-form.tsx` (drawer forme)

**Interfaces:**
- Consumes: Faza 1 komponente/tokeni.

- [ ] **Step 1: Order-card**

U `order-card.tsx`:
- Wrapper (linija 30) `ring-1 ring-black/5` → `shadow-card`.
- `hover:bg-sand/40` (linija 35), `bg-sand/50` (linija 79), `bg-sand/60` (linija 118): ostaju (sada hladan neutral) — OK, ne dirati.
- `border-black/5` (linije 60, 94) → `border-line`.
- Napomena `bg-honey-100` (linija 111) ostaje — dozvoljena honey upotreba (istaknuto).

- [ ] **Step 2: Admin ekrani + drawer forme**

Prođi kroz svaki admin `page.tsx` i `components/admin/*-form.tsx`; primijeni mehaničke zamjene: `ring-1 ring-black/5` → `shadow-card`/`border border-line`, `font-extrabold` → `font-bold`. Tabele: redove razdvoji `border-line`. Drawer forme već koriste `Field`/`Input` (osvježeni u Task 4).

- [ ] **Step 3: Typecheck + lint + vizuelna provjera**

Run: `npx tsc --noEmit && npm run lint`; `npm run dev` → prijavi se kao `admin@epiljara.ba`/`admin123`, prođi `/admin/dashboard`, `/admin/orders`, `/admin/products` (+ otvori drawer "Novi proizvod"), `/admin/routes`. Sve čisto i dosljedno.

- [ ] **Step 4: Commit**

```bash
git add frontend/src/app/admin frontend/src/components/admin
git commit -m "feat(ui): clean admin panel"
```

---

## FAZA 3 — Animacije (bogato, `motion`)

### Task 10: Instaliraj `motion` + scroll-reveal na Početnoj i listi

**Files:**
- Modify: `frontend/package.json` (dependency)
- Modify: `frontend/src/app/providers.tsx` (global reduced-motion)
- Create: `frontend/src/components/ui/reveal.tsx`
- Modify: `frontend/src/app/(shop)/page.tsx`, `frontend/src/components/shop/product-grid.tsx`

**Interfaces:**
- Produces: `<Reveal delay?={number}>{children}</Reveal>` — wrapper koji fade-in-up animira djecu kad uđu u viewport.

- [ ] **Step 1: Instaliraj**

Run (iz `frontend/`): `npm install motion`
Expected: `motion` se pojavi u `dependencies`.

- [ ] **Step 1b: Globalni reduced-motion (`MotionConfig`)**

`motion` NE gasi svoje JS animacije preko CSS `@media` pravila — mora se reći eksplicitno. Omotaj providere u `<MotionConfig reducedMotion="user">` da SVE `motion` komponente (Reveal, cart drawer, toast) poštuju korisnikovu postavku. U `frontend/src/app/providers.tsx` dodaj `import { MotionConfig } from "motion/react";` i omotaj postojeće providere:

```tsx
  return (
    <QueryClientProvider client={queryClient}>
      <MotionConfig reducedMotion="user">
        <AuthProvider>
          <CartProvider>
            <ToastProvider>{children}</ToastProvider>
          </CartProvider>
        </AuthProvider>
      </MotionConfig>
    </QueryClientProvider>
  );
```

- [ ] **Step 2: Napravi `reveal.tsx`**

```tsx
"use client";

import { motion } from "motion/react";

/** Fade-in-up kad element uđe u viewport. Poštuje prefers-reduced-motion automatski (motion). */
export function Reveal({
  children,
  delay = 0,
  className,
}: {
  children: React.ReactNode;
  delay?: number;
  className?: string;
}) {
  return (
    <motion.div
      className={className}
      initial={{ opacity: 0, y: 16 }}
      whileInView={{ opacity: 1, y: 0 }}
      viewport={{ once: true, margin: "-40px" }}
      transition={{ duration: 0.4, delay, ease: [0.22, 1, 0.36, 1] }}
    >
      {children}
    </motion.div>
  );
}
```

- [ ] **Step 3: Primijeni na sekcije Početne**

U `page.tsx` uvezi `import { Reveal } from "@/components/ui/reveal";` i omotaj sekcije "Naši domaćini", "Kako funkcioniše" i "Delivery strip" u `<Reveal>`. Za grid domaćina, daj svakoj kartici stagger preko `delay={i * 0.06}` (mapiraj s indexom).

- [ ] **Step 4: Stagger na grid proizvoda**

U `product-grid.tsx` `ProductGrid`: omotaj svaku karticu u `<Reveal delay={Math.min(i, 8) * 0.04}>`:
```tsx
{products.map((p, i) => (
  <Reveal key={p.id} delay={Math.min(i, 8) * 0.04}>
    <ProductCard product={p} />
  </Reveal>
))}
```

- [ ] **Step 5: Typecheck + lint + vizuelna provjera**

Run: `npx tsc --noEmit && npm run lint`; `npm run dev` → skrolaj Početnu (sekcije "uplivaju"); `/products` (kartice se pojavljuju u nizu). U DevTools uključi "Reduce motion" i potvrdi da se animacije gase.

- [ ] **Step 6: Commit**

```bash
git add frontend/package.json frontend/package-lock.json frontend/src/components/ui/reveal.tsx frontend/src/app/\(shop\)/page.tsx frontend/src/components/shop/product-grid.tsx
git commit -m "feat(ui): motion library + scroll-reveal & staggered grid"
```

---

### Task 11: "Dodaj u korpu" — leteći efekat + poskok badža

**Files:**
- Create: `frontend/src/lib/cart/fly-to-cart.ts`
- Modify: `frontend/src/components/shop/product-card.tsx`
- Modify: `frontend/src/components/shop/cart-button.tsx`, `frontend/src/components/layout/mobile-tab-bar.tsx`

**Interfaces:**
- Produces: `flyToCart(fromEl: HTMLElement): void` — animira malu tačku/sliku od dugmeta ka `#cart-target` elementu; i CSS klasa `animate-bump` na badžu.

- [ ] **Step 1: Bump keyframe**

Dodaj u `globals.css` (uz shimmer):
```css
@keyframes bump {
  0% { transform: scale(1); }
  30% { transform: scale(1.35); }
  100% { transform: scale(1); }
}
.animate-bump { animation: bump 0.35s ease; }
```

- [ ] **Step 2: `fly-to-cart.ts`**

```ts
/** Leteća zelena tačka od izvornog dugmeta do elementa s id="cart-target". */
export function flyToCart(fromEl: HTMLElement) {
  if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;
  const target = document.getElementById("cart-target");
  if (!target) return;
  const a = fromEl.getBoundingClientRect();
  const b = target.getBoundingClientRect();
  const dot = document.createElement("span");
  dot.style.cssText = `position:fixed;left:${a.left + a.width / 2}px;top:${a.top + a.height / 2}px;width:14px;height:14px;border-radius:9999px;background:#2a7a3a;z-index:60;pointer-events:none;transform:translate(-50%,-50%)`;
  document.body.appendChild(dot);
  const dx = b.left + b.width / 2 - (a.left + a.width / 2);
  const dy = b.top + b.height / 2 - (a.top + a.height / 2);
  dot.animate(
    [
      { transform: "translate(-50%,-50%) scale(1)", opacity: 1 },
      { transform: `translate(calc(-50% + ${dx}px), calc(-50% + ${dy}px)) scale(0.3)`, opacity: 0.4 },
    ],
    { duration: 600, easing: "cubic-bezier(0.5,0,0.75,0)" },
  ).onfinish = () => {
    dot.remove();
    target.classList.add("animate-bump");
    setTimeout(() => target.classList.remove("animate-bump"), 400);
  };
}
```

- [ ] **Step 3: Označi metu badža**

U `cart-button.tsx` dodaj `id="cart-target"` na badž `<span>` (linija 16); isto u `mobile-tab-bar.tsx` na badž `<span>` (linija 50). Ako su oba prisutna na ekranu (desktop badž skriven na mobilnom i obrnuto), `getElementById` uzima prvi vidljivi — dovoljno; meta id ide na oba, browser vraća prvi u DOM-u, što je OK jer je samo jedan vidljiv.

  Napomena: da izbjegneš duple `id`, stavi `id="cart-target"` SAMO na `mobile-tab-bar` badž kad je `md:hidden`, a na `cart-button` badž (desktop) — dovoljno je staviti `id` na `cart-button` badž (vidljiv na `md+`) i ostaviti mobilni bez id-a; leteći efekat na telefonu cilja tab-bar preko dodatnog id-a `cart-target` samo ako desktop nije u DOM-u. Praktično: stavi `id="cart-target"` na `cart-button` badž; na `mobile-tab-bar` badž stavi `id="cart-target-mobile"` i u `flyToCart` probaj `getElementById("cart-target") ?? getElementById("cart-target-mobile")` i badž mete koji nađeš.

  Ažuriraj `fly-to-cart.ts` Step 2 liniju:
```ts
  const target = document.getElementById("cart-target") ?? document.getElementById("cart-target-mobile");
```

- [ ] **Step 4: Pozovi iz kartice**

U `product-card.tsx` `onClick` dugmeta "+" proslijedi event i pozovi `flyToCart`:
```tsx
onClick={(e) => {
  add(product);
  show(`${product.name} dodano u korpu`);
  flyToCart(e.currentTarget);
}}
```
Dodaj import: `import { flyToCart } from "@/lib/cart/fly-to-cart";`

- [ ] **Step 5: Typecheck + lint + vizuelna provjera**

Run: `npx tsc --noEmit && npm run lint`; `npm run dev` → klikni "+" na kartici: zelena tačka odleti ka korpi, badž poskoči. Provjeri i desktop i mobilni (tab bar). Uključi "Reduce motion" → nema letenja.

- [ ] **Step 6: Commit**

```bash
git add frontend/src/app/globals.css frontend/src/lib/cart/fly-to-cart.ts frontend/src/components/shop/product-card.tsx frontend/src/components/shop/cart-button.tsx frontend/src/components/layout/mobile-tab-bar.tsx
git commit -m "feat(ui): fly-to-cart animation + badge bump"
```

---

### Task 12: Pokret za cart drawer + toast

**Files:**
- Modify: `frontend/src/components/shop/cart-drawer.tsx`
- Modify: `frontend/src/components/ui/toast.tsx`

**Interfaces:**
- Consumes: `motion/react` iz Task 10.

- [ ] **Step 1: Animiraj cart drawer**

U `cart-drawer.tsx` uvezi `import { motion, AnimatePresence } from "motion/react";`. Zamijeni overlay+panel `div`-ove `motion` verzijama i omotaj cijeli `isOpen` blok u `<AnimatePresence>`. Ključno: ukloni rani `if (!isOpen) return null;` — umjesto toga renderuj `<AnimatePresence>{isOpen && (...)}</AnimatePresence>` da exit animacija radi.

```tsx
return (
  <AnimatePresence>
    {isOpen && (
      <div className="fixed inset-0 z-50">
        <motion.button
          aria-label="Zatvori"
          onClick={closeCart}
          className="absolute inset-0 bg-black/40"
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          transition={{ duration: 0.2 }}
        />
        <motion.div
          className="absolute right-0 top-0 flex h-full w-full max-w-md flex-col bg-cream shadow-xl"
          initial={{ x: "100%" }}
          animate={{ x: 0 }}
          exit={{ x: "100%" }}
          transition={{ type: "spring", stiffness: 380, damping: 40 }}
        >
          {/* ... postojeći sadržaj panela nepromijenjen ... */}
        </motion.div>
      </div>
    )}
  </AnimatePresence>
);
```
(Zadrži `useEffect` za Escape; on smije ostati iznad returna.)

- [ ] **Step 2: Animiraj toast-ove**

U `toast.tsx` uvezi `motion, AnimatePresence`; omotaj listu u `<AnimatePresence>` i pretvori svaki toast `div` u `motion.div`:
```tsx
<AnimatePresence>
  {toasts.map((t) => (
    <motion.div
      key={t.id}
      layout
      initial={{ opacity: 0, y: 20, scale: 0.9 }}
      animate={{ opacity: 1, y: 0, scale: 1 }}
      exit={{ opacity: 0, scale: 0.9 }}
      transition={{ duration: 0.2 }}
      className={cn(/* ... postojeće klase ... */)}
    >
      {/* ... ikona + poruka ... */}
    </motion.div>
  ))}
</AnimatePresence>
```

- [ ] **Step 3: Typecheck + lint + vizuelna provjera**

Run: `npx tsc --noEmit && npm run lint`; `npm run dev` → otvori/zatvori korpu (glatki slide-in/out); dodaj proizvod (toast uklizne, pa nestane). "Reduce motion" → trenutno bez klizanja.

- [ ] **Step 4: Commit**

```bash
git add frontend/src/components/shop/cart-drawer.tsx frontend/src/components/ui/toast.tsx
git commit -m "feat(ui): animated cart drawer + toasts"
```

---

## Završna provjera (nakon svih taskova)

- [ ] `npx tsc --noEmit` i `npm run lint` prolaze čisti.
- [ ] `npm run build` prolazi bez grešaka.
- [ ] Prođi cijeli tok kao gost i kao kupac: Početna → proizvodi → detalj → dodaj u korpu (leteći efekat) → checkout (mapa) → narudžba.
- [ ] Prođi admin: dashboard, narudžbe (promjena statusa), proizvodi (drawer forma), rute.
- [ ] Mobilni breakpoint (~390px): tab bar, drawer, kartice izgledaju čisto.
- [ ] "Reduce motion" gasi sve animacije.
```
