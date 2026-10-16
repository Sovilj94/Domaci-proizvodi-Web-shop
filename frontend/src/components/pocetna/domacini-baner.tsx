import Image from "next/image";
import { IZGLED } from "@/lib/izgled";

/** Fotografija preko cijele širine sa porukom o domaćinima (R1, po uzoru na farmit). */
export function DomaciniBaner() {
  return (
    <section className="relative flex min-h-[420px] items-center justify-center overflow-hidden px-6 py-24 sm:min-h-[560px]">
      <Image src={IZGLED.domacini} alt="" fill sizes="100vw" className="object-cover" />
      <div className="absolute inset-0 bg-black/40" />
      <div className="relative max-w-2xl text-center text-white">
        <h2 className="text-[34px] font-bold leading-tight sm:text-6xl">Iza svake namirnice stoji domaćin</h2>
        <p className="mt-5 text-base leading-relaxed text-white/95 sm:text-lg">
          Naši domaćini su porodična gazdinstva sa Manjače i okoline. Svako ima svoje ime, selo i
          namirnice koje najbolje radi — zato znaš odakle stiže ono što jedeš.
        </p>
      </div>
    </section>
  );
}
