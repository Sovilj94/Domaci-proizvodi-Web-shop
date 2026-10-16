import Image from "next/image";
import { cn } from "@/lib/utils";

/**
 * Obojena pločica namirnice iz sadržaja korpice (R1, po uzoru na farmit): boja namirnice
 * kao pozadina, ime gore lijevo, slika dole. Boju šalje server (`BojaProizvoda`); bez nje
 * (stariji API) pločica je krem.
 */
export function PlocicaNamirnice({
  naziv,
  slika,
  boja,
  className,
}: {
  naziv: string;
  slika?: string;
  boja?: string;
  className?: string;
}) {
  return (
    <div
      className={cn("relative flex aspect-square flex-col overflow-hidden rounded-xl p-2", className)}
      style={{ backgroundColor: boja ?? "var(--color-beige)" }}
    >
      <span className="relative z-10 text-[11px] font-semibold leading-tight text-ink sm:text-xs">{naziv}</span>
      {slika && (
        <div className="relative mx-auto mt-auto h-[58%] w-[78%] overflow-hidden rounded-lg">
          <Image src={slika} alt="" fill sizes="96px" className="object-cover" />
        </div>
      )}
    </div>
  );
}
