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
  const naProizvodima = pathname === "/products";
  const aktivna = naProizvodima ? searchParams.get("category") : null;

  if (isLoading) {
    return <CategoryRailSkeleton />;
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
          const active = naProizvodima && s.slug === aktivna;
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
                    "line-clamp-2 min-h-7 text-center text-[11px] leading-tight",
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

/**
 * Oblik trake prije podataka. Postoji kao zasebna komponenta jer ga koriste dva
 * mjesta — `isLoading` grana ispod i <Suspense fallback> u headeru. Dvije kopije
 * bi se razišle i stranica bi skakala pri hidraciji.
 */
export function CategoryRailSkeleton() {
  return (
    <div className="no-scrollbar overflow-x-auto">
      <div className="mx-auto flex max-w-6xl items-start gap-5 px-4 py-2.5">
        {Array.from({ length: 8 }).map((_, i) => (
          <div key={i} className="flex w-16 shrink-0 flex-col items-center gap-1.5">
            <div className="skeleton h-11 w-11 rounded-full" />
            <div className="skeleton h-7 w-full rounded" />
          </div>
        ))}
      </div>
    </div>
  );
}
