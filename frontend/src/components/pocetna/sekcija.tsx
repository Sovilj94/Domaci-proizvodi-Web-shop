import { cn } from "@/lib/utils";

/** Zajednički okvir sekcije početne: puna širina, unutrašnja kolona, isti ritam svuda. */
export function Sekcija({
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
