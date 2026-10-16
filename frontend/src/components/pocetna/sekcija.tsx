import { cn } from "@/lib/utils";

/**
 * Pojas stranice po uzoru na farmit (R1): bijeli ili krem (`beige`), puna širina,
 * unutrašnja kolona. `luk` daje krem pojasu zaobljenu gornju ivicu kao na uzoru
 * („Kako funkcioniše?", „Česta pitanja").
 */
export function Sekcija({
  children,
  className,
  id,
  ton = "bijela",
  luk = false,
  siroka = false,
}: {
  children: React.ReactNode;
  className?: string;
  id?: string;
  ton?: "bijela" | "krem";
  luk?: boolean;
  siroka?: boolean;
}) {
  return (
    <section
      id={id}
      className={cn(
        "relative px-4 py-12 sm:py-24",
        ton === "krem" ? "bg-beige" : "bg-white",
        luk && "rounded-t-[50%_48px] sm:rounded-t-[50%_80px]",
        className,
      )}
    >
      <div className={cn("mx-auto", siroka ? "max-w-7xl" : "max-w-6xl")}>{children}</div>
    </section>
  );
}
