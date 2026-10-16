import Link from "next/link";
import { buttonVariants } from "@/components/ui/button";

/** 404 po uzoru na farmit izgled (R4): veliki naslov, kratak tekst, dva puta dalje. */
export default function NijePronadjeno() {
  return (
    <section className="px-4 py-20 sm:py-28">
      <div className="mx-auto max-w-xl text-center">
        <p className="text-6xl" aria-hidden>
          🥕
        </p>
        <h1 className="mt-6 text-[32px] font-bold leading-tight text-ink sm:text-5xl">Ova stranica ne postoji</h1>
        <p className="mt-4 text-base text-ink/80 sm:text-lg">
          Možda je link zastario ili je adresa pogrešno otkucana. Korpice i Špajz su i dalje tu.
        </p>
        <div className="mt-8 flex flex-wrap justify-center gap-3">
          <Link href="/korpice/povrce" className={buttonVariants({ size: "lg" })}>
            Pogledaj korpice
          </Link>
          <Link href="/" className={buttonVariants({ variant: "secondary", size: "lg" })}>
            Početna
          </Link>
        </div>
      </div>
    </section>
  );
}
