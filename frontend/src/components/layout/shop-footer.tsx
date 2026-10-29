import Link from "next/link";
import { Logo } from "@/components/layout/logo";
import { FooterRok } from "@/components/layout/footer-rok";
import { PrijavaNovosti } from "@/components/layout/prijava-novosti";
import { JezikPrekidac } from "@/components/jezik/jezik-prekidac";
import { KontaktEmail, KontaktPolje, KontaktTelefon } from "@/components/layout/kontakt";

const LINKOVI = [
  { href: "/o-nama", naziv: "O nama" },
  { href: "/korpice", naziv: "Korpice" },
  { href: "/spajz", naziv: "Špajz" },
  { href: "/proizvodjaci", naziv: "Naši domaćini" },
  { href: "/postani-partner", naziv: "Postani partner" },
];

/**
 * Footer po uzoru na farmit (R1): bijel, sa karticom „Prijavi se i budi u toku" koja
 * preklapa granicu sa krem pojasom iznad, red linkova, pa logo, rok i kontakt.
 */
export function ShopFooter() {
  return (
    <footer className="relative bg-white pb-8">
      {/* Krem pojas iza gornje polovine kartice — kartica „preklapa" granicu kao na uzoru. */}
      <div className="bg-[linear-gradient(to_bottom,var(--color-beige)_50%,white_50%)] px-4">
        <div className="mx-auto max-w-6xl">
          <PrijavaNovosti />
        </div>
      </div>
      <div className="mx-auto max-w-6xl px-4">

        <div className="mt-10 flex flex-col gap-6 border-b border-line pb-6 sm:flex-row sm:items-center sm:justify-between">
          <nav className="flex flex-wrap gap-x-8 gap-y-3 text-sm text-ink">
            {LINKOVI.map((l) => (
              <Link key={l.href} href={l.href} className="transition hover:text-honey-500">
                {l.naziv}
              </Link>
            ))}
          </nav>
          <JezikPrekidac className="self-start rounded-full border border-line px-4 py-1.5 text-sm text-ink hover:border-honey-500 sm:self-auto" />
        </div>

        <div className="mt-6 grid gap-8 sm:grid-cols-3 sm:items-center">
          <ul className="space-y-2 text-sm text-muted">
            <FooterRok />
          </ul>
          <div className="sm:justify-self-center">
            <Logo />
          </div>
          <div className="space-y-1.5 text-sm text-muted sm:text-right">
            <p>
              Kontakt:{" "}
              <KontaktTelefon className="text-ink underline underline-offset-2 hover:text-honey-500" />
            </p>
            <p>
              <KontaktEmail className="text-ink underline underline-offset-2 hover:text-honey-500" />
            </p>
            <p>
              <KontaktPolje polje="adresa" />
            </p>
            <p>
              © {new Date().getFullYear()} E piljara · <KontaktPolje polje="podrucje" />
            </p>
          </div>
        </div>
      </div>
    </footer>
  );
}
