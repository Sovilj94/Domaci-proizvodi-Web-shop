"use client";

import Image from "next/image";
import Link from "next/link";
import { Sekcija } from "@/components/pocetna/sekcija";
import { buttonVariants } from "@/components/ui/button";
import { useBlokovi } from "@/lib/api/hooks";

/**
 * „Hoću svoju korpicu!" (R1, po uzoru na farmit): fotografija korpice lijevo, veliki zeleni naslov
 * desno. Sve je u bazi (sekcija „pocetna-korpica"): naslov, opis slike (tekst), dugme, link, slika.
 */
export function HocuKorpicu() {
  const { blokovi } = useBlokovi("pocetna-korpica");
  const b = blokovi[0];
  if (!b) return null;
  return (
    <Sekcija>
      <div className="grid items-center gap-8 md:grid-cols-2 md:gap-12">
        {b.slikaUrl && (
          <div className="relative mx-auto aspect-[4/3] w-full max-w-[460px] overflow-hidden rounded-[26px]">
            <Image src={b.slikaUrl} alt={b.tekst ?? ""} fill sizes="(max-width: 768px) 90vw, 460px" className="object-cover" />
          </div>
        )}
        <div className="text-center md:text-left">
          {b.naslov && <h2 className="text-[44px] font-bold leading-[1.05] text-brand-600 sm:text-7xl">{b.naslov}</h2>}
          {b.dugme && b.link && (
            <Link href={b.link} className={`${buttonVariants({ size: "lg" })} mt-8`}>
              {b.dugme}
            </Link>
          )}
        </div>
      </div>
    </Sekcija>
  );
}
