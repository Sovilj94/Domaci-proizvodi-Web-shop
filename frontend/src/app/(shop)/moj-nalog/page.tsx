import Link from "next/link";
import {
  MapPin,
  RefreshCw,
  ShoppingBag,
  ShoppingBasket,
  User,
  type LucideIcon,
} from "lucide-react";

interface Plocica {
  href: string;
  label: string;
  description: string;
  icon: LucideIcon;
}

// Žive pločice — vode na radnju, ovo nije forma. Sadržaj iz Zadatka 4-6 popunjava
// mete ovih linkova; `/moj-nalog/narudzbe` do tada 404uje (odluka, ne previd — vidi
// task-3-report.md).
const PLOCICE: Plocica[] = [
  {
    href: "/moj-nalog/podaci",
    label: "Izmijeni podatke",
    // Lozinka se u P2a NE mijenja — `podaci` je prikazuje sivo, sa objašnjenjem zašto.
    // Obećati je ovdje znači poslati kupca po nešto što tamo ne postoji.
    description: "Ime i broj telefona na kojem te traži vozač.",
    icon: User,
  },
  {
    href: "/moj-nalog/adrese",
    label: "Moje adrese",
    description: "Sačuvaj adrese za bržu i preciznu dostavu.",
    icon: MapPin,
  },
  {
    href: "/moj-nalog/narudzbe",
    label: "Moje narudžbe",
    description: "Pregledaj prošle i tekuće narudžbe.",
    icon: ShoppingBag,
  },
  {
    href: "/korpice",
    label: "Korpice u ponudi",
    description: "Pogledaj šta je ova sedmica donijela sa Manjače.",
    icon: ShoppingBasket,
  },
  {
    href: "/moj-nalog/korpica",
    label: "Izmjena sadržaja korpice",
    description: "Podesi šta ide u tvoju korpicu prije dostave.",
    icon: ShoppingBasket,
  },
];

const USKORO: Plocica[] = [
  {
    href: "/moj-nalog/pretplate",
    label: "Pretplate",
    description: "Redovna dostava bez naručivanja iznova.",
    icon: RefreshCw,
  },
];

export default function MojNalogPage() {
  return (
    <div>
      <h1 className="font-display text-2xl font-bold text-ink md:text-3xl">Pregled</h1>
      <p className="mt-1 text-muted">Sve o tvom nalogu, na jednom mjestu.</p>

      <div className="mt-6 grid gap-4 sm:grid-cols-2">
        {PLOCICE.map((p) => (
          <Link
            key={p.href}
            href={p.href}
            className="flex items-start gap-4 rounded-card border border-line bg-surface p-5 shadow-card transition hover:border-brand-500"
          >
            <span className="grid h-11 w-11 shrink-0 place-items-center rounded-full bg-sand text-brand-600">
              <p.icon className="h-5 w-5" />
            </span>
            <div>
              <h2 className="font-display font-semibold text-ink">{p.label}</h2>
              <p className="mt-1 text-sm text-muted">{p.description}</p>
            </div>
          </Link>
        ))}

        {USKORO.map((p) => (
          <div
            key={p.href}
            className="flex items-start gap-4 rounded-card border border-line bg-surface/60 p-5"
          >
            <span className="grid h-11 w-11 shrink-0 place-items-center rounded-full bg-sand text-muted">
              <p.icon className="h-5 w-5" />
            </span>
            <div>
              <div className="flex items-center gap-2">
                <h2 className="font-display font-semibold text-ink/70">{p.label}</h2>
                <span className="rounded-full bg-sand px-2 py-0.5 text-[10px] font-semibold text-muted">
                  uskoro
                </span>
              </div>
              <p className="mt-1 text-sm text-muted">{p.description}</p>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
