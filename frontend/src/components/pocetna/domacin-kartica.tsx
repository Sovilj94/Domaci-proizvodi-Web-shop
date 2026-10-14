import Link from "next/link";
import { SmartImage } from "@/components/ui/smart-image";
import type { Producer } from "@/types/domain";

/** Kartica domaćina; cijela je link na njegovu stranicu. */
export function DomacinKartica({ producer: p }: { producer: Producer }) {
  return (
    <Link
      href={`/proizvodjaci/${p.id}`}
      className="block overflow-hidden rounded-card border border-line bg-surface transition hover:shadow-md"
    >
      <div className="relative aspect-[4/3] bg-sand">
        <SmartImage src={p.photoUrl} alt={p.name} sizes="(max-width: 640px) 50vw, 25vw" />
      </div>
      <div className="p-3.5">
        <p className="text-sm font-semibold text-ink">{p.name}</p>
        <p className="mt-0.5 text-xs text-muted">{p.village}</p>
        {(p.artikli?.length ?? 0) > 0 && (
          <p className="mt-1.5 text-xs font-medium text-brand-700">{p.artikli?.join(" · ")}</p>
        )}
        <p className="mt-1.5 line-clamp-2 text-xs text-muted">{p.description}</p>
      </div>
    </Link>
  );
}
