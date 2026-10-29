"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { ArrowLeft } from "lucide-react";
import { ReceptMeta } from "@/components/recepti/recept-kartica";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { LoadingBlock } from "@/components/ui/spinner";
import { SmartImage } from "@/components/ui/smart-image";
import { useToast } from "@/components/ui/toast";
import { useRecept } from "@/lib/api/hooks";
import { useCart } from "@/lib/cart/cart-context";
import { dodanoSastojakaTekst } from "@/lib/recepti";
import type { Product } from "@/types/domain";

export default function ReceptDetaljPage() {
  const slug = String(useParams<{ slug: string }>().slug);
  const { data: recept, isLoading, isError } = useRecept(slug);
  const { add } = useCart();
  const { show } = useToast();

  if (isLoading) return <LoadingBlock />;
  if (isError || !recept) {
    return (
      <div className="mx-auto max-w-6xl px-4 py-12">
        <EmptyState
          title="Recept nije pronađen"
          description="Možda je uklonjen ili je link pogrešan."
          action={
            <Link href="/recepti">
              <Button variant="secondary">← Svi recepti</Button>
            </Link>
          }
        />
      </div>
    );
  }

  const zaKorpu = (recept.sastojci ?? [])
    .map((s) => s.proizvod)
    .filter((p): p is Product => !!p);
  // Povrće i voće se ne prodaje na kilogram (stiže u korpicama) — u korpu idu samo artikli Špajza.
  const dostupni = zaKorpu.filter((p) => p.isAvailable && p.uSpajzu !== false);
  // Isti proizvod u dva sastojka ide u korpu jednom.
  const jedinstveni = [...new Map(dostupni.map((p) => [p.id, p])).values()];

  function dodajSve() {
    jedinstveni.forEach((p) => add(p));
    show(`${dodanoSastojakaTekst(jedinstveni.length)} u korpu`);
  }

  return (
    <div className="mx-auto max-w-6xl px-4 py-8">
      <div className="grid gap-8 md:grid-cols-2">
        <div className="relative aspect-[4/3] overflow-hidden rounded-card bg-sand shadow-card">
          <SmartImage src={recept.slikaUrl} alt={recept.naslov} sizes="(max-width:768px) 100vw, 50vw" />
        </div>
        <div className="flex flex-col">
          <h1 className="font-display text-3xl font-bold">{recept.naslov}</h1>
          {recept.opis && <p className="mt-4 whitespace-pre-line text-ink/80">{recept.opis}</p>}
          <div className="mt-4">
            <ReceptMeta recept={recept} />
          </div>
        </div>
      </div>

      <div className="mt-10 grid gap-10 lg:grid-cols-2">
        <section>
          <h2 className="mb-4 font-display text-2xl font-bold text-ink">Sastojci</h2>
          <ul className="divide-y divide-line rounded-card border border-line bg-surface">
            {(recept.sastojci ?? []).map((s, i) => (
              <li key={i} className="flex flex-wrap items-baseline gap-x-2 px-4 py-3 text-ink">
                {s.kolicina && (
                  <>
                    <span className="text-ink/80">{s.kolicina}</span>
                    <span aria-hidden>—</span>
                  </>
                )}
                {s.proizvod ? (
                  <Link
                    href={`/products/${s.proizvod.slug}`}
                    className="font-medium text-brand-700 underline-offset-2 hover:underline"
                  >
                    {s.naziv}
                  </Link>
                ) : (
                  <span className="font-medium">{s.naziv}</span>
                )}
                {s.proizvod && !s.proizvod.isAvailable && (
                  <span className="text-xs text-muted">trenutno nema</span>
                )}
                {s.proizvod?.isAvailable && s.proizvod.uSpajzu === false && (
                  <span className="text-xs text-muted">stiže u korpici</span>
                )}
              </li>
            ))}
          </ul>

          {zaKorpu.length > 0 && (
            <div className="mt-4">
              <Button onClick={dodajSve} disabled={jedinstveni.length === 0} className="w-full sm:w-auto">
                {jedinstveni.length === 0
                  ? "Sastojci trenutno nisu u ponudi"
                  : `Dodaj sastojke u korpu (${jedinstveni.length})`}
              </Button>
              <p className="mt-2 text-xs text-muted">
                Količina je po jedan komad ili jedinica proizvoda — prilagodi je u korpi.
              </p>
            </div>
          )}
        </section>

        <section>
          <h2 className="mb-4 font-display text-2xl font-bold text-ink">Priprema</h2>
          <ol className="space-y-4">
            {(recept.koraci ?? []).map((k, i) => (
              <li key={i} className="flex gap-3">
                <span className="grid h-8 w-8 shrink-0 place-items-center rounded-full bg-sand text-sm font-semibold text-brand-700">
                  {i + 1}
                </span>
                <p className="pt-1 text-ink/90">{k}</p>
              </li>
            ))}
          </ol>
        </section>
      </div>

      <Link
        href="/recepti"
        className="mt-10 inline-flex items-center gap-1.5 text-sm font-medium text-muted hover:text-ink"
      >
        <ArrowLeft className="h-4 w-4" /> Svi recepti
      </Link>
    </div>
  );
}
