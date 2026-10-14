import Link from "next/link";
import { ChefHat, Clock, Users } from "lucide-react";
import { SmartImage } from "@/components/ui/smart-image";
import { porcijeTekst } from "@/lib/recepti";
import { TEZINA_TEKST, type ReceptKratko } from "@/types/domain";

export function ReceptMeta({ recept }: { recept: ReceptKratko }) {
  return (
    <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-muted">
      <span className="inline-flex items-center gap-1.5">
        <Clock className="h-4 w-4" aria-hidden /> {recept.vrijemeMinuta} min
      </span>
      <span className="inline-flex items-center gap-1.5">
        <Users className="h-4 w-4" aria-hidden /> {porcijeTekst(recept.porcije)}
      </span>
      <span className="inline-flex items-center gap-1.5">
        <ChefHat className="h-4 w-4" aria-hidden /> {TEZINA_TEKST[recept.tezina] ?? recept.tezina}
      </span>
    </div>
  );
}

export function ReceptKartica({ recept }: { recept: ReceptKratko }) {
  return (
    <Link
      href={`/recepti/${recept.slug}`}
      className="group flex flex-col overflow-hidden rounded-card border border-line bg-surface shadow-card transition hover:shadow-lg"
    >
      <div className="relative aspect-[4/3] overflow-hidden bg-sand">
        <SmartImage
          src={recept.slikaUrl}
          alt={recept.naslov}
          sizes="(max-width:640px) 100vw, (max-width:1024px) 50vw, 33vw"
        />
      </div>
      <div className="flex flex-1 flex-col gap-2 p-4">
        <h2 className="font-display text-lg font-semibold text-ink group-hover:text-brand-700">{recept.naslov}</h2>
        <p className="line-clamp-2 text-sm text-ink/80">{recept.opis}</p>
        <div className="mt-auto pt-2">
          <ReceptMeta recept={recept} />
        </div>
      </div>
    </Link>
  );
}
