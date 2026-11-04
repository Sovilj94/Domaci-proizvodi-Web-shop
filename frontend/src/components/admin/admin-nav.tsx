"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { usePathname } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import {
  Calculator,
  CalendarDays,
  ChartColumn,
  ChefHat,
  ChevronDown,
  ClipboardList,
  Search,
  Send,
  FileText,
  Coins,
  FlaskConical,
  Handshake,
  LayoutDashboard,
  LogOut,
  Mail,
  MapPinned,
  Package,
  PanelLeftClose,
  PanelLeftOpen,
  PackageCheck,
  Quote,
  Receipt,
  ReceiptText,
  Repeat,
  ShoppingBag,
  ShoppingBasket,
  SlidersHorizontal,
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
import { OTVORI_PALETU } from "@/lib/admin/paleta";
import { narudzbeRijec, prijaveRijec } from "@/lib/orders";
import { cn } from "@/lib/utils";

type BadgeVrsta = "narudzbe" | "nabavka" | "korpice" | "proizvodi" | "partneri";

export interface NavItem {
  href: string;
  label: string;
  icon: LucideIcon;
  /** Brojka uz stavku — ključ iz `meni` sa /admin/zadaci. */
  badge?: BadgeVrsta;
}

const PREGLED: NavItem = { href: "/admin/dashboard", label: "Pregled", icon: LayoutDashboard };
const POSLOVANJE: NavItem = { href: "/admin/poslovanje", label: "Poslovanje", icon: ChartColumn };
const TROSKOVI: NavItem = { href: "/admin/poslovanje/troskovi", label: "Troškovi", icon: Receipt };

/** Meni po cjelinama — redoslijed prati sedmični posao: prodaja → ponuda → dostava → kupci → sajt. */
const GRUPE: { naziv: string; stavke: NavItem[] }[] = [
  {
    naziv: "Prodaja",
    stavke: [
      { href: "/admin/orders", label: "Narudžbe", icon: ShoppingBag, badge: "narudzbe" },
      { href: "/admin/nabavka", label: "Nabavka", icon: ClipboardList, badge: "nabavka" },
      { href: "/admin/pakovanje", label: "Pakovanje", icon: PackageCheck },
      { href: "/admin/pretplate", label: "Pretplate", icon: Repeat },
      { href: "/admin/fiskalni-racuni", label: "Fiskalni računi", icon: ReceiptText },
    ],
  },
  {
    naziv: "Ponuda",
    stavke: [
      { href: "/admin/korpice", label: "Korpice", icon: ShoppingBasket, badge: "korpice" },
      { href: "/admin/korpice/postavke", label: "Postavke korpica", icon: SlidersHorizontal },
      { href: "/admin/korpice/obracun", label: "Obračun cijena", icon: Calculator },
      { href: "/admin/korpice/kalendar", label: "Sezonski kalendar", icon: CalendarDays },
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
      { href: "/admin/mejlovi", label: "Mejlovi", icon: Send },
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

export const ITEMS: NavItem[] = [PREGLED, POSLOVANJE, TROSKOVI, ...GRUPE.flatMap((g) => g.stavke)];

function useActive() {
  const pathname = usePathname();
  const pogadja = (href: string) => pathname === href || pathname.startsWith(href + "/");
  // Svijetli samo najtačnija stavka: na /admin/korpice/postavke to su „Postavke korpica“, ne i „Korpice“.
  const sve = ITEMS.map((s) => s.href);
  return (href: string) => pogadja(href) && !sve.some((h) => h !== href && h.startsWith(href + "/") && pogadja(h));
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
    nabavka: m.nabavka ?? 0,
    korpice: m.korpice ?? 0,
    proizvodi: m.proizvodi ?? 0,
    partneri: m.partneri ?? 0,
  };
}

function badgeLabel(vrsta: BadgeVrsta, count: number): string {
  switch (vrsta) {
    case "narudzbe":
      return `${count} ${narudzbeRijec(count)} na čekanju`;
    case "nabavka":
      return `${count} artikala za nabavku`;
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

const SKLOPLJEN_KLJUC = "epiljara.admin.meniSklopljen";

/** Meni sklopljen na same ikone — pamti se po pregledaču. */
function useSklopljen() {
  const [sklopljen, setSklopljen] = useState(false);
  useEffect(() => {
    try {
      setSklopljen(localStorage.getItem(SKLOPLJEN_KLJUC) === "1");
    } catch {
      // Bez localStorage meni ostaje raširen.
    }
  }, []);
  function prebaci() {
    setSklopljen((prije) => {
      try {
        localStorage.setItem(SKLOPLJEN_KLJUC, prije ? "0" : "1");
      } catch {
        // Nije bitno ako se ne zapamti.
      }
      return !prije;
    });
  }
  return { sklopljen, prebaci };
}

function StavkaMenija({
  item,
  aktivna,
  counts,
  sklopljen = false,
}: {
  item: NavItem;
  aktivna: boolean;
  counts: Record<BadgeVrsta, number>;
  sklopljen?: boolean;
}) {
  const broj = item.badge ? counts[item.badge] : 0;
  return (
    <Link
      href={item.href}
      title={sklopljen ? item.label : undefined}
      aria-current={aktivna ? "page" : undefined}
      className={cn(
        "group relative flex items-center gap-3 rounded-lg py-2 text-sm font-medium transition",
        sklopljen ? "justify-center px-0" : "px-3",
        aktivna ? "bg-white/10 text-white" : "text-white/65 hover:bg-white/5 hover:text-white",
      )}
    >
      {/* Aktivna stavka: zelena traka uz lijevu ivicu i zelena ikona. */}
      {aktivna && <span aria-hidden className="absolute inset-y-1.5 left-0 w-1 rounded-r-full bg-brand-400" />}
      <item.icon className={cn("h-[18px] w-[18px] shrink-0 transition", aktivna ? "text-brand-300" : "text-white/50 group-hover:text-white/80")} />
      {!sklopljen && <span className="truncate">{item.label}</span>}
      {item.badge &&
        (sklopljen ? (
          broj > 0 && <span aria-label={badgeLabel(item.badge, broj)} className="absolute right-2.5 top-1.5 h-2 w-2 rounded-full bg-red-500 ring-2 ring-ink" />
        ) : (
          <PendingBadge count={broj} label={badgeLabel(item.badge, broj)} className="ml-auto" />
        ))}
    </Link>
  );
}

/** Inicijali za avatar u dnu menija (admin@epiljara.ba → AD). */
function inicijali(email?: string | null): string {
  const ime = (email ?? "").split("@")[0].replace(/[^a-zA-ZčćšđžČĆŠĐŽ]/g, "");
  return (ime.slice(0, 2) || "A").toUpperCase();
}

/**
 * Bočni meni admina. Fiksiran je uz lijevu ivicu cijelom visinom ekrana (ne `sticky`: omotač
 * stranice je visok jedan ekran, pa je ljepljivi meni pri skrolovanju odlazio sa stranicom i
 * ostavljao bijelo). Razmaknica iste širine drži sadržaj desno od njega. Može se sklopiti na
 * same ikone — pamti se po pregledaču.
 */
export function AdminSidebar() {
  const isActive = useActive();
  const { user, logout } = useAuth();
  const counts = useBadgeCounts();
  const { zatvorene, prebaci } = useZatvoreneGrupe();
  const { sklopljen, prebaci: prebaciSklopljen } = useSklopljen();
  const sirina = sklopljen ? "w-[4.5rem]" : "w-64";

  return (
    <>
      {/* Razmaknica: zauzima mjesto menija u redu, jer je sam meni fiksiran. */}
      <div aria-hidden className={cn("hidden shrink-0 transition-[width] duration-200 lg:block print:hidden", sirina)} />

      <aside
        className={cn(
          "fixed inset-y-0 left-0 z-40 hidden flex-col bg-gradient-to-b from-ink to-[#1f2a20] text-white shadow-xl shadow-black/10 transition-[width] duration-200 lg:flex print:hidden",
          sirina,
        )}
      >
        <div className={cn("flex h-16 shrink-0 items-center gap-2", sklopljen ? "justify-center px-2" : "px-5")}>
          <Link href="/admin/dashboard" className="flex min-w-0 items-center gap-2" title="Pregled">
            <span className="grid h-9 w-9 shrink-0 place-items-center rounded-xl bg-brand-500 shadow-inner">
              <Sprout className="h-[18px] w-[18px]" />
            </span>
            {!sklopljen && (
              <>
                <span className="font-display font-extrabold">E piljara</span>
                <span className="rounded bg-white/10 px-1.5 py-0.5 text-[10px] font-semibold">admin</span>
              </>
            )}
          </Link>
          {!sklopljen && (
            <button
              type="button"
              onClick={prebaciSklopljen}
              aria-label="Sklopi meni"
              title="Sklopi meni"
              className="ml-auto grid h-8 w-8 place-items-center rounded-lg text-white/50 transition hover:bg-white/10 hover:text-white"
            >
              <PanelLeftClose className="h-4 w-4" />
            </button>
          )}
        </div>

        <div className={cn("shrink-0", sklopljen ? "px-2" : "px-3")}>
          {sklopljen ? (
            <button
              type="button"
              onClick={prebaciSklopljen}
              aria-label="Raširi meni"
              title="Raširi meni"
              className="mb-1 grid h-9 w-full place-items-center rounded-lg text-white/50 transition hover:bg-white/10 hover:text-white"
            >
              <PanelLeftOpen className="h-4 w-4" />
            </button>
          ) : null}
          <button
            type="button"
            onClick={() => window.dispatchEvent(new Event(OTVORI_PALETU))}
            aria-label="Traži"
            title={sklopljen ? "Traži (Ctrl K)" : undefined}
            className={cn(
              "flex w-full items-center gap-2 rounded-lg bg-white/[0.07] py-2 text-sm text-white/55 ring-1 ring-white/10 transition hover:bg-white/10 hover:text-white",
              sklopljen ? "justify-center px-0" : "px-3",
            )}
          >
            <Search className="h-4 w-4 shrink-0" />
            {!sklopljen && (
              <>
                Traži…
                <kbd className="ml-auto rounded bg-white/10 px-1.5 py-0.5 text-[10px] font-semibold">Ctrl K</kbd>
              </>
            )}
          </button>
        </div>

        <nav
          className={cn(
            "mt-3 flex-1 space-y-4 overflow-y-auto overscroll-contain pb-4 [scrollbar-color:rgba(255,255,255,0.15)_transparent] [scrollbar-width:thin]",
            sklopljen ? "px-2" : "px-3",
          )}
        >
          <StavkaMenija item={PREGLED} aktivna={isActive(PREGLED.href)} counts={counts} sklopljen={sklopljen} />
          <StavkaMenija item={POSLOVANJE} aktivna={isActive(POSLOVANJE.href)} counts={counts} sklopljen={sklopljen} />
          <StavkaMenija item={TROSKOVI} aktivna={isActive(TROSKOVI.href)} counts={counts} sklopljen={sklopljen} />
          {GRUPE.map((g) => {
            const sadrziAktivnu = g.stavke.some((i) => isActive(i.href));
            // Sklopljen meni pokazuje sve ikone — grupe se tada ne zatvaraju.
            const otvorena = sklopljen || sadrziAktivnu || !zatvorene.includes(g.naziv);
            const zbir = g.stavke.reduce((n, i) => n + (i.badge ? counts[i.badge] : 0), 0);
            return (
              <div key={g.naziv}>
                {sklopljen ? (
                  <div aria-hidden className="mx-3 mb-2 border-t border-white/10" />
                ) : (
                  <button
                    type="button"
                    onClick={() => !sadrziAktivnu && prebaci(g.naziv)}
                    aria-expanded={otvorena}
                    className="mb-1 flex w-full items-center gap-2 rounded-md px-3 py-1 text-[11px] font-semibold uppercase tracking-wider text-white/35 transition hover:text-white/70"
                  >
                    {g.naziv}
                    {!otvorena && zbir > 0 && <PendingBadge count={zbir} label={`${zbir} u grupi ${g.naziv}`} />}
                    <ChevronDown
                      className={cn("ml-auto h-3.5 w-3.5 transition", !otvorena && "-rotate-90", sadrziAktivnu && "opacity-0")}
                    />
                  </button>
                )}
                {otvorena && (
                  <div className="space-y-0.5">
                    {g.stavke.map((item) => (
                      <StavkaMenija key={item.href} item={item} aktivna={isActive(item.href)} counts={counts} sklopljen={sklopljen} />
                    ))}
                  </div>
                )}
              </div>
            );
          })}
        </nav>

        <div className={cn("shrink-0 border-t border-white/10", sklopljen ? "space-y-1 p-2" : "p-3")}>
          {/*
            Izlaz iz panela je dugme sa obrubom, ne još jedan prigušeni red menija — jedina je
            stavka ovdje koja vodi van admina.
          */}
          <Link
            href="/"
            title={sklopljen ? "Nazad u shop" : undefined}
            aria-label="Nazad u shop"
            className={cn(
              "flex items-center justify-center gap-2 rounded-full border border-white/25 py-2.5 text-sm font-medium text-white transition hover:border-white/50 hover:bg-white/10",
              sklopljen ? "px-0" : "px-3",
            )}
          >
            <Store className="h-4 w-4" /> {!sklopljen && "Nazad u shop"}
          </Link>
          <div className={cn("flex items-center gap-2.5", sklopljen ? "flex-col pt-1" : "mt-3 px-1")}>
            <span
              title={user?.email ?? undefined}
              className="grid h-8 w-8 shrink-0 place-items-center rounded-full bg-brand-600/80 text-[11px] font-bold"
            >
              {inicijali(user?.email)}
            </span>
            {!sklopljen && <span className="min-w-0 flex-1 truncate text-xs text-white/55">{user?.email}</span>}
            <button
              onClick={logout}
              aria-label="Odjava"
              title="Odjava"
              className="grid h-8 w-8 shrink-0 place-items-center rounded-lg text-white/55 transition hover:bg-white/10 hover:text-white"
            >
              <LogOut className="h-4 w-4" />
            </button>
          </div>
        </div>
      </aside>
    </>
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
          <button
            type="button"
            onClick={() => window.dispatchEvent(new Event(OTVORI_PALETU))}
            aria-label="Traži"
            className="grid h-9 w-9 place-items-center rounded-full text-white/70 transition hover:bg-white/10 hover:text-white"
          >
            <Search className="h-4 w-4" />
          </button>
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
