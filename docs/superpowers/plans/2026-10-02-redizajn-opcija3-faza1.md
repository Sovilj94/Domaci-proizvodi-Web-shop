# Redizajn „Opcija 3" — Faza 1: vizuelni sistem i početna

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prebaciti aplikaciju na svijetlu paletu iz mockupa i prepraviti header, donju
navigaciju, karticu proizvoda i početnu stranicu tako da izgledaju kao screenshot „Opcija 3".

**Architecture:** Tokeni se pretaču u `globals.css` — mijenjaju se **vrijednosti**, ne imena,
pa se cijela aplikacija pomjeri na novu paletu iz jednog fajla. Zatim se prepravlja struktura
tamo gdje boja nije dovoljna: početna gubi dvije tamne sekcije, header dobija traku kategorija
umjesto plutanja nad herojem, donja traka dobija peti tab.

**Tech Stack:** Next.js 15 (App Router), React 19, TypeScript, Tailwind CSS v4 (`@theme` u CSS-u,
bez `tailwind.config`), TanStack Query, lucide-react.

**Spec:** `docs/superpowers/specs/2026-10-02-redizajn-opcija3-design.md`

**Vizuelni izvor istine:** `C:\Users\soviljn\Desktop\E-piljara Grocery UI Showcase complete.png`

## Global Constraints

- **Grana:** `redizajn-opcija3` (već napravljena od `main`).
- **Radni direktorij:** `C:\Users\soviljn\Desktop\epiljara-backend-wt` — **worktree**, ne
  „Kumov Web Shop" koji je 27 commita iza.
- **Frontend komande se pokreću iz `frontend/`.**
- **Sav tekst vidljiv korisniku je na srpskom.**
- **`npm run build` je obavezan** poslije svake task grupe. `tsc` i `eslint` u ovom repozitoriju
  prolaze čisto na greškama koje se pojave tek u Next-ovom prerender prolazu.
- **Ne ostavljati neiskorištene importe** — `eslint` ih prijavljuje kao greške.
- **Kontrast:** svaki par teksta i podloge mora proći WCAG AA (4.5:1). Bijeli tekst na `sand`
  (`#eef5ee`) **pada** i ne smije se koristiti.
- **Ne izmišljati podatke** kojih backend nema (ocjene, nutritivne vrijednosti, popusti).
- **Ne dirati** `src/app/admin/**`, `src/lib/api/**`, `src/server/**`, backend.
- **Dnevnik** `docs/superpowers/plans/2026-10-02-redizajn-opcija3-dnevnik.md` se dopisuje
  poslije svakog taska (Step „Dopiši dnevnik").

## Struktura fajlova

| Fajl | Odgovornost | Task |
|---|---|---|
| `src/app/globals.css` | tokeni, keyframes, globalni stilovi | 1 |
| `src/components/ui/button.tsx` | varijante dugmadi | 2 |
| `src/components/ui/section-header.tsx` | **nov** — naslov sekcije + „Prikaži sve →" | 2 |
| `src/components/layout/logo.tsx` | logo, dvije podloge | 2 |
| `src/components/shop/product-card.tsx` | **jedini** izgled proizvoda u aplikaciji | 3 |
| `src/components/layout/category-rail.tsx` | **nov** — traka kategorija ispod headera | 4 |
| `src/components/layout/shop-navbar.tsx` | header | 4 |
| `src/components/layout/mobile-tab-bar.tsx` | donja navigacija, 5 tabova | 5 |
| `src/components/shop/benefit-cards.tsx` | **nov** — četiri benefita, dijeli ih više ekrana | 6 |
| `src/components/shop/category-tile.tsx` | **nov** — pločica kategorije | 6 |
| `src/app/(shop)/page.tsx` | početna | 7 |
| `src/components/shop/category-chips.tsx` | **briše se** — zamjenjuje ga `category-rail` | 4 |

---

### Task 1: Tokeni

**Files:**
- Modify: `frontend/src/app/globals.css`

**Interfaces:**
- Consumes: ništa
- Produces: Tailwind utilitiji `bg-cream`, `bg-surface`, `bg-sand`, `bg-brand-600`,
  `text-ink`, `text-muted`, `border-line`, `rounded-card` sa novim vrijednostima.
  Imena se **ne mijenjaju** — 33 fajla ih već uvozi.

- [ ] **Step 1: Zamijeniti brand skalu**

U `@theme` bloku zamijeniti cijelu `--color-brand-*` skalu ovom, izvedenom oko `600`:

```css
  /*
   * Brand — tamno zelena iz mockupa „Opcija 3". Skala je izvedena oko 600, koji nosi
   * ispunu dugmadi: bijelo na #0e5b38 daje 8.28:1. 700 je hover, 500 ide na ikone.
   */
  --color-brand-50: #f1f7f3;
  --color-brand-100: #dcebe2;
  --color-brand-200: #bbd8c7;
  --color-brand-300: #8dbda2;
  --color-brand-400: #589b79;
  --color-brand-500: #2d7a55;
  --color-brand-600: #0e5b38;
  --color-brand-700: #083f29;
  --color-brand-800: #06301f;
  --color-brand-900: #042217;
```

- [ ] **Step 2: Zamijeniti neutrale**

Zamijeniti postojeće `--color-cream`, `--color-sand`, `--color-ink`, `--color-muted`,
`--color-line`, `--color-surface` ovim:

```css
  /* Neutrali — svijetli i skoro neutralni. Mockup nema tople krem tonove starog izgleda. */
  --color-cream: #f8f5ed;   /* pozadina stranice */
  --color-sand: #eef5ee;    /* svijetlo zelena površina — ikone benefita, čipovi */
  --color-ink: #171914;     /* tekst — 16.3:1 na cream */
  --color-muted: #5f6459;   /* sekundarni tekst — 5.66:1 na cream */
  --color-line: #e5e1d7;    /* hairline granice */
  --color-surface: #ffffff; /* kartice */
```

- [ ] **Step 3: Suziti radijus i smiriti sjenke**

```css
  --radius-card: 0.875rem; /* 14px */

  /* Elevacije — mockup ima jedva vidljive sjenke, nosi ih border. */
  --shadow-sm: 0 1px 2px rgba(23, 25, 20, 0.05);
  --shadow-card: 0 1px 3px rgba(23, 25, 20, 0.06);
  --shadow-md: 0 4px 14px rgba(23, 25, 20, 0.07);
  --shadow-lg: 0 20px 40px rgba(23, 25, 20, 0.12);
```

- [ ] **Step 4: Ažurirati komentar na vrhu fajla**

Zamijeniti uvodni blok komentara (prvih ~10 linija poslije `@import "tailwindcss";`):

```css
/*
 * E piljara design tokeni — izgled „Opcija 3": svijetla podloga, tamno zelena kao
 * jedina jaka boja, bijele kartice, diskretni borderi.
 * Tailwind v4 čita ove iz @theme i generiše utilitije
 * (bg-brand-600, text-muted, rounded-card, shadow-card, font-display, ...).
 *
 * Imena tokena su namjerno ostala ista (brand-*, sand, ink…) — 33 fajla ih koristi,
 * pa se cijeli izgled mijenja ovdje umjesto kroz stotine izmjena po komponentama.
 * Posljedica: `sand` od sada znači SVIJETLO ZELENU, ne pijesak. Ime je ostalo zbog
 * broja pozivalaca, ali ko čita samo ime očekivaće topli ton.
 */
```

- [ ] **Step 5: Obrisati animiranu traku**

Obrisati `@keyframes traka`, `.animate-traka` i `@media (prefers-reduced-motion: reduce)` blok
koji se odnosi **samo** na `.animate-traka` (onaj sa komentarom „Bez ovoga traka nastavi da se
vrti"). **Zadržati** veliki `prefers-reduced-motion` blok na dnu fajla koji gasi sve animacije.

Razlog: beskonačno pomjerana traka ne postoji na screenshotu; zamjenjuje je statična traka
kategorija u headeru (Task 4).

- [ ] **Step 6: Osvježiti skeleton na novu podlogu**

```css
/* Shimmer skeleton površina */
.skeleton {
  background: linear-gradient(90deg, #e9e5db 25%, #f4f1e9 37%, #e9e5db 63%);
  background-size: 200% 100%;
  animation: var(--animate-shimmer);
}
```

- [ ] **Step 7: Provjeriti da se aplikacija diže**

Run (iz `frontend/`): `npx tsc --noEmit && npm run lint && npm run build`
Expected: sve tri prolaze. CSS greška u Tailwind v4 `@theme` bloku se vidi u `build`.

Zatim otvoriti `http://localhost:3000` i pogledati: pozadina je svijetla, dugmad tamno zelena.
Stranica će i dalje imati tamne sekcije — to je Task 7, ne greška.

- [ ] **Step 8: Dopiši dnevnik**

U `docs/superpowers/plans/2026-10-02-redizajn-opcija3-dnevnik.md`, na kraj „Hronologije":

```markdown
- **Task 1 (tokeni) gotov.** `globals.css` na novoj paleti; obrisana `.animate-traka`.
  Tamne sekcije na početnoj još stoje — one su Task 7.
```

- [ ] **Step 9: Commit**

```bash
git add frontend/src/app/globals.css docs/superpowers/plans/2026-10-02-redizajn-opcija3-dnevnik.md
git commit -m "style: pretočiti design tokene na paletu „Opcija 3\"

Vrijednosti se mijenjaju, imena ostaju — brand-* je sada #0e5b38, cream
je svijetli #f8f5ed, sand je svijetlo zelena a ne pijesak. Imena su
zadržana jer ih uvozi 33 fajla.

Obrisana .animate-traka: beskonačno pomjerana traka kategorija ne postoji
na mockupu, zamjenjuje je statična traka u headeru.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 2: Dugmad, logo i zaglavlje sekcije

**Files:**
- Modify: `frontend/src/components/ui/button.tsx`
- Modify: `frontend/src/components/layout/logo.tsx`
- Create: `frontend/src/components/ui/section-header.tsx`

**Interfaces:**
- Consumes: tokene iz Taska 1
- Produces:
  - `buttonVariants({ variant, size })` — `variant: "primary" | "secondary" | "ghost" | "danger" | "honey"`, `size: "sm" | "md" | "lg" | "icon"`. **Potpis se ne mijenja** (koristi ga 12 mjesta), mijenjaju se samo klase.
  - `<SectionHeader naslov={string} opis?={string} link?={string} linkTekst?={string} />`

- [ ] **Step 1: Prepraviti dugmad na pravougaonik sa zaobljenjem**

U `button.tsx` zamijeniti `base` i `variants`:

```tsx
/*
 * Mockup „Opcija 3" nosi zaobljeni pravougaonik (10px), ne pilulu. Puna pilula je
 * pripadala starom rustikalnom izgledu; ovdje bi odudarala od kartica koje su
 * zaobljene na 14px.
 */
const base =
  "inline-flex items-center justify-center gap-2 rounded-[10px] font-medium transition active:scale-[0.98] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-600 focus-visible:ring-offset-2 focus-visible:ring-offset-cream disabled:pointer-events-none disabled:opacity-50";

const variants: Record<Variant, string> = {
  primary: "bg-brand-600 text-white hover:bg-brand-700",
  secondary: "bg-surface text-ink border border-line hover:bg-sand",
  ghost: "text-ink hover:bg-sand",
  danger: "bg-red-700 text-white hover:bg-red-800",
  /* Amber ispuna nosi TAMAN tekst — bijelo na honey-500 daje 2.9:1 i pada AA. */
  honey: "bg-honey-500 text-ink hover:bg-honey-600",
};
```

`sizes` i `Variant`/`Size` tipovi ostaju nepromijenjeni.

- [ ] **Step 2: Logo — zeleno umjesto amber**

U `logo.tsx` zamijeniti dio sa `honey` bojama. Riječ „piljara" u mockupu nije amber nego
dio istog zelenog sistema:

```tsx
        E{" "}
        {/*
          Na svijetloj podlozi riječ nosi brand-600 (8.28:1 na cream). Na tamnoj bi
          brand-600 pao, pa ide brand-300 (#8dbda2) koji na night daje 7.2:1.
        */}
        <span className={tone === "dark" ? "text-brand-300" : "text-brand-600"}>
          piljara
        </span>
```

Obrisati stari komentar o `honey-500`/`honey-600` iznad tog spana.

- [ ] **Step 3: Napisati `section-header.tsx`**

```tsx
import Link from "next/link";
import { ArrowRight } from "lucide-react";
import { cn } from "@/lib/utils";

/**
 * Zaglavlje sekcije sa mockupa: serif naslov lijevo, „Prikaži sve →" desno.
 * Nema nadnaslova u amberu — to je pripadalo starom izgledu i na screenshotu ga nema.
 */
export function SectionHeader({
  naslov,
  opis,
  link,
  linkTekst = "Prikaži sve",
  className,
}: {
  naslov: React.ReactNode;
  opis?: string;
  link?: string;
  linkTekst?: string;
  className?: string;
}) {
  return (
    <div className={cn("mb-6 flex flex-wrap items-end justify-between gap-x-6 gap-y-2", className)}>
      <div>
        <h2 className="font-display text-2xl font-bold leading-tight text-ink sm:text-3xl">
          {naslov}
        </h2>
        {opis && <p className="mt-1.5 max-w-xl text-sm text-muted">{opis}</p>}
      </div>
      {link && (
        <Link
          href={link}
          className="inline-flex shrink-0 items-center gap-1 text-sm font-medium text-brand-600 transition hover:text-brand-700"
        >
          {linkTekst}
          <ArrowRight className="h-4 w-4" />
        </Link>
      )}
    </div>
  );
}
```

- [ ] **Step 4: Provjera**

Run (iz `frontend/`): `npx tsc --noEmit && npm run lint && npm run build`
Expected: sve prolazi. `SectionHeader` još niko ne koristi — to je očekivano, `eslint` ne
prijavljuje neiskorištene **eksporte**, samo neiskorištene importe.

- [ ] **Step 5: Dopiši dnevnik**

```markdown
- **Task 2 gotov.** Dugmad na 10px radijusu umjesto pilule, logo „piljara" iz amber u
  brand zelenu, nov `ui/section-header.tsx`.
```

- [ ] **Step 6: Commit**

```bash
git add frontend/src/components/ui/button.tsx frontend/src/components/layout/logo.tsx frontend/src/components/ui/section-header.tsx docs/superpowers/plans/2026-10-02-redizajn-opcija3-dnevnik.md
git commit -m "style: dugmad, logo i zaglavlje sekcije po mockupu

Dugmad prelaze sa pune pilule na 10px radijus, da stoje uz kartice od
14px. Riječ „piljara\" iz amber u brand zelenu, sa brand-300 na tamnoj
podlozi gdje brand-600 pada ispod AA.

Novi SectionHeader nosi serif naslov i „Prikaži sve →\" kao na mockupu,
bez amber nadnaslova koji je pripadao starom izgledu.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 3: Kartica proizvoda

**Files:**
- Modify: `frontend/src/components/shop/product-card.tsx`

**Interfaces:**
- Consumes: `buttonVariants` nije potreban (kartica ima svoje dugme), tokene iz Taska 1
- Produces: `<ProductCard product={Product} />` — **`tone` prop se uklanja.** Pozivaoci
  koji su slali `tone="dark"` moraju se ispraviti u Tasku 7.

**Zašto se `tone` uklanja:** mockup nema tamnu podlogu ni na jednoj kartici. Zadržati mrtvu
granu znači zadržati i `night-card`, `muted-dark` i dvostruku tabelu boja koju niko ne koristi.

- [ ] **Step 1: Potvrditi pozivaoce**

Run (iz `frontend/`): `grep -rn "ProductCard" src --include="*.tsx"`

Očekivano (provjereno 2026-10-02): `tone="dark"` se šalje na **tačno dva mjesta**, oba u
`src/app/(shop)/page.tsx` (linije 285 i 291). `product-grid.tsx` ga ne šalje.
Ako grep pokaže drugačije, ispraviti i ta mjesta u Stepu 3.

- [ ] **Step 2: Napisati novu karticu**

Zamijeniti **cio sadržaj** `product-card.tsx`:

```tsx
"use client";

import Link from "next/link";
import { Plus } from "lucide-react";
import type { Product } from "@/types/domain";
import { SmartImage } from "@/components/ui/smart-image";
import { useCart } from "@/lib/cart/cart-context";
import { flyToCart } from "@/lib/cart/fly-to-cart";
import { useToast } from "@/components/ui/toast";
import { formatPrice, unitSuffix } from "@/lib/utils";

/**
 * Jedini izgled proizvoda u aplikaciji — koriste ga početna, /products, „Slični
 * proizvodi" i (od Faze 5) Omiljeni. Ako se kartica negdje crta ručno, granica je pukla.
 *
 * Gornji desni ugao slike je namjerno prazan: tu ide srce za omiljene, koje stiže u
 * Fazi 5. Mjesto mu je apsolutno pozicionirano pa se raspored neće pomjeriti kad se
 * uključi. Do tada se ne renderuje ništa — onemogućeno srce koje ništa ne radi je gore
 * od praznog ugla.
 */
export function ProductCard({ product }: { product: Product }) {
  const { add } = useCart();
  const { show } = useToast();
  const soldOut = !product.isAvailable || product.stock <= 0;

  return (
    <div className="group flex h-full flex-col overflow-hidden rounded-card border border-line bg-surface transition hover:shadow-md">
      <Link
        href={`/products/${product.slug}`}
        className="relative block aspect-square overflow-hidden bg-sand"
      >
        <SmartImage
          src={product.imageUrl}
          alt={product.name}
          className="transition duration-500 group-hover:scale-105"
        />
        <span className="absolute left-2.5 top-2.5 rounded-full bg-surface/90 px-2.5 py-1 text-[11px] font-medium text-ink backdrop-blur">
          {product.isFeatured && !soldOut ? "Izdvojeno" : product.categoryName}
        </span>
        {soldOut && (
          <div className="absolute inset-0 flex items-center justify-center bg-surface/80 text-sm font-medium text-ink">
            Trenutno nedostupno
          </div>
        )}
      </Link>

      <div className="flex flex-1 flex-col p-3 sm:p-3.5">
        <Link
          href={`/products/${product.slug}`}
          className="text-sm font-semibold leading-snug text-ink transition hover:text-brand-700 sm:text-base"
        >
          {product.name}
        </Link>
        <p className="mt-0.5 truncate text-xs text-muted">
          {product.producer?.name}
          {product.producer?.village ? `, ${product.producer.village}` : ""}
        </p>

        <div className="mt-3 flex items-end justify-between gap-2">
          <div className="flex items-baseline gap-0.5">
            <span className="text-base font-bold text-ink sm:text-lg">
              {formatPrice(product.price)}
            </span>
            <span className="text-xs text-muted">{unitSuffix(product.unit)}</span>
          </div>
        </div>

        <button
          type="button"
          disabled={soldOut}
          onClick={(e) => {
            const btn = e.currentTarget;
            add(product);
            show(`${product.name} dodano u korpu`);
            requestAnimationFrame(() => flyToCart(btn));
          }}
          aria-label={`Dodaj ${product.name} u korpu`}
          className="mt-3 inline-flex h-10 w-full items-center justify-center gap-1.5 rounded-[10px] bg-brand-600 text-sm font-medium text-white transition hover:bg-brand-700 active:scale-[0.98] disabled:cursor-not-allowed disabled:bg-brand-600/30 disabled:text-white/70"
        >
          <Plus className="h-4 w-4" />
          {/*
            Na telefonu je kartica ~160px i natpis bi se prelomio, pa ostaje samo „+".
            Dugme je i dalje puna širina — tap target je cijela donja ivica kartice.
          */}
          <span className="hidden sm:inline">Dodaj u korpu</span>
        </button>
      </div>
    </div>
  );
}
```

- [ ] **Step 3: Ukloniti `tone` kod pozivalaca**

U `src/app/(shop)/page.tsx` obrisati `tone="dark"` sa oba `<ProductCard …/>`:

```tsx
                    <ProductCard product={p} />
```
```tsx
                  <ProductCard key={p.id} product={p} />
```

Taj fajl se ionako prepisuje u Tasku 7 — ovdje je dovoljno da build prođe sada.

- [ ] **Step 4: Prilagoditi mrežu u `product-grid.tsx`**

`MREZA` je razmaknuta na `gap-8` uz komentar „kartica više nema okvir ni sjenku". Nova
kartica **ima** border, pa je taj komentar od sada netačan a razmak prevelik. Zamijeniti
oba:

```tsx
/* Kartica nosi vlastiti border, pa razmak više ne mora da je razdvaja — gap-8 je
   ostao iz izgleda u kojem kartica nije imala okvir. Mockup drži kartice blizu. */
const MREZA = "grid grid-cols-2 gap-3 md:grid-cols-3 lg:grid-cols-4 lg:gap-4";
```

- [ ] **Step 5: Provjera da ništa nije ostalo**

Run: `grep -rn "tone=" src --include="*.tsx" | grep -i productcard`
Expected: **nijedan pogodak**.

- [ ] **Step 6: Provjera build-a**

Run (iz `frontend/`): `npx tsc --noEmit && npm run lint && npm run build`
Expected: sve prolazi. Ako `tsc` prijavi `Property 'tone' does not exist`, Step 3 nije
dovršen.

- [ ] **Step 7: Pogledati rezultat**

Otvoriti `http://localhost:3000/products` i provjeriti: kartica je bijela sa borderom,
slika 1:1, badge gore lijevo, zeleno dugme preko cijele širine. Suziti prozor na 375px —
dugme pokazuje samo „+", dvije kartice stoje u redu.

- [ ] **Step 8: Dopiši dnevnik**

```markdown
- **Task 3 gotov.** Kartica proizvoda po mockupu: bijela sa borderom, slika 1:1, badge
  gore lijevo, dugme pune širine. **`tone` prop uklonjen** — mockup nema tamnu karticu.
  Gornji desni ugao ostavljen prazan za srce iz Faze 5.
```

- [ ] **Step 9: Commit**

```bash
git add frontend/src/components/shop/ "frontend/src/app/(shop)/page.tsx" docs/superpowers/plans/2026-10-02-redizajn-opcija3-dnevnik.md
git commit -m "feat: kartica proizvoda po mockupu „Opcija 3\"

Bijela kartica sa borderom i slikom 1:1, badge gore lijevo, cijena sa
jedinicom manjim tekstom, zeleno dugme preko cijele širine.

Uklonjen `tone` prop: mockup nema tamnu podlogu ni na jednoj kartici, pa
je tamna grana bila mrtav kod koji je držao night-card i muted-dark u
upotrebi.

Gornji desni ugao slike ostaje prazan — tu ide srce za omiljene iz Faze 5.
Mjesto je apsolutno pozicionirano pa se raspored neće pomjeriti.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 4: Header i traka kategorija

**Files:**
- Create: `frontend/src/components/layout/category-rail.tsx`
- Modify: `frontend/src/components/layout/shop-navbar.tsx`
- Delete: `frontend/src/components/shop/category-chips.tsx`

**Interfaces:**
- Consumes: `useCategories()` iz `@/lib/api/hooks` → `{ data?: Category[], isLoading: boolean }`;
  `Category` ima `{ id, name, slug, imageUrl?, sortOrder, productCount? }`
- Produces: `<CategoryRail />` — bez propova, sam čita kategorije

- [ ] **Step 1: Potvrditi da je `category-chips.tsx` mrtav kod**

Run: `grep -rn "category-chips\|CategoryChips" src --include="*.tsx"`

Očekivano (provjereno 2026-10-02): **jedini pogodak je sama definicija** u
`src/components/shop/category-chips.tsx`. Komponentu ne uvozi nijedan fajl — već je mrtav
kod. Brisanje je zato bez posljedica i ne traži čišćenje importa nigdje.

**Ako grep pokaže pozivaoca**, ne brisati fajl — zabilježiti i preskočiti Step 7.

- [ ] **Step 2: Napisati `category-rail.tsx`**

```tsx
"use client";

import Link from "next/link";
import Image from "next/image";
import { usePathname, useSearchParams } from "next/navigation";
import { LayoutGrid } from "lucide-react";
import { useCategories } from "@/lib/api/hooks";
import { cn } from "@/lib/utils";

/**
 * Traka kategorija ispod headera, kao na mockupu: okrugla slika + naziv, vodoravno
 * skrolovanje na telefonu. Zamjenjuje staru `category-chips` i obrisanu `.animate-traka`.
 *
 * Imena su NAŠA stvarna (Povrće, Voće, Mliječni proizvodi, Meso i suhomesnato, Jaja,
 * Med i pčelinji proizvodi, Zimnica i džemovi) — mockupovih „Pekara/Zdrava hrana/Piće"
 * u bazi nema. Redoslijed ide po `sortOrder`, koji backend već vraća sortiran.
 */
export function CategoryRail() {
  const { data, isLoading } = useCategories();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const aktivna = pathname === "/products" ? searchParams.get("category") : null;

  if (isLoading) {
    return (
      <div className="flex gap-5 overflow-hidden px-4 py-2.5">
        {Array.from({ length: 8 }).map((_, i) => (
          <div key={i} className="flex shrink-0 flex-col items-center gap-1.5">
            <div className="skeleton h-11 w-11 rounded-full" />
            <div className="skeleton h-2.5 w-12 rounded" />
          </div>
        ))}
      </div>
    );
  }

  if (!data?.length) return null;

  const stavke = [
    { slug: null, name: "Sve", imageUrl: undefined as string | undefined },
    ...data.map((c) => ({ slug: c.slug, name: c.name, imageUrl: c.imageUrl })),
  ];

  return (
    <nav aria-label="Kategorije" className="no-scrollbar overflow-x-auto">
      <ul className="mx-auto flex max-w-6xl items-start gap-5 px-4 py-2.5">
        {stavke.map((s) => {
          const active = s.slug === aktivna;
          return (
            <li key={s.slug ?? "sve"} className="shrink-0">
              <Link
                href={s.slug ? `/products?category=${s.slug}` : "/products"}
                className="flex w-16 flex-col items-center gap-1.5"
              >
                <span
                  className={cn(
                    "relative grid h-11 w-11 place-items-center overflow-hidden rounded-full border-2 bg-sand transition",
                    active ? "border-brand-600" : "border-transparent hover:border-brand-200",
                  )}
                >
                  {s.imageUrl ? (
                    <Image src={s.imageUrl} alt="" fill sizes="44px" className="object-cover" />
                  ) : (
                    <LayoutGrid className="h-5 w-5 text-brand-600" />
                  )}
                </span>
                <span
                  className={cn(
                    "line-clamp-2 text-center text-[11px] leading-tight",
                    active ? "font-semibold text-brand-700" : "text-muted",
                  )}
                >
                  {s.name}
                </span>
              </Link>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}
```

- [ ] **Step 3: Prepisati `shop-navbar.tsx`**

Mockup nema providan navbar nad fotografijom — header je svijetao i `sticky` svuda,
uključujući početnu. Time otpada sva `nadHerojem` logika: state `skrolovano`, `useEffect`
na skrol, gradijentna prekrivka i dvostruke tabele klasa. Otpada i čip lokacije
(„Banja Luka"), kojeg na mockupu nema.

Zamijeniti **cio sadržaj** fajla:

```tsx
"use client";

import Link from "next/link";
import { Suspense, useState } from "react";
import { usePathname, useRouter } from "next/navigation";
import { LayoutDashboard, LogOut, Menu, Search, User, X } from "lucide-react";
import { Logo } from "@/components/layout/logo";
import { CategoryRail } from "@/components/layout/category-rail";
import { CartButton } from "@/components/shop/cart-button";
import { useAuth } from "@/lib/auth/auth-context";

function ShopNavbarInner() {
  const [open, setOpen] = useState(false);
  const [q, setQ] = useState("");
  const { user, isAdmin, logout, loading } = useAuth();
  const router = useRouter();
  const pathname = usePathname();
  // Stranica proizvoda ima svoju živu pretragu — ne dupliraj je u headeru.
  const hideSearch = pathname === "/products";

  function search(e: React.FormEvent) {
    e.preventDefault();
    const t = q.trim();
    router.push(t ? `/products?search=${encodeURIComponent(t)}` : "/products");
    setOpen(false);
  }

  const searchInputClass =
    "w-full rounded-[10px] border border-line bg-surface py-2.5 pl-9 pr-4 text-sm transition placeholder:text-muted focus:border-brand-400 focus:outline-none focus:ring-2 focus:ring-brand-600/20";

  return (
    <header className="sticky top-0 z-40 border-b border-line bg-cream/95 backdrop-blur">
      <div className="mx-auto max-w-6xl px-4">
        <div className="flex h-16 items-center gap-3">
          <Logo />

          {/* Pretraga na desktopu */}
          {!hideSearch && (
            <form onSubmit={search} className="relative mx-2 hidden max-w-md flex-1 md:block">
              <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted" />
              <input
                value={q}
                onChange={(e) => setQ(e.target.value)}
                placeholder="Pretraži proizvode…"
                className={searchInputClass}
              />
            </form>
          )}

          <div className="flex flex-1 items-center justify-end gap-1 md:flex-none">
            {isAdmin && (
              <Link
                href="/admin/dashboard"
                className="mr-1 hidden items-center gap-1.5 rounded-[10px] bg-ink px-3 py-1.5 text-xs font-medium text-cream transition hover:bg-ink/85 sm:inline-flex"
              >
                <LayoutDashboard className="h-3.5 w-3.5" />
                Admin
              </Link>
            )}
            <CartButton />

            {!loading &&
              (user ? (
                <div className="hidden items-center gap-1 sm:flex">
                  <Link
                    href="/orders"
                    className="flex items-center gap-2 rounded-[10px] px-3 py-2 text-sm font-medium text-ink transition hover:bg-sand"
                  >
                    <User className="h-4 w-4" />
                    {user.name.split(" ")[0]}
                  </Link>
                  <button
                    onClick={logout}
                    aria-label="Odjava"
                    className="grid h-10 w-10 place-items-center rounded-[10px] text-muted transition hover:bg-sand hover:text-ink"
                  >
                    <LogOut className="h-4 w-4" />
                  </button>
                </div>
              ) : (
                <Link
                  href="/login"
                  className="hidden rounded-[10px] bg-brand-600 px-5 py-2 text-sm font-medium text-white transition hover:bg-brand-700 sm:inline-flex"
                >
                  Prijava
                </Link>
              ))}

            <button
              onClick={() => setOpen((v) => !v)}
              aria-label="Meni"
              className="grid h-10 w-10 place-items-center rounded-[10px] text-ink transition hover:bg-sand md:hidden"
            >
              {open ? <X className="h-5 w-5" /> : <Menu className="h-5 w-5" />}
            </button>
          </div>
        </div>

        {/* Pretraga na telefonu */}
        {!hideSearch && (
          <form onSubmit={search} className="relative pb-3 md:hidden">
            <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted" />
            <input
              value={q}
              onChange={(e) => setQ(e.target.value)}
              placeholder="Pretraži proizvode…"
              className={searchInputClass}
            />
          </form>
        )}
      </div>

      {/* Traka kategorija — puna širina, sama se skroluje */}
      <div className="border-t border-line">
        <CategoryRail />
      </div>

      {/*
        Meni naloga na telefonu. Puni drawer sa svim stavkama (Kategorije, O nama,
        Kontakt…) stiže u Fazi 4; dotle ovdje stoji samo ono što već postoji.
      */}
      {open && (
        <div className="border-t border-line bg-cream md:hidden">
          <nav className="mx-auto flex max-w-6xl flex-col gap-1 px-4 py-3">
            {user ? (
              <>
                {isAdmin && (
                  <Link
                    href="/admin/dashboard"
                    onClick={() => setOpen(false)}
                    className="rounded-[10px] px-3 py-2.5 text-sm font-medium text-ink hover:bg-sand"
                  >
                    Admin panel
                  </Link>
                )}
                <Link
                  href="/orders"
                  onClick={() => setOpen(false)}
                  className="rounded-[10px] px-3 py-2.5 text-sm font-medium text-ink hover:bg-sand"
                >
                  Moje narudžbe
                </Link>
                <button
                  onClick={() => {
                    logout();
                    setOpen(false);
                  }}
                  className="rounded-[10px] px-3 py-2.5 text-left text-sm font-medium text-red-700 hover:bg-sand"
                >
                  Odjava
                </button>
              </>
            ) : (
              <Link
                href="/login"
                onClick={() => setOpen(false)}
                className="rounded-[10px] bg-brand-600 px-3 py-2.5 text-center text-sm font-medium text-white"
              >
                Prijava
              </Link>
            )}
          </nav>
        </div>
      )}
    </header>
  );
}

/*
 * CategoryRail čita `useSearchParams()`. Bez <Suspense> oko njega `npm run build` pada
 * u prerender prolazu, dok tsc i eslint prolaze čisto — u ovom repozitoriju je to već
 * oborilo /login, /register i /admin/dostava.
 *
 * Visina fallbacka drži 64px gornjeg reda + ~42px trake; bez nje stranica skoči pri
 * hidraciji.
 */
export function ShopNavbar() {
  return (
    <Suspense
      fallback={<div className="sticky top-0 z-40 h-[106px] border-b border-line bg-cream" />}
    >
      <ShopNavbarInner />
    </Suspense>
  );
}
```

- [ ] **Step 4: Provjeriti da je `cn` prestao da se uvozi**

Novi navbar ne koristi `cn`. Run: `grep -n "cn(" src/components/layout/shop-navbar.tsx`
Expected: **nijedan pogodak**. Ako import `cn` ostane, `eslint` pada.

- [ ] **Step 5: Provjeriti da `pathname === "/"` više ne mijenja header**

Run: `grep -n "nadHerojem\|skrolovano\|MapPin\|locationChip" src/components/layout/shop-navbar.tsx`
Expected: **nijedan pogodak**.

- [ ] **Step 6: Obrisati `category-chips.tsx`**

Samo ako je Step 1 potvrdio da ga ne uvozi nijedan fajl.

```bash
rm frontend/src/components/shop/category-chips.tsx
```

**Ništa drugo se ne dira** — nema importa koji treba čistiti. Ulogu te komponente od sada
nosi `CategoryRail`.

- [ ] **Step 7: Provjera**

Run (iz `frontend/`): `npx tsc --noEmit && npm run lint && npm run build`
Expected: sve prolazi.

**Ako `build` padne sa `useSearchParams() should be wrapped in a suspense boundary`** —
Step 5 nije dovršen ili je `Suspense` na pogrešnom nivou.

- [ ] **Step 8: Pogledati rezultat**

`http://localhost:3000` — header je svijetao i sticky, ispod njega traka sa 8 okruglih
kategorija. Skrolati: header ostaje, ne mijenja boju. Na 375px traka se skroluje postrance
bez vidljivog scrollbara i **stranica se ne pomjera vodoravno**.

- [ ] **Step 9: Dopiši dnevnik**

```markdown
- **Task 4 gotov.** Nova `layout/category-rail.tsx` (okrugle slike, naše stvarne kategorije,
  aktivna iz `?category=`). Header više ne pluta nad herojem — svijetao i sticky svuda;
  obrisani `skrolovano`, `nadHerojem`, gradijent i `locationChip`. `ShopNavbar` je sada
  omotan `<Suspense>` jer traka čita `useSearchParams()`. Obrisana `shop/category-chips.tsx`.
```

- [ ] **Step 10: Commit**

```bash
git add -A frontend/src/components/layout/ frontend/src/components/shop/ docs/superpowers/plans/2026-10-02-redizajn-opcija3-dnevnik.md
git commit -m "feat: header sa trakom kategorija po mockupu

Header prestaje da pluta providan nad herojem — na mockupu je svijetao i
sticky na svim stranicama. Obrisani skrolovano/nadHerojem, gradijentna
prekrivka i čip lokacije kojeg na mockupu nema.

Nova CategoryRail nosi okrugle slike kategorija ispod headera i
zamjenjuje i staru category-chips i obrisanu .animate-traka. Imena su
naša stvarna, ne mockupova.

ShopNavbar je omotan u <Suspense> jer CategoryRail čita useSearchParams();
bez toga pada npm run build, što tsc i eslint ne vide.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 5: Donja navigacija — peti tab

**Files:**
- Modify: `frontend/src/components/layout/mobile-tab-bar.tsx`

**Interfaces:**
- Consumes: `useCart()` → `{ count, isOpen, openCart }`; `useAuth()` → `{ user }`
- Produces: `<MobileTabBar />`

- [ ] **Step 1: Zamijeniti listu tabova**

```tsx
const TABS: Tab[] = [
  { href: "/", label: "Početna", icon: Home, exact: true },
  { href: "/products", label: "Proizvodi", icon: LayoutGrid },
  { href: "/kategorije", label: "Kategorije", icon: Grid2x2 },
  { href: "/cart", label: "Korpa", icon: ShoppingCart, cart: true },
  { href: "/account", label: "Profil", icon: User },
];
```

Import: `import { Grid2x2, Home, LayoutGrid, ShoppingCart, User, type LucideIcon } from "lucide-react";`
Obrisati `ClipboardList` iz importa — više se ne koristi.

- [ ] **Step 2: Profil vodi na prijavu kad korisnik nije prijavljen**

`/kategorije` i `/account` **još ne postoje** — prave se u Fazi 4. Da peti tab ne vodi na
404 do tada, u komponenti:

```tsx
  const { user } = useAuth();
```

i u `TABS.map` prije `<Link href={t.href}>` izvesti odredište:

```tsx
          /* /account stiže u Fazi 4; do tada Profil vodi na prijavu ili narudžbe. */
          const href =
            t.href === "/account" ? (user ? "/orders" : "/login") : t.href;
```

pa koristiti `href` umjesto `t.href` u `<Link>`. Dodati import
`import { useAuth } from "@/lib/auth/auth-context";`

**`/kategorije` ostaje kako jest** — Faza 4 ga pravi, a dotle vodi na 404. Zabilježiti to
u dnevnik umjesto da se pravi privremeni redirect koji bi se poslije zaboravio.

- [ ] **Step 3: Safe-area i podloga**

Zamijeniti `<nav>` klase:

```tsx
    <nav className="fixed inset-x-0 bottom-0 z-40 border-t border-line bg-surface/95 pb-[env(safe-area-inset-bottom)] backdrop-blur md:hidden">
```

Zamijeniti aktivnu boju u `cls`:

```tsx
            active ? "text-brand-600" : "text-muted",
```

- [ ] **Step 4: Proširiti donji razmak u layoutu**

Pet tabova sa safe-area su viši od `pb-16`. U `frontend/src/app/(shop)/layout.tsx`:

```tsx
      <main className="flex-1 pb-20 md:pb-0">{children}</main>
```

- [ ] **Step 5: Provjera**

Run (iz `frontend/`): `npx tsc --noEmit && npm run lint && npm run build`
Expected: prolazi.

- [ ] **Step 6: Pogledati na telefonskoj širini**

Na 375px i 430px: pet tabova staje u red bez preloma, natpisi se ne sijeku, traka stoji
iznad safe-area. Dno stranice nije skriveno iza trake.

- [ ] **Step 7: Dopiši dnevnik**

```markdown
- **Task 5 gotov.** Donja navigacija ima pet tabova (Početna, Proizvodi, Kategorije,
  Korpa, Profil), safe-area i bijelu podlogu. Layout `pb-16` → `pb-20`.
  **Otvoreno:** tab „Kategorije" vodi na `/kategorije` koja postoji tek od Faze 4 — do
  tada daje 404. „Profil" privremeno vodi na `/orders` (prijavljen) ili `/login` (gost).
```

- [ ] **Step 8: Commit**

```bash
git add frontend/src/components/layout/mobile-tab-bar.tsx "frontend/src/app/(shop)/layout.tsx" docs/superpowers/plans/2026-10-02-redizajn-opcija3-dnevnik.md
git commit -m "feat: donja navigacija na pet tabova

Početna, Proizvodi, Kategorije, Korpa, Profil — kao na mockupu. Traka
dobija bijelu podlogu i env(safe-area-inset-bottom), a layout širi donji
razmak sa pb-16 na pb-20 da sadržaj ne ostane ispod nje.

Profil privremeno vodi na /orders ili /login jer /account stiže u Fazi 4.
Kategorije vode na /kategorije koja do Faze 4 daje 404 — zapisano u
dnevnik umjesto privremenog redirecta koji bi se zaboravio.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 6: Benefiti i pločica kategorije

**Files:**
- Create: `frontend/src/components/shop/benefit-cards.tsx`
- Create: `frontend/src/components/shop/category-tile.tsx`

**Interfaces:**
- Consumes: `Category` tip iz `@/types/domain`
- Produces:
  - `<BenefitCards />` — bez propova
  - `<CategoryTile category={Category} />`

- [ ] **Step 1: Napisati `benefit-cards.tsx`**

```tsx
import { Leaf, ShieldCheck, Sprout, Truck } from "lucide-react";

/**
 * Četiri benefita sa mockupa. Dijele ih početna i (u Fazi 2) detalj proizvoda, pa
 * tekstovi stoje na jednom mjestu umjesto da se prepisuju.
 *
 * Tvrdnje su namjerno one koje aplikacija stvarno drži: dostava je Banja Luka i
 * okolina (zone u bazi), proizvodi dolaze od proizvođača iz `producers`. Ništa se
 * ne obećava što se ne može potkrijepiti.
 */
const BENEFITI = [
  { ikona: Leaf, naslov: "100% svježe", opis: "Provjereni kvalitet" },
  { ikona: Sprout, naslov: "Domaći proizvodi", opis: "Podrška lokalnim proizvođačima" },
  { ikona: Truck, naslov: "Brza dostava", opis: "Banja Luka i okolina" },
  { ikona: ShieldCheck, naslov: "Sigurna kupovina", opis: "Vaši podaci su zaštićeni" },
] as const;

export function BenefitCards() {
  return (
    <ul className="grid grid-cols-2 gap-3 lg:grid-cols-4">
      {BENEFITI.map((b) => (
        <li
          key={b.naslov}
          className="flex items-center gap-3 rounded-card border border-line bg-surface p-3.5"
        >
          <span className="grid h-10 w-10 shrink-0 place-items-center rounded-[10px] bg-sand text-brand-600">
            <b.ikona className="h-5 w-5" />
          </span>
          <div className="min-w-0">
            <p className="text-sm font-semibold text-ink">{b.naslov}</p>
            <p className="truncate text-xs text-muted">{b.opis}</p>
          </div>
        </li>
      ))}
    </ul>
  );
}
```

- [ ] **Step 2: Napisati `category-tile.tsx`**

```tsx
import Link from "next/link";
import Image from "next/image";
import { Sprout } from "lucide-react";
import type { Category } from "@/types/domain";

/**
 * Pločica kategorije sa mockupa: kvadratna slika sa zaobljenim uglovima, naziv ispod,
 * blago uvećanje na hover. Koriste je početna („Popularne kategorije") i /kategorije.
 */
export function CategoryTile({ category }: { category: Category }) {
  return (
    <Link href={`/products?category=${category.slug}`} className="group block text-center">
      <span className="relative block aspect-square overflow-hidden rounded-card bg-sand">
        {category.imageUrl ? (
          <Image
            src={category.imageUrl}
            alt=""
            fill
            sizes="(max-width: 640px) 33vw, 150px"
            className="object-cover transition duration-500 group-hover:scale-105"
          />
        ) : (
          <span className="absolute inset-0 grid place-items-center">
            <Sprout className="h-8 w-8 text-brand-400" />
          </span>
        )}
      </span>
      <span className="mt-2 block text-xs font-medium text-ink transition group-hover:text-brand-700 sm:text-sm">
        {category.name}
      </span>
    </Link>
  );
}
```

- [ ] **Step 3: Provjera**

Run (iz `frontend/`): `npx tsc --noEmit && npm run lint && npm run build`
Expected: prolazi. Obje komponente su još neupotrijebljene — koristi ih Task 7.

- [ ] **Step 4: Dopiši dnevnik**

```markdown
- **Task 6 gotov.** Nove `shop/benefit-cards.tsx` (četiri benefita, tekstovi na jednom
  mjestu) i `shop/category-tile.tsx`. Koristi ih Task 7.
```

- [ ] **Step 5: Commit**

```bash
git add frontend/src/components/shop/benefit-cards.tsx frontend/src/components/shop/category-tile.tsx docs/superpowers/plans/2026-10-02-redizajn-opcija3-dnevnik.md
git commit -m "feat: kartice benefita i pločica kategorije

Dvije komponente koje mockup ponavlja na više ekrana. Tekstovi benefita
drže se onoga što aplikacija stvarno nosi — dostava Banja Luka i okolina
po zonama iz baze, proizvodi od stvarnih proizvođača.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 7: Početna stranica

**Files:**
- Rewrite: `frontend/src/app/(shop)/page.tsx`

**Interfaces:**
- Consumes: `<SectionHeader>` (Task 2), `<ProductCard>` (Task 3), `<BenefitCards>` i
  `<CategoryTile>` (Task 6), `useCategories()`, `useProducts({ featured: true })`,
  `useProducers()`, `<ProductGridSkeleton>` iz `@/components/shop/product-grid`,
  `buttonVariants` iz `@/components/ui/button`
- Produces: ništa (list stranica)

**Sedam sekcija, redom:** hero → benefiti → popularne kategorije → izdvojeni proizvodi →
domaćini → promo baner → (footer je izvan stranice).

- [ ] **Step 1: Pročitati postojeću sekciju domaćina**

Run: `sed -n '300,360p' "frontend/src/app/(shop)/page.tsx"`

Ta sekcija (`<Section id="domacini">`) radi i zadržava se — prepisuje se u novi raspored
sa `SectionHeader`-om umjesto starog `Nadnaslov`+`SectionHeader` para. Zabilježiti kako
crta proizvođača (slika, ime, selo) da se isti podaci prenesu.

- [ ] **Step 2: Napisati novu početnu**

Zamijeniti **cio sadržaj** `frontend/src/app/(shop)/page.tsx`:

```tsx
"use client";

import Link from "next/link";
import Image from "next/image";
import { ArrowRight } from "lucide-react";
import { SmartImage } from "@/components/ui/smart-image";
import { ProductCard } from "@/components/shop/product-card";
import { ProductGridSkeleton } from "@/components/shop/product-grid";
import { BenefitCards } from "@/components/shop/benefit-cards";
import { CategoryTile } from "@/components/shop/category-tile";
import { SectionHeader } from "@/components/ui/section-header";
import { buttonVariants } from "@/components/ui/button";
import { useCategories, useProducers, useProducts } from "@/lib/api/hooks";
import { cn } from "@/lib/utils";

/**
 * Početna po mockupu „Opcija 3": svijetla cijelom dužinom. Stari izgled je imao dvije
 * tamne sekcije (hero i izdvojeni proizvodi) — one su uklonjene, jer tamnu površinu na
 * mockupu nosi samo donji promo baner.
 */

/** Zajednički okvir sekcije: puna širina, unutrašnja kolona, isti ritam svuda. */
function Section({
  children,
  className,
  id,
}: {
  children: React.ReactNode;
  className?: string;
  id?: string;
}) {
  return (
    <section id={id} className={cn("px-4 py-8 sm:py-12", className)}>
      <div className="mx-auto max-w-6xl">{children}</div>
    </section>
  );
}

export default function HomePage() {
  const categories = useCategories();
  const featured = useProducts({ featured: true });
  const producers = useProducers();

  return (
    <div>
      {/* ---------- Hero ---------- */}
      <Section className="pt-6">
        <div className="relative overflow-hidden rounded-card border border-line bg-surface">
          <div className="grid items-center gap-6 md:grid-cols-2">
            <div className="order-2 p-6 sm:p-10 md:order-1">
              <span className="text-xs font-bold uppercase tracking-[0.18em] text-brand-600">
                Zdrava ishrana
              </span>
              <h1 className="mt-3 font-display text-3xl font-bold leading-[1.1] text-ink sm:text-5xl">
                Bolji izbori
                <br />
                za zdraviji život.
              </h1>
              <p className="mt-4 max-w-md text-sm text-muted sm:text-base">
                Svježe, domaće i kvalitetne namirnice na jednom mjestu.
              </p>
              <Link
                href="/products"
                className={cn(buttonVariants({ size: "lg" }), "mt-6")}
              >
                Pogledaj ponudu
                <ArrowRight className="h-4 w-4" />
              </Link>
            </div>
            <div className="relative order-1 h-56 md:order-2 md:h-[380px]">
              <Image
                src="/hero/hero-domacinstvo.jpg"
                alt="Svježe povrće sa Manjače"
                fill
                priority
                sizes="(max-width: 768px) 100vw, 50vw"
                className="object-cover"
              />
            </div>
          </div>
        </div>
      </Section>

      {/* ---------- Benefiti ---------- */}
      <Section className="py-2">
        <BenefitCards />
      </Section>

      {/* ---------- Popularne kategorije ---------- */}
      <Section>
        <SectionHeader naslov="Popularne kategorije" link="/kategorije" />
        {categories.isLoading ? (
          <div className="grid grid-cols-3 gap-3 sm:grid-cols-4 lg:grid-cols-6">
            {Array.from({ length: 6 }).map((_, i) => (
              <div key={i}>
                <div className="skeleton aspect-square rounded-card" />
                <div className="skeleton mx-auto mt-2 h-3 w-14 rounded" />
              </div>
            ))}
          </div>
        ) : (
          <div className="grid grid-cols-3 gap-3 sm:grid-cols-4 lg:grid-cols-6">
            {categories.data?.slice(0, 6).map((c) => (
              <CategoryTile key={c.id} category={c} />
            ))}
          </div>
        )}
      </Section>

      {/* ---------- Izdvojeni proizvodi ---------- */}
      <Section>
        <SectionHeader naslov="Izdvojeni proizvodi" link="/products" />
        {featured.isLoading ? (
          <ProductGridSkeleton />
        ) : (
          /* Iste kolone i razmak kao MREZA u product-grid.tsx — inače skeleton i
             učitani sadržaj imaju različit raspored i stranica skoči. */
          <div className="grid grid-cols-2 gap-3 md:grid-cols-3 lg:grid-cols-4 lg:gap-4">
            {featured.data?.slice(0, 8).map((p) => (
              <ProductCard key={p.id} product={p} />
            ))}
          </div>
        )}
      </Section>

      {/* ---------- Domaćini ---------- */}
      <Section id="domacini">
        <SectionHeader
          naslov="Naši domaćini"
          opis="Ljudi koji stoje iza svakog proizvoda."
        />
        <div className="grid grid-cols-2 gap-3 md:grid-cols-4 lg:gap-4">
          {producers.data?.slice(0, 4).map((p) => (
            <div
              key={p.id}
              className="overflow-hidden rounded-card border border-line bg-surface"
            >
              <div className="relative aspect-[4/3] bg-sand">
                <SmartImage src={p.photoUrl} alt={p.name} sizes="(max-width: 640px) 50vw, 25vw" />
              </div>
              <div className="p-3.5">
                <p className="text-sm font-semibold text-ink">{p.name}</p>
                <p className="mt-0.5 text-xs text-muted">{p.village}</p>
              </div>
            </div>
          ))}
        </div>
      </Section>

      {/* ---------- Promo baner ---------- */}
      <Section className="pb-14">
        <div className="relative overflow-hidden rounded-card">
          <Image
            src="/hero/hero-domacinstvo.jpg"
            alt=""
            fill
            sizes="100vw"
            className="object-cover"
          />
          {/*
            Jedna od dvije preostale tamne površine u aplikaciji (druga je footer).
            Prekrivka je obavezna: bijeli tekst direktno na fotografiji pada ispod AA.
          */}
          <div className="absolute inset-0 bg-brand-700/85" />
          <div className="relative px-6 py-10 sm:px-12 sm:py-14">
            <h2 className="font-display text-2xl font-bold text-white sm:text-4xl">
              Domaće. Svježe. Pouzdano.
            </h2>
            <p className="mt-2 max-w-md text-sm text-white/85">
              Direktno od provjerenih proizvođača sa Manjače.
            </p>
            <Link
              href="/products"
              className="mt-5 inline-flex h-11 items-center justify-center rounded-[10px] bg-white px-6 text-sm font-medium text-brand-700 transition hover:bg-cream"
            >
              Saznaj više
            </Link>
          </div>
        </div>
      </Section>
    </div>
  );
}
```

- [ ] **Step 3: Uporediti sekciju domaćina sa starom**

Ako je stara sekcija (Step 1) prikazivala i `p.description` ili broj proizvoda, dodati to
u novu. **Ne dodavati podatke kojih u `Producer` tipu nema** — tip je
`{ id, name, village, description, photoUrl?, latitude?, longitude?, phone? }`.

- [ ] **Step 4: Provjera**

Run (iz `frontend/`): `npx tsc --noEmit && npm run lint && npm run build`
Expected: sve prolazi.

**Česta greška:** `Reveal`, `Nadnaslov`, `MapPin`, `Truck`, `PackageCheck`,
`MessagesSquare`, `Sprout`, `useMemo`, `useState` su bili u starom fajlu a u novom se ne
koriste — ako su ostali u importima, `eslint` pada.

- [ ] **Step 5: Pogledati u browseru**

`http://localhost:3000` na 1440px: hero je svijetla kartica sa slikom desno, ispod nje
četiri benefita u redu, šest kategorija, osam proizvoda u dva reda, četiri domaćina,
tamnozeleni baner. **Nigdje nema crne sekcije osim banera i footera.**

Zatim 375 / 390 / 430 / 768 / 1024: nema vodoravnog skrola, proizvodi su dva po redu na
telefonu, hero slika je iznad teksta.

- [ ] **Step 6: Dopiši dnevnik i zatvori Fazu 1**

```markdown
- **Task 7 gotov — Faza 1 završena.** Početna prepisana na sedam svijetlih sekcija;
  uklonjene obje tamne sekcije (hero i izdvojeni proizvodi). Tamno je ostalo samo na
  promo baneru i u footeru.
```

Ažurirati i tabelu „Status faza": Faza 1 → **gotova**, Faza 2 → **na redu**.

- [ ] **Step 7: Commit**

```bash
git add "frontend/src/app/(shop)/page.tsx" docs/superpowers/plans/2026-10-02-redizajn-opcija3-dnevnik.md
git commit -m "feat: početna stranica po mockupu „Opcija 3\"

Sedam svijetlih sekcija: hero split sa slikom, četiri benefita, popularne
kategorije, izdvojeni proizvodi, domaćini i promo baner.

Uklonjene obje tamne sekcije starog izgleda. Promjena vrijednosti tokena
ih ne bi uklonila — tamne površine su bile raspored, ne boja. Tamno je
ostalo samo na promo baneru i u footeru, gdje ga mockup i ima.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 8: Provjera cijele faze

**Files:** nijedan (samo provjera)

- [ ] **Step 1: Puna provjera**

Run (iz `frontend/`):

```bash
npx tsc --noEmit
npm run lint
npm run build
```

Expected: sve tri čiste. Zapisati stvaran izlaz, ne pretpostavku.

- [ ] **Step 2: Provjera da stari tokeni nisu ostali u upotrebi tamo gdje ne treba**

Run: `grep -rn "night-card\|muted-dark\|animate-traka" src --include="*.tsx" --include="*.css"`
Expected: `night-card` i `muted-dark` smiju ostati samo u footeru i fajlovima koje ova faza
ne dira. `animate-traka` **ne smije imati nijedan pogodak** — obrisan je u Tasku 1.

- [ ] **Step 3: Prolaz kroz širine**

Na `/` i `/products`, na **375, 390, 430, 768, 1024, 1440 px**:

- nema vodoravnog skrola
- na telefonu najmanje dva proizvoda po redu
- donja traka stoji iznad safe-area i ne pokriva sadržaj
- traka kategorija se skroluje postrance, bez vidljivog scrollbara
- tap targeti ≥ 40px

- [ ] **Step 4: Provjera da tok i dalje radi**

Dodati proizvod u korpu sa početne i sa `/products` — toast se pojavi, `flyToCart` leti ka
korpi, broj u badge-u raste. Otvoriti korpu. Prijaviti se demo nalogom
`mujo@example.com` / `mujo123`.

- [ ] **Step 5: Dopiši dnevnik**

Upisati rezultate provjere (šta je prošlo, šta nije) i sve što je ostalo otvoreno za
Fazu 2.

- [ ] **Step 6: Commit dnevnika**

```bash
git add docs/superpowers/plans/2026-10-02-redizajn-opcija3-dnevnik.md
git commit -m "docs: dnevnik — Faza 1 provjerena

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Poznata otvorena pitanja za Fazu 2

- `/kategorije` i `/account` ne postoje — tabovi u donjoj traci vode na 404 odnosno na
  privremeno odredište. Rješava Faza 4.
- `(shop)/products/page.tsx` je u ovoj fazi samo očišćen od `category-chips` i `tone` propa;
  njegov puni redizajn (sidebar filteri, bottom sheet, klijentska paginacija) je Faza 2.
- Srce za omiljene nije renderovano nigdje — Faza 5.
- `shop-footer.tsx` nije diran; ostaje tamni, što mockup i ima.
