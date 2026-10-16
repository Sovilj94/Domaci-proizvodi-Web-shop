import Link from "next/link";
import { cn } from "@/lib/utils";

/**
 * Naslov sekcije po uzoru na farmit (R1), u dva oblika:
 *   „centar" — veliki centriran naslov sekcije (48px, mobilni 28px) sa opisom ispod,
 *              npr. „Ova sedmica", „Kako funkcioniše?"
 *   „red"    — manji naslov lijevo i link desno, npr. „Zavolećeš ove proizvode… · Svi proizvodi"
 */
export function SectionHeader({
  naslov,
  opis,
  link,
  linkTekst = "Prikaži sve",
  oblik = "centar",
  className,
}: {
  naslov: React.ReactNode;
  opis?: React.ReactNode;
  link?: string;
  linkTekst?: string;
  oblik?: "centar" | "red";
  className?: string;
}) {
  if (oblik === "red") {
    return (
      <div className={cn("mb-5 flex items-end justify-between gap-6", className)}>
        <h2 className="text-lg font-semibold text-ink sm:text-xl">{naslov}</h2>
        {link && (
          <Link href={link} className="shrink-0 text-sm text-muted transition hover:text-ink sm:text-base">
            {linkTekst}
          </Link>
        )}
      </div>
    );
  }

  return (
    <div className={cn("mb-8 text-center sm:mb-12", className)}>
      <h2 className="text-[28px] font-bold leading-tight text-ink sm:text-5xl">{naslov}</h2>
      {opis && <p className="mx-auto mt-4 max-w-2xl text-base text-ink/80 sm:text-lg">{opis}</p>}
    </div>
  );
}
