import Image from "next/image";
import Link from "next/link";
import { Sekcija } from "@/components/pocetna/sekcija";
import { buttonVariants } from "@/components/ui/button";
import { IZGLED } from "@/lib/izgled";

/** „Hoću svoju korpicu!" (R1, po uzoru na farmit): fotografija korpice lijevo, veliki zeleni naslov desno. */
export function HocuKorpicu() {
  return (
    <Sekcija>
      <div className="grid items-center gap-8 md:grid-cols-2 md:gap-12">
        <div className="relative mx-auto aspect-[4/3] w-full max-w-[460px] overflow-hidden rounded-[26px]">
          <Image src={IZGLED.korpica} alt="Korpica sa domaćim povrćem" fill sizes="(max-width: 768px) 90vw, 460px" className="object-cover" />
        </div>
        <div className="text-center md:text-left">
          <h2 className="text-[44px] font-bold leading-[1.05] text-brand-600 sm:text-7xl">Hoću svoju korpicu! 🥕</h2>
          <Link href="/korpice/povrce" className={`${buttonVariants({ size: "lg" })} mt-8`}>
            Pogledaj korpice
          </Link>
        </div>
      </div>
    </Sekcija>
  );
}
