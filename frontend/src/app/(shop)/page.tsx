"use client";

import Link from "next/link";
import Image from "next/image";
import { useMemo, useState } from "react";
import {
  ArrowRight,
  MapPin,
  MessagesSquare,
  PackageCheck,
  Sprout,
  Truck,
} from "lucide-react";
import { SmartImage } from "@/components/ui/smart-image";
import { ProductCard } from "@/components/shop/product-card";
import { ProductGridSkeleton } from "@/components/shop/product-grid";
import { buttonVariants } from "@/components/ui/button";
import { Reveal } from "@/components/ui/reveal";
import { useCategories, useProducers, useProducts } from "@/lib/api/hooks";
import { cn } from "@/lib/utils";

/**
 * Naslovna ide u ritmu krem → zelena traka → krem → TAMNO → krem → pijesak → zelena
 * → tamni footer. Naizmjenično svijetlo/tamno drži dugu stranicu da se ne rasplinjava,
 * a tamna sekcija izdvojenih proizvoda je jedino mjesto gdje fotografije proizvoda
 * dobijaju kontrast koji na kremu nemaju.
 *
 * Horizontalne trake su namjerno zadržane ispod `sm`. Telefon ima donju tab traku i
 * korpu — mreža od četiri u redu tamo znači beskonačno skrolanje do prvog proizvoda.
 */

/** Zajednički okvir sekcije: puna širina, unutrašnja kolona, isti ritam svuda. */
function Section({
  children,
  alt = false,
  className,
  id,
}: {
  children: React.ReactNode;
  alt?: boolean;
  className?: string;
  id?: string;
}) {
  return (
    <section id={id} className={cn("px-4 py-16 sm:py-24", alt && "bg-sand", className)}>
      <div className="mx-auto max-w-6xl">{children}</div>
    </section>
  );
}

/**
 * Nadnaslov u amberu iznad svakog naslova sekcije. Na svijetloj podlozi ide honey-700
 * (5.02:1); honey-500 bi tu dao 2.97:1 i pao AA, pa `tone` nije stvar ukusa.
 */
function Nadnaslov({ children, tone = "light" }: { children: string; tone?: "light" | "dark" }) {
  return (
    <span
      className={cn(
        "text-xs font-bold uppercase tracking-[0.18em]",
        tone === "dark" ? "text-honey-500" : "text-honey-700",
      )}
    >
      {children}
    </span>
  );
}

function SectionHeader({
  nadnaslov,
  naslov,
  opis,
  link,
  linkTekst,
  tone = "light",
  desno,
}: {
  nadnaslov: string;
  naslov: React.ReactNode;
  opis?: string;
  link?: string;
  linkTekst?: string;
  tone?: "light" | "dark";
  desno?: React.ReactNode;
}) {
  return (
    <div className="mb-10 flex flex-wrap items-end justify-between gap-x-6 gap-y-4">
      <div>
        <Nadnaslov tone={tone}>{nadnaslov}</Nadnaslov>
        <h2
          className={cn(
            "mt-3 font-display text-4xl font-bold leading-[1.1] sm:text-5xl",
            tone === "dark" ? "text-cream" : "text-ink",
          )}
        >
          {naslov}
        </h2>
        {opis && (
          <p className={cn("mt-3 max-w-xl", tone === "dark" ? "text-muted-dark" : "text-muted")}>
            {opis}
          </p>
        )}
      </div>
      {desno}
      {link && (
        <Link
          href={link}
          className={cn(
            "border-b pb-1 text-sm font-medium transition",
            tone === "dark"
              ? "border-white/25 text-cream hover:border-honey-500 hover:text-honey-500"
              : "border-ink/25 text-ink hover:border-honey-700 hover:text-honey-700",
          )}
        >
          {linkTekst} →
        </Link>
      )}
    </div>
  );
}

export default function HomePage() {
  const categories = useCategories();
  const featured = useProducts({ featured: true });
  const producers = useProducers();

  /* Filter čipova radi nad već učitanim izdvojenim proizvodima — bez novog zahtjeva. */
  const [filter, setFilter] = useState<string | null>(null);
  const filterKategorije = useMemo(() => {
    const imena = new Set((featured.data ?? []).map((p) => p.categoryName).filter(Boolean));
    return [...imena] as string[];
  }, [featured.data]);
  const prikazani = useMemo(() => {
    const svi = featured.data ?? [];
    return filter ? svi.filter((p) => p.categoryName === filter) : svi;
  }, [featured.data, filter]);

  /* Mozaik nosi pet pločica: jednu veliku i četiri male. Ostale idu iza "Sve kategorije". */
  const mozaik = categories.data?.slice(0, 5) ?? [];
  const [velika, ...male] = mozaik;

  return (
    <div>
      {/* ---------- Hero ---------- */}
      <section className="relative min-h-[600px] overflow-hidden sm:min-h-[680px]">
        <Image
          src="/hero/hero-domacinstvo.jpg"
          alt="Paradajz u drvenom sanduku, vijenci bijelog luka i pletene korpe sa voćem"
          fill
          priority
          sizes="100vw"
          className="object-cover"
        />
        {/*
          Gradijent je namjerno u inline `rgba`, a ne kroz `bg-gradient-to-r from-[…]/92`:
          Tailwind v4 interpolira takve stopove u OKLab prostoru i sloj ispadne skoro pun
          umjesto tražene prozirnosti — fotografija se izgubi. Eksplicitni rgba stopovi
          rade isto u svakom browseru.

          Jak je lijevo, skoro čist desno: lijeva trećina kadra je svijetli kameni zid i
          bez tog zatamnjenja krem naslov na njemu pada ispod AA, dok desno stoje korpe
          sa voćem i povrćem gdje prekrivka samo smeta.
        */}
        <div
          className="absolute inset-0"
          style={{
            background:
              "linear-gradient(90deg, rgba(20,18,12,0.92) 0%, rgba(20,18,12,0.82) 26%, rgba(20,18,12,0.45) 58%, rgba(20,18,12,0.12) 100%)",
          }}
        />
        <div
          className="absolute inset-0"
          style={{
            background:
              "linear-gradient(0deg, rgba(20,18,12,0.55) 0%, rgba(20,18,12,0) 45%)",
          }}
        />

        {/*
          `pt-36` na telefonu, ne `py-20`: tamo navbar ima dva reda (logo/korpa, pa
          lokacija i pretraga) i visok je oko 122px, pa bi centrirani sadržaj naslonio
          nadnaslov na donju ivicu pretrage. Od `sm` naviše navbar je jedan red i
          simetričan razmak je opet ispravan.
        */}
        <div className="relative mx-auto flex min-h-[600px] max-w-6xl items-center px-4 pb-20 pt-36 sm:min-h-[680px] sm:py-20">
          <div className="max-w-2xl">
            <p className="text-xs font-medium uppercase tracking-[0.22em] text-honey-300">
              Banja Luka · Manjača
            </p>
            {/* clamp: 40px na telefonu do 84px na širokom ekranu — bez skokova po breakpointima. */}
            <h1 className="mt-7 font-display font-bold leading-[1.02] text-cream [font-size:clamp(2.5rem,7vw,5.25rem)]">
              Domaće.
              <br />
              <span className="text-honey-500">Svježe.</span>
              <br />
              Na tvoja vrata.
            </h1>
            <p className="mt-7 max-w-md text-lg leading-relaxed text-[#ddd3c0]">
              Povrće, sir, jaja, med i zimnica od domaćina sa Manjače — bez pijace, bez
              posrednika.
            </p>
            <div className="mt-9 flex flex-wrap items-center gap-x-7 gap-y-3">
              <Link href="/products" className={buttonVariants({ size: "lg" })}>
                Kupuj proizvode <ArrowRight className="h-4 w-4" />
              </Link>
              <Link
                href="#domacini"
                className="text-sm font-medium text-cream transition hover:text-honey-500"
              >
                Naši domaćini →
              </Link>
            </div>
          </div>
        </div>
      </section>

      {/* ---------- Traka kategorija ---------- */}
      <TrakaKategorija imena={categories.data?.map((c) => c.name) ?? []} />

      {/* ---------- Kategorije ---------- */}
      <Section>
        <SectionHeader
          nadnaslov="Kategorije"
          naslov="Šta nudimo"
          link="/products"
          linkTekst="Sve kategorije"
        />
        {categories.isLoading ? (
          <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3 lg:[grid-auto-rows:224px]">
            <div className="h-72 animate-pulse rounded-card bg-sand sm:col-span-2 lg:col-span-1 lg:row-span-2 lg:h-full" />
            {Array.from({ length: 4 }).map((_, i) => (
              <div key={i} className="h-56 animate-pulse rounded-card bg-sand" />
            ))}
          </div>
        ) : velika ? (
          /*
            Mozaik umjesto ravnomjerne mreže: jedna pločica preko dva reda daje
            stranici težište, a četiri male oko nje drže gustinu. Na telefonu se
            sve slaže u jednu kolonu — mozaik na 360px nema šta da pokaže.
          */
          <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3 lg:[grid-auto-rows:224px]">
            <KategorijaPlocica
              kategorija={velika}
              className="h-72 sm:col-span-2 lg:col-span-1 lg:row-span-2 lg:h-full"
              velika
            />
            {male.map((c) => (
              <KategorijaPlocica key={c.id} kategorija={c} className="h-56" />
            ))}
          </div>
        ) : (
          <p className="text-muted">Kategorije trenutno nisu dostupne.</p>
        )}
      </Section>

      {/* ---------- Izdvojeni proizvodi ---------- */}
      <section className="bg-night px-4 py-16 sm:py-24">
        <div className="mx-auto max-w-6xl">
          <SectionHeader
            nadnaslov="Ponuda"
            naslov="Izdvojeni proizvodi"
            tone="dark"
            desno={
              filterKategorije.length > 1 ? (
                <div className="no-scrollbar -mx-4 flex max-w-full gap-2 overflow-x-auto px-4">
                  <Cip aktivan={filter === null} onClick={() => setFilter(null)}>
                    Svi
                  </Cip>
                  {filterKategorije.map((k) => (
                    <Cip key={k} aktivan={filter === k} onClick={() => setFilter(k)}>
                      {k}
                    </Cip>
                  ))}
                </div>
              ) : undefined
            }
          />
          {featured.isLoading ? (
            <ProductGridSkeleton count={4} />
          ) : prikazani.length > 0 ? (
            <>
              {/* Telefon: traka. Tablet naviše: mreža. */}
              <div className="no-scrollbar -mx-4 flex gap-4 overflow-x-auto px-4 pb-1 sm:hidden">
                {prikazani.slice(0, 10).map((p) => (
                  <div key={p.id} className="w-44 shrink-0">
                    <ProductCard product={p} />
                  </div>
                ))}
              </div>
              <div className="hidden gap-6 sm:grid sm:grid-cols-2 lg:grid-cols-3">
                {prikazani.slice(0, 9).map((p) => (
                  <ProductCard key={p.id} product={p} />
                ))}
              </div>
            </>
          ) : (
            <p className="text-muted-dark">
              {filter
                ? `Nema izdvojenih proizvoda u kategoriji ${filter}.`
                : "Trenutno nema izdvojenih proizvoda."}
            </p>
          )}
        </div>
      </section>

      {/* ---------- Domaćini ---------- */}
      <Section id="domacini">
        <div className="grid gap-6 lg:grid-cols-[1fr_1fr] lg:items-end">
          <div>
            <Nadnaslov>Naši domaćini</Nadnaslov>
            <h2 className="mt-3 font-display text-4xl font-bold leading-[1.1] text-ink sm:text-5xl">
              Upoznajte
              <br />
              <span className="text-brand-600">proizvođače</span>
            </h2>
          </div>
          <p className="text-muted lg:pb-2 lg:text-right">
            Iza svakog proizvoda stoji porodica sa Manjače. Svaka etiketa ima lice i ime.
          </p>
        </div>

        <div className="mt-14 grid gap-8 sm:grid-cols-2 lg:grid-cols-4">
          {producers.isLoading
            ? Array.from({ length: 4 }).map((_, i) => (
                <div key={i} className="flex flex-col items-center gap-3">
                  <div className="h-32 w-32 animate-pulse rounded-full bg-sand" />
                  <div className="h-4 w-28 animate-pulse rounded bg-sand" />
                </div>
              ))
            : producers.data?.map((p, i) => (
                <Reveal key={p.id} delay={i * 0.06}>
                  <article className="flex flex-col items-center text-center">
                    <div className="relative h-32 w-32 overflow-hidden rounded-full bg-sand ring-1 ring-line">
                      <SmartImage src={p.photoUrl} alt={p.name} sizes="128px" />
                    </div>
                    <h3 className="mt-5 font-display text-lg font-bold text-ink">{p.name}</h3>
                    <p className="mt-1 flex items-center gap-1 text-xs font-bold uppercase tracking-[0.14em] text-honey-700">
                      <MapPin className="h-3.5 w-3.5" /> {p.village}
                    </p>
                    <p className="mt-2.5 line-clamp-3 text-sm text-muted">{p.description}</p>
                  </article>
                </Reveal>
              ))}
        </div>

        {/* Razdjelnik sa rombom — isti potez kao na predlošku, drži dvije sekcije razdvojene. */}
        <div className="mt-16 flex items-center gap-4" aria-hidden>
          <span className="h-px flex-1 bg-line" />
          <span className="text-honey-500">◆</span>
          <span className="h-px flex-1 bg-line" />
        </div>

        <div className="mt-14 grid gap-10 sm:grid-cols-2 lg:grid-cols-4">
          <Obecanje
            icon={<Sprout className="h-5 w-5" />}
            naslov="Provjereni domaćini"
            tekst="Sarađujemo samo sa gazdinstvima koja lično poznajemo."
          />
          <Obecanje
            icon={<Truck className="h-5 w-5" />}
            naslov="Dostava četvrtkom"
            tekst="Kombiji voze na kućni prag u Banjoj Luci, petak po potrebi."
          />
          <Obecanje
            icon={<PackageCheck className="h-5 w-5" />}
            naslov="Pakujemo na Manjači"
            tekst="Srijedom domaćini beru i pakuju — bez skladišta između."
          />
          <Obecanje
            icon={<MessagesSquare className="h-5 w-5" />}
            naslov="Podrška na telefonu"
            tekst="Dostupni smo za pitanja o porijeklu i uslovima dostave."
          />
        </div>
      </Section>

      {/* ---------- Naša priča ---------- */}
      <Section alt id="kako">
        <div className="grid gap-12 lg:grid-cols-[1fr_1.1fr] lg:items-center">
          <div>
            <Nadnaslov>Naša priča</Nadnaslov>
            <h2 className="mt-3 font-display text-4xl font-bold leading-[1.1] text-ink sm:text-5xl">
              Provjereno, domaće,
              <br />s Manjače.
            </h2>
          </div>
          <p className="text-lg leading-relaxed text-muted">
            Sarađujemo sa porodicama sa Manjače koje poznajemo lično. Bez konzervansa, bez
            industrijske obrade, bez dugih lanaca snabdijevanja — svaki proizvod dolazi od
            domaćina kojeg možemo imenovati. Narudžbe primamo do utorka, a dostavljamo
            četvrtkom.
          </p>
        </div>
      </Section>

      {/* ---------- Zelena traka pred footerom ---------- */}
      <section className="bg-brand-600 px-4 py-20 text-center sm:py-24">
        <div className="mx-auto max-w-2xl">
          <p className="text-xs font-bold uppercase tracking-[0.22em] text-honey-300">
            Zone dostave
          </p>
          <h2 className="mt-5 font-display text-4xl font-bold leading-[1.1] text-cream sm:text-5xl">
            Dostavljamo
            <br />u tvoj kraj.
          </h2>
          <p className="mt-5 text-[#d9cdb8]">
            Centar, Borik, Lauš i Starčevica — narudžbe do utorka u 12h, dostava četvrtkom.
          </p>
          <div className="mt-9 flex flex-wrap justify-center gap-2">
            {["Centar", "Borik", "Lauš", "Starčevica"].map((z) => (
              <span
                key={z}
                className="rounded-full border border-white/20 px-4 py-2 text-sm text-cream"
              >
                {z}
              </span>
            ))}
          </div>
          <Link
            href="/products"
            className={cn(buttonVariants({ variant: "honey", size: "lg" }), "mt-10")}
          >
            Počni kupovinu <ArrowRight className="h-4 w-4" />
          </Link>
        </div>
      </section>
    </div>
  );
}

/**
 * Traka se renderuje dva puta zaredom i pomjera za -50%: na pola animacije je druga
 * kopija tačno tamo gdje je prva krenula, pa skok nazad na 0 niko ne vidi.
 */
function TrakaKategorija({ imena }: { imena: string[] }) {
  if (imena.length === 0) return null;
  const stavke = [...imena, ...imena];
  return (
    <div className="overflow-hidden bg-brand-600 py-3.5">
      <div className="animate-traka flex w-max items-center gap-8 whitespace-nowrap">
        {stavke.map((ime, i) => (
          <span
            key={i}
            className="flex items-center gap-8 text-xs font-bold uppercase tracking-[0.2em] text-[#d9cdb8]"
          >
            {ime}
            {/* Romb je ukras, pa amber tu smije — kao boja teksta na zelenoj daje 2.79:1. */}
            <span className="text-honey-500" aria-hidden>
              ◆
            </span>
          </span>
        ))}
      </div>
    </div>
  );
}

function KategorijaPlocica({
  kategorija,
  className,
  velika = false,
}: {
  kategorija: { name: string; slug: string; imageUrl?: string; productCount?: number };
  className?: string;
  velika?: boolean;
}) {
  return (
    <Link
      href={`/products?category=${kategorija.slug}`}
      className={cn("group relative block overflow-hidden rounded-card bg-ink", className)}
    >
      <SmartImage
        src={kategorija.imageUrl}
        alt={kategorija.name}
        sizes={velika ? "(max-width: 1024px) 100vw, 400px" : "(max-width: 640px) 100vw, 300px"}
        className="opacity-85 transition duration-700 group-hover:scale-105"
      />
      {/* Gradijent, ne ravna prekrivka: vrh slike ostaje čitljiv, dno nosi tekst. */}
      <div className="absolute inset-0 bg-gradient-to-t from-[#14120c] via-[#14120c]/50 to-transparent" />
      <div className="absolute inset-x-0 bottom-0 p-5">
        {kategorija.productCount != null && (
          <p className="text-[10px] font-bold uppercase tracking-[0.18em] text-honey-500">
            {kategorija.productCount} proizvoda
          </p>
        )}
        <h3
          className={cn(
            "mt-1.5 font-display font-bold text-cream",
            velika ? "text-3xl" : "text-xl",
          )}
        >
          {kategorija.name}
        </h3>
      </div>
    </Link>
  );
}

function Cip({
  children,
  aktivan,
  onClick,
}: {
  children: React.ReactNode;
  aktivan: boolean;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-pressed={aktivan}
      className={cn(
        "shrink-0 rounded-full border px-4 py-2 text-xs font-bold uppercase tracking-[0.12em] transition",
        aktivan
          ? "border-honey-500 bg-honey-500 text-ink"
          : "border-white/20 text-muted-dark hover:border-white/40 hover:text-cream",
      )}
    >
      {children}
    </button>
  );
}

function Obecanje({
  icon,
  naslov,
  tekst,
}: {
  icon: React.ReactNode;
  naslov: string;
  tekst: string;
}) {
  return (
    <div>
      <div className="grid h-11 w-11 place-items-center rounded-full bg-brand-100 text-brand-700">
        {icon}
      </div>
      <h3 className="mt-4 font-display text-lg font-bold text-ink">{naslov}</h3>
      <p className="mt-1.5 text-sm leading-relaxed text-muted">{tekst}</p>
    </div>
  );
}
