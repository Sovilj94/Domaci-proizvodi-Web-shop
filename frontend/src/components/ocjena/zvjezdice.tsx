import { Star } from "lucide-react";
import { cn } from "@/lib/utils";

/** Samo za čitanje: pet zvjezdica, prvih `ocjena` popunjeno. */
export function Zvjezdice({ ocjena, velicina = 18 }: { ocjena: number; velicina?: number }) {
  return (
    <span role="img" aria-label={`${ocjena} od 5`} className="inline-flex items-center gap-0.5">
      {[1, 2, 3, 4, 5].map((n) => (
        <Star
          key={n}
          aria-hidden="true"
          width={velicina}
          height={velicina}
          className={n <= ocjena ? "fill-honey-500 text-honey-500" : "text-line"}
        />
      ))}
    </span>
  );
}

export function zvjezdaKlasa(popunjena: boolean) {
  return cn("transition-colors", popunjena ? "fill-honey-500 text-honey-500" : "text-line");
}
