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
