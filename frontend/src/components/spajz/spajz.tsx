"use client";

import { useEffect, useState } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { Award, LayoutGrid, MapPin, Search, Sprout } from "lucide-react";
import { Sekcija } from "@/components/pocetna/sekcija";
import { ProductGrid, ProductGridSkeleton } from "@/components/shop/product-grid";
import { SectionHeader } from "@/components/ui/section-header";
import { SmartImage } from "@/components/ui/smart-image";
import { EmptyState } from "@/components/ui/empty-state";
import { KorpicaUSpajzu } from "@/components/spajz/korpica-u-spajzu";
import { useCategories, useKorpiceList, useProducts } from "@/lib/api/hooks";
import { VELICINE } from "@/lib/planovi";
import { cn } from "@/lib/utils";

/** Slug „kategorije" korpica u Špajzu — korpice nisu proizvodi kataloga, nego svoj katalog. */
const KORPICE = "korpice";
const MREZA = "grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5 xl:grid-cols-6 lg:gap-4";

/**
 * Špajz (R3, po uzoru na farmit /proizvodi): naslov, kategorije u krugovima, „Zavolećeš ove
 * proizvode…", pojas sa tri razloga i „Zašto kod nas?". Izbor kategorije i pretraga su u URL-u
 * (`?category=`, `?search=`), pa se link može podijeliti. Pretraga je E piljara dodatak.
 */
export function Spajz() {
  const params = useSearchParams();
  const router = useRouter();
  const pathname = usePathname();
  const kategorija = params.get("category");
  const [unos, setUnos] = useState(params.get("search") ?? "");
  const pretraga = useDebounced(unos.trim(), 250);

  // Pretraga ide u URL bez novog unosa u istoriju.
  useEffect(() => {
    const q = new URLSearchParams(params.toString());
    if (pretraga) q.set("search", pretraga);
    else q.delete("search");
    const s = q.toString();
    if (s !== params.toString()) router.replace(s ? `${pathname}?${s}` : pathname, { scroll: false });
  }, [pretraga, params, pathname, router]);

  function izaberi(slug: string | null) {
    const q = new URLSearchParams(params.toString());
    if (slug) q.set("category", slug);
    else q.delete("category");
    const s = q.toString();
    router.replace(s ? `${pathname}?${s}` : pathname, { scroll: false });
  }

  const kategorije = useCategories();
  const izdvojeni = useProducts({ featured: true });
  const naKorpicama = kategorija === KORPICE;
  const proizvodi = useProducts({
    category: naKorpicama ? undefined : (kategorija ?? undefined),
    search: pretraga || undefined,
  });
  const filtrirano = !!kategorija || !!pretraga;
  const nazivKategorije = naKorpicama ? "Korpice" : kategorije.data?.find((c) => c.slug === kategorija)?.name;

  // Po uzoru na farmit: u Špajzu su korpice (povrće i voće), a ne povrće i voće na kilogram.
  // Kategorija bez ijednog artikla Špajza (povrće, voće, meso) se ne prikazuje.
  const korpiceLista = useKorpiceList();
  const korpice = (korpiceLista.data ?? [])
    .filter((k) => (k.tip === "povrce" || k.tip === "voce") && k.dostupna)
    .sort((a, b) =>
      a.tip === b.tip
        ? VELICINE.findIndex((v) => v.velicina === a.velicina) - VELICINE.findIndex((v) => v.velicina === b.velicina)
        : a.tip === "povrce" ? -1 : 1,
    );
  const vidljiveKategorije = (kategorije.data ?? []).filter((c) => (c.productCount ?? 0) > 0);

  return (
    <>
      <section className="px-4 pb-10 pt-10 sm:pt-14">
        <div className="mx-auto max-w-6xl text-center">
          <h1 className="text-[32px] font-bold leading-tight text-ink sm:text-[60px] sm:leading-[64px]">
            Proizvodi čije porijeklo znaš
          </h1>
          <p className="mx-auto mt-6 max-w-xl text-base text-ink sm:text-xl">
            Najukusnije sa Manjače, pažljivo izabrano za tebe. Korpice, jaja, ajvar, džem, med i mliječni proizvodi —
            sve stiže u istoj dostavi.
          </p>
        </div>

        {/* Kategorije u krugovima */}
        <div className="no-scrollbar mx-auto mt-10 flex max-w-6xl gap-4 overflow-x-auto px-1 pb-2 sm:justify-center sm:gap-6">
          <Krug naziv="Svi proizvodi" aktivan={!kategorija} onClick={() => izaberi(null)}>
            <LayoutGrid className="h-9 w-9 text-brand-600" />
          </Krug>
          {korpice.length > 0 && (
            <Krug naziv="Korpice" aktivan={naKorpicama} onClick={() => izaberi(KORPICE)}>
              <SmartImage src={korpice[0].imageUrl} alt="" sizes="120px" />
            </Krug>
          )}
          {vidljiveKategorije.map((c) => (
            <Krug key={c.id} naziv={c.name} aktivan={kategorija === c.slug} onClick={() => izaberi(c.slug)}>
              <SmartImage src={c.imageUrl} alt="" sizes="120px" />
            </Krug>
          ))}
        </div>

        <div className="relative mx-auto mt-6 max-w-md">
          <Search className="pointer-events-none absolute left-4 top-1/2 h-4 w-4 -translate-y-1/2 text-muted" />
          <input
            value={unos}
            onChange={(e) => setUnos(e.target.value)}
            placeholder="Traži u Špajzu…"
            className="w-full rounded-full border border-line bg-white py-2.5 pl-11 pr-4 text-sm focus:border-honey-500 focus:outline-none"
          />
        </div>
      </section>

      <section className="px-4 pb-16">
        <div className="mx-auto max-w-7xl">
          {!filtrirano && korpice.length > 0 && (
            <div className="mb-14">
              <SectionHeader oblik="red" naslov="Korpice" link="/korpice/povrce" linkTekst="Sve o korpicama" />
              <div className={MREZA}>
                {korpice.map((k) => (
                  <KorpicaUSpajzu key={k.id} korpica={k} />
                ))}
              </div>
            </div>
          )}

          {!filtrirano && (
            <div className="mb-14">
              <SectionHeader oblik="red" naslov="Zavolećeš ove proizvode…" link="#svi-proizvodi" linkTekst="Svi proizvodi" />
              {izdvojeni.isLoading ? (
                <ProductGridSkeleton />
              ) : (
                <ProductGrid products={(izdvojeni.data ?? []).slice(0, 6)} />
              )}
            </div>
          )}

          <div id="svi-proizvodi" className="scroll-mt-32">
            <SectionHeader
              oblik="red"
              naslov={nazivKategorije ?? (pretraga ? `Rezultati za „${pretraga}"` : "Svi proizvodi")}
            />
          </div>
          {naKorpicama ? (
            <div className={MREZA}>
              {korpice.map((k) => (
                <KorpicaUSpajzu key={k.id} korpica={k} />
              ))}
            </div>
          ) : proizvodi.isLoading ? (
            <ProductGridSkeleton />
          ) : proizvodi.isError ? (
            <EmptyState title="Greška pri učitavanju" description="Nismo uspjeli dohvatiti proizvode. Pokušaj ponovo." />
          ) : proizvodi.data?.length ? (
            <ProductGrid products={proizvodi.data} />
          ) : (
            <EmptyState title="Nema rezultata" description="Pokušaj drugu kategoriju ili pojam pretrage." />
          )}
        </div>
      </section>

      <ZatoSto />
      <ZastoKodNas />
    </>
  );
}

function Krug({
  naziv,
  aktivan,
  onClick,
  children,
}: {
  naziv: string;
  aktivan: boolean;
  onClick: () => void;
  children: React.ReactNode;
}) {
  return (
    <button type="button" onClick={onClick} className="group w-[92px] shrink-0 text-center sm:w-[120px]">
      <span
        className={cn(
          "relative mx-auto grid aspect-square w-full place-items-center overflow-hidden rounded-full bg-[#F2F2F2] transition",
          aktivan ? "ring-[3px] ring-honey-500" : "group-hover:ring-2 group-hover:ring-honey-200",
        )}
      >
        {children}
      </span>
      <span className={cn("mt-3 block text-sm", aktivan ? "font-semibold text-honey-500" : "text-ink")}>{naziv}</span>
    </button>
  );
}

const RAZLOZI = [
  {
    ikona: Sprout,
    naslov: "Dobro za tebe i za domaćinstva",
    tekst: "Sve dolazi direktno od domaćina koje poznajemo lično. Kupovinom podržavaš porodična gazdinstva sa Manjače.",
  },
  {
    ikona: MapPin,
    naslov: "Odabir sa stavom",
    tekst: "Kad ovdje biraš proizvod, znaš ko ga je napravio i u kom selu. Biraš ono što vrijedi.",
  },
  {
    ikona: Award,
    naslov: "Manje robe, više kvaliteta",
    tekst: "Ne nudimo stotinu vrsta istog proizvoda — samo one koje smo sami probali i u koje vjerujemo.",
  },
] as const;

/** „Zato što znaš šta vrijedi" (R3, po uzoru na farmit): krem pojas, tri narandžaste ikone. */
function ZatoSto() {
  return (
    <Sekcija ton="krem">
      <SectionHeader naslov="Zato što znaš šta vrijedi" />
      <div className="grid gap-10 md:grid-cols-3">
        {RAZLOZI.map((r) => (
          <div key={r.naslov} className="text-center">
            <r.ikona className="mx-auto h-16 w-16 text-honey-500" strokeWidth={1.6} />
            <h3 className="mx-auto mt-5 max-w-[260px] text-2xl font-semibold leading-tight text-ink sm:text-[28px]">
              {r.naslov}
            </h3>
            <p className="mx-auto mt-3 max-w-[300px] text-sm leading-relaxed text-ink/80">{r.tekst}</p>
          </div>
        ))}
      </div>
    </Sekcija>
  );
}

const ZASTO = [
  { emoji: "🚚", naslov: "Ista dostava", tekst: "Proizvodi iz Špajza stižu zajedno sa korpicom." },
  { emoji: "💵", naslov: "Pouzećem", tekst: "Plaćaš pri dostavi, bez kartice." },
  { emoji: "🧑‍🌾", naslov: "Poznat domaćin", tekst: "Uz svaki proizvod piše ko ga pravi." },
  { emoji: "🔬", naslov: "Javni nalazi", tekst: "Laboratorijske analize su objavljene." },
] as const;

function ZastoKodNas() {
  return (
    <Sekcija>
      <SectionHeader naslov="Zašto kod nas?" />
      <div className="grid grid-cols-2 gap-6 md:grid-cols-4">
        {ZASTO.map((z) => (
          <div key={z.naslov} className="rounded-[26px] bg-white p-6 text-center shadow-[0_6px_20px_rgba(64,74,61,0.12)]">
            <span className="text-4xl" aria-hidden>
              {z.emoji}
            </span>
            <h3 className="mt-3 text-lg font-semibold text-ink">{z.naslov}</h3>
            <p className="mt-1 text-sm text-ink/80">{z.tekst}</p>
          </div>
        ))}
      </div>
    </Sekcija>
  );
}

function useDebounced<T>(value: T, delay: number): T {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const t = setTimeout(() => setDebounced(value), delay);
    return () => clearTimeout(t);
  }, [value, delay]);
  return debounced;
}
