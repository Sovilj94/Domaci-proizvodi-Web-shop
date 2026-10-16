import Link from "next/link";
import { ChevronRight } from "lucide-react";
import { Sekcija } from "@/components/pocetna/sekcija";
import { SectionHeader } from "@/components/ui/section-header";

const PLOCICE = [
  { href: "/korpice/povrce", naziv: "Povrće", emoji: "🥦", pozadina: "#9DCB9D", tekst: "#1B701B" },
  { href: "/korpice/voce", naziv: "Voće", emoji: "🍎", pozadina: "#F8CFAE", tekst: "#C25B03" },
  { href: "/spajz", naziv: "Špajz", emoji: "🏪", pozadina: "#D9A88A", tekst: "#7A3E1C" },
] as const;

/** „Sa njive do tvog stola" (R1, po uzoru na farmit): tri obojene pločice-dugmeta na krem pojasu. */
export function NjivaDoStola() {
  return (
    <Sekcija ton="krem">
      <SectionHeader
        naslov="Sa njive do tvog stola"
        opis="Pogledaj naše korpice povrća i voća i izaberi šta ti treba iz Špajza."
      />
      <div className="mx-auto grid max-w-3xl gap-4 sm:grid-cols-3">
        {PLOCICE.map((p) => (
          <Link
            key={p.href}
            href={p.href}
            className="flex h-[72px] items-center gap-3 rounded-2xl px-5 text-xl font-semibold shadow-[0_6px_16px_rgba(64,74,61,0.18)] transition hover:scale-[1.02]"
            style={{ backgroundColor: p.pozadina, color: p.tekst }}
          >
            <span className="text-3xl" aria-hidden>
              {p.emoji}
            </span>
            <span className="flex-1">{p.naziv}</span>
            <ChevronRight className="h-6 w-6" />
          </Link>
        ))}
      </div>
    </Sekcija>
  );
}
