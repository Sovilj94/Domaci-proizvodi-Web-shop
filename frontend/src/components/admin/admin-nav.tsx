"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { usePathname } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import {
  ChefHat,
  ChevronDown,
  FileText,
  Coins,
  FlaskConical,
  Handshake,
  LayoutDashboard,
  LogOut,
  Mail,
  MapPinned,
  Package,
  PackageCheck,
  Quote,
  Repeat,
  ShoppingBag,
  ShoppingBasket,
  Star,
  Sprout,
  Store,
  Tags,
  TicketPercent,
  Tractor,
  Truck,
  UserRound,
  Users,
  Warehouse,
  type LucideIcon,
} from "lucide-react";
import { useAuth } from "@/lib/auth/auth-context";
import { adminApi } from "@/lib/api/endpoints";
import { narudzbeRijec, prijaveRijec } from "@/lib/orders";
import { cn } from "@/lib/utils";

type BadgeVrsta = "narudzbe" | "korpice" | "proizvodi" | "partneri";

interface NavItem {
  href: string;
  label: string;
  icon: LucideIcon;
  /** Brojka uz stavku — ključ iz `meni` sa /admin/zadaci. */
  badge?: BadgeVrsta;
}

const PREGLED: NavItem = { href: "/admin/dashboard", label: "Pregled", icon: LayoutDashboard };

/** Meni po cjelinama — redoslijed prati sedmični posao: prodaja → ponuda → dostava → kupci → sajt. */
const GRUPE: { naziv: string; stavke: NavItem[] }[] = [
  {
    naziv: "Prodaja",
    stavke: [
      { href: "/admin/orders", label: "Narudžbe", icon: ShoppingBag, badge: "narudzbe" },
      { href: "/admin/pakovanje", label: "Pakovanje", icon: PackageCheck },
      { href: "/admin/pretplate", label: "Pretplate", icon: Repeat },
    ],
  },
  {
    naziv: "Ponuda",
    stavke: [
      { href: "/admin/korpice", label: "Korpice", icon: ShoppingBasket, badge: "korpice" },
      { href: "/admin/products", label: "Proizvodi", icon: Package, badge: "proizvodi" },
      { href: "/admin/categories", label: "Kategorije", icon: Tags },
      { href: "/admin/producers", label: "Proizvođači", icon: Tractor },
    ],
  },
  {
    naziv: "Dostava",
    stavke: [
      { href: "/admin/dostava", label: "Dostava", icon: Truck },
      { href: "/admin/zone", label: "Zone i termini", icon: MapPinned },
      { href: "/admin/drivers", label: "Vozači", icon: Users },
      { href: "/admin/settings", label: "Magacin", icon: Warehouse },
    ],
  },
  {
    naziv: "Kupci",
    stavke: [
      { href: "/admin/kupci", label: "Kupci", icon: UserRound },
      { href: "/admin/poeni", label: "Poeni", icon: Coins },
      { href: "/admin/promo-kodovi", label: "Promo kodovi", icon: TicketPercent },
      { href: "/admin/ocjene", label: "Ocjene", icon: Star },
      { href: "/admin/novosti", label: "Novosti", icon: Mail },
    ],
  },
  {
    naziv: "Sadržaj sajta",
    stavke: [
      { href: "/admin/sadrzaj", label: "Kontakt i pitanja", icon: FileText },
      { href: "/admin/utisci", label: "Utisci", icon: Quote },
      { href: "/admin/recepti", label: "Recepti", icon: ChefHat },
      { href: "/admin/analize", label: "Analize", icon: FlaskConical },
      { href: "/admin/partneri", label: "Partneri", icon: Handshake, badge: "partneri" },
    ],
  },
];

const ITEMS: NavItem[] = [PREGLED, ...GRUPE.flatMap((g) => g.stavke)];

function useActive() {
  const pathname = usePathname();
  return (href: string) => pathname === href || pathname.startsWith(href + "/");
}

/**
 * Brojke uz stavke menija dolaze sa /admin/zadaci — isti ključ kao početni ekran, pa ih
 * TanStack Query spaja u jedan zahtjev. `refetchInterval` ih drži svježim dok je panel
 * otvoren (pauzira se kad je tab u pozadini); izmjene koje rješavaju zadatak invalidiraju
 * ["admin","zadaci"], pa brojka padne odmah.
 */
function useBadgeCounts(): Record<BadgeVrsta, number> {
  const { data } = useQuery({
    queryKey: ["admin", "zadaci"],
    queryFn: () => adminApi.zadaci(),
    refetchInterval: 30_000,
  });
  const m = data?.meni ?? {};
  return {
    narudzbe: m.narudzbe ?? 0,
    korpice: m.korpice ?? 0,
    proizvodi: m.proizvodi ?? 0,
    partneri: m.partneri ?? 0,
  };
}

function badgeLabel(vrsta: BadgeVrsta, count: number): string {
  switch (vrsta) {
    case "narudzbe":
      return `${count} ${narudzbeRijec(count)} na čekanju`;
    case "partneri":
      return `${count} ${prijaveRijec(count)}`;
    case "korpice":
      return `${count} korpica bez objavljenog sadržaja`;
    case "proizvodi":
      return `${count} artikala u sezoni bez zalihe`;
  }
}

const ZATVORENE_KLJUC = "epiljara.admin.zatvoreneGrupe";

/**
 * Koje su grupe menija zatvorene — pamti se po pregledaču. Grupa sa otvorenom stranicom
 * je uvijek otvorena, da se trenutna stavka ne sakrije.
 */
function useZatvoreneGrupe() {
  const [zatvorene, setZatvorene] = useState<string[]>([]);
  useEffect(() => {
    try {
      const sacuvano: unknown = JSON.parse(localStorage.getItem(ZATVORENE_KLJUC) ?? "[]");
      if (Array.isArray(sacuvano)) setZatvorene(sacuvano.filter((x): x is string => typeof x === "string"));
    } catch {
      // Bez localStorage (privatni prozor) sve grupe ostaju otvorene.
    }
  }, []);
  function prebaci(naziv: string) {
    setZatvorene((prije) => {
      const nove = prije.includes(naziv) ? prije.filter((x) => x !== naziv) : [...prije, naziv];
      try {
        localStorage.setItem(ZATVORENE_KLJUC, JSON.stringify(nove));
      } catch {
        // Nije bitno ako se ne zapamti.
      }
      return nove;
    });
  }
  return { zatvorene, prebaci };
}

function PendingBadge({
  count,
  label,
  className,
}: {
  count: number;
  label: string;
  className?: string;
}) {
  if (count <= 0) return null;
  return (
    <span
      aria-label={label}
      className={cn(
        "grid h-5 min-w-5 shrink-0 place-items-center rounded-full bg-red-500 px-1.5 text-[11px] font-bold text-white",
        className,
      )}
    >
      {count > 99 ? "99+" : count}
    </span>
  );
}

function StavkaMenija({
  item,
  aktivna,
  counts,
}: {
  item: NavItem;
  aktivna: boolean;
  counts: Record<BadgeVrsta, number>;
}) {
  return (
    <Link
      href={item.href}
      className={cn(
        "flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition",
        aktivna ? "bg-brand-600 text-white" : "text-white/70 hover:bg-white/10 hover:text-white",
      )}
    >
      <item.icon className="h-4 w-4" />
      {item.label}
      {item.badge && (
        <PendingBadge count={counts[item.badge]} label={badgeLabel(item.badge, counts[item.badge])} className="ml-auto" />
      )}
    </Link>
  );
}

export function AdminSidebar() {
  const isActive = useActive();
  const { user, logout } = useAuth();
  const counts = useBadgeCounts();
  const { zatvorene, prebaci } = useZatvoreneGrupe();

  return (
    <aside className="sticky top-0 hidden h-screen w-60 shrink-0 flex-col bg-ink text-white lg:flex print:hidden">
      <div className="flex h-16 items-center gap-2 px-5">
        <span className="grid h-8 w-8 place-items-center rounded-full bg-brand-500">
          <Sprout className="h-4 w-4" />
        </span>
        <span className="font-display font-extrabold">E piljara</span>
        <span className="ml-1 rounded bg-white/10 px-1.5 py-0.5 text-[10px] font-semibold">
          admin
        </span>
      </div>

      <nav className="flex-1 space-y-3 overflow-y-auto px-3 py-4">
        <StavkaMenija item={PREGLED} aktivna={isActive(PREGLED.href)} counts={counts} />
        {GRUPE.map((g) => {
          const sadrziAktivnu = g.stavke.some((i) => isActive(i.href));
          const otvorena = sadrziAktivnu || !zatvorene.includes(g.naziv);
          const zbir = g.stavke.reduce((n, i) => n + (i.badge ? counts[i.badge] : 0), 0);
          return (
            <div key={g.naziv}>
              <button
                type="button"
                onClick={() => !sadrziAktivnu && prebaci(g.naziv)}
                aria-expanded={otvorena}
                className="flex w-full items-center gap-2 rounded-md px-3 py-1 text-[11px] font-semibold uppercase tracking-wide text-white/40 transition hover:text-white/70"
              >
                {g.naziv}
                {!otvorena && zbir > 0 && <PendingBadge count={zbir} label={`${zbir} u grupi ${g.naziv}`} />}
                <ChevronDown
                  className={cn("ml-auto h-3.5 w-3.5 transition", !otvorena && "-rotate-90", sadrziAktivnu && "opacity-0")}
                />
              </button>
              {otvorena && (
                <div className="mt-0.5 space-y-0.5">
                  {g.stavke.map((item) => (
                    <StavkaMenija key={item.href} item={item} aktivna={isActive(item.href)} counts={counts} />
                  ))}
                </div>
              )}
            </div>
          );
        })}
      </nav>

      <div className="border-t border-white/10 p-3">
        {/*
          Izlaz iz panela je dugme sa obrubom, ne još jedan prigušeni red menija.
          Ranije je stajao u istoj sivoj kao stavke iznad i čitao se kao dio navigacije,
          pa se previđao — a jedina je stavka ovdje koja vodi van admina.
        */}
        <Link
          href="/"
          className="flex items-center justify-center gap-2 rounded-full border border-white/25 px-3 py-2.5 text-sm font-medium text-white transition hover:border-white/50 hover:bg-white/10"
        >
          <Store className="h-4 w-4" /> Nazad u shop
        </Link>
        <div className="mt-2 flex items-center justify-between px-3">
          <span className="truncate text-xs text-white/50">{user?.email}</span>
          <button
            onClick={logout}
            aria-label="Odjava"
            className="text-white/60 transition hover:text-white"
          >
            <LogOut className="h-4 w-4" />
          </button>
        </div>
      </div>
    </aside>
  );
}

export function AdminMobileBar() {
  const isActive = useActive();
  const { logout } = useAuth();
  const counts = useBadgeCounts();

  return (
    <header className="sticky top-0 z-30 border-b border-white/10 bg-ink text-white lg:hidden print:hidden">
      <div className="flex h-14 items-center justify-between px-4">
        <span className="font-display font-bold">
          E piljara{" "}
          <span className="text-xs font-medium text-white/50">admin</span>
        </span>
        {/*
          Na telefonu izlaza u shop nije ni bilo — sidebar sa tim linkom je skriven
          ispod `lg`, pa se iz panela moglo izaći samo ručnim kucanjem adrese.
          U traci nema mjesta za natpis, zato ikona sa aria-label.
        */}
        <div className="flex items-center gap-1">
          <Link
            href="/"
            aria-label="Nazad u shop"
            className="grid h-9 w-9 place-items-center rounded-full text-white/70 transition hover:bg-white/10 hover:text-white"
          >
            <Store className="h-4 w-4" />
          </Link>
          <button
            onClick={logout}
            aria-label="Odjava"
            className="grid h-9 w-9 place-items-center rounded-full text-white/70 transition hover:bg-white/10 hover:text-white"
          >
            <LogOut className="h-4 w-4" />
          </button>
        </div>
      </div>
      <nav className="no-scrollbar flex gap-1 overflow-x-auto px-2 pb-2">
        {ITEMS.map((item) => (
          <Link
            key={item.href}
            href={item.href}
            className={cn(
              "flex items-center gap-1.5 whitespace-nowrap rounded-lg px-3 py-1.5 text-sm font-medium",
              isActive(item.href) ? "bg-brand-600" : "text-white/70",
            )}
          >
            <item.icon className="h-4 w-4" />
            {item.label}
            {item.badge && (
              <PendingBadge
                count={counts[item.badge]}
                label={badgeLabel(item.badge, counts[item.badge])}
              />
            )}
          </Link>
        ))}
      </nav>
    </header>
  );
}
