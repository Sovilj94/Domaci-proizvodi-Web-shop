import { cn } from "@/lib/utils";

/**
 * Naslov stranice po uzoru na farmit (R4): centriran, 60px na desktopu i 32px na telefonu,
 * opis ispod. Isti oblik kao „Izaberi korpicu" i „Proizvodi čije porijeklo znaš".
 */
export function PageHeader({
  naslov,
  opis,
  className,
}: {
  naslov: React.ReactNode;
  opis?: React.ReactNode;
  className?: string;
}) {
  return (
    <div className={cn("mx-auto mb-10 max-w-3xl text-center sm:mb-14", className)}>
      <h1 className="text-[32px] font-bold leading-tight text-ink sm:text-[60px] sm:leading-[64px]">{naslov}</h1>
      {opis && <p className="mx-auto mt-5 max-w-2xl text-base text-ink sm:text-xl">{opis}</p>}
    </div>
  );
}
