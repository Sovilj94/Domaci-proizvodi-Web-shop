"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import {
  ChefHat,
  FlaskConical,
  Handshake,
  LayoutDashboard,
  LogOut,
  Mail,
  Package,
  Quote,
  ShoppingBag,
  ShoppingBasket,
  Star,
  Sprout,
  Store,
  Tags,
  TicketPercent,
  Tractor,
  Truck,
  Users,
  Warehouse,
  type LucideIcon,
} from "lucide-react";
import { useAuth } from "@/lib/auth/auth-context";
import { adminApi } from "@/lib/api/endpoints";
import { queryKeys } from "@/lib/api/hooks";
import { narudzbeRijec, prijaveRijec } from "@/lib/orders";
import { cn } from "@/lib/utils";

type BadgeVrsta = "narudzbe" | "partneri";

interface NavItem {
  href: string;
  label: string;
  icon: LucideIcon;
  /** Vrsta badge-a: narudžbe koje čekaju potvrdu ili nove prijave partnera. */
  badge?: BadgeVrsta;
}

const ITEMS: NavItem[] = [
  { href: "/admin/dashboard", label: "Pregled", icon: LayoutDashboard },
  { href: "/admin/orders", label: "Narudžbe", icon: ShoppingBag, badge: "narudzbe" },
  { href: "/admin/products", label: "Proizvodi", icon: Package },
  { href: "/admin/korpice", label: "Korpice", icon: ShoppingBasket },
  { href: "/admin/categories", label: "Kategorije", icon: Tags },
  { href: "/admin/producers", label: "Proizvođači", icon: Tractor },
  { href: "/admin/utisci", label: "Utisci", icon: Quote },
  { href: "/admin/ocjene", label: "Ocjene", icon: Star },
  { href: "/admin/promo-kodovi", label: "Promo kodovi", icon: TicketPercent },
  { href: "/admin/recepti", label: "Recepti", icon: ChefHat },
  { href: "/admin/analize", label: "Analize", icon: FlaskConical },
  { href: "/admin/partneri", label: "Partneri", icon: Handshake, badge: "partneri" },
  { href: "/admin/novosti", label: "Novosti", icon: Mail },
  { href: "/admin/drivers", label: "Vozači", icon: Users },
  { href: "/admin/dostava", label: "Dostava", icon: Truck },
  { href: "/admin/settings", label: "Magacin", icon: Warehouse },
];

function useActive() {
  const pathname = usePathname();
  return (href: string) => pathname === href || pathname.startsWith(href + "/");
}

/**
 * Broj narudžbi u statusu Pending. Dijeli queryKey sa admin dashboardom, pa ih
 * TanStack Query spaja u jedan zahtjev; `refetchInterval` drži badge svježim dok
 * je admin panel otvoren (pauzira se sam kad je tab u pozadini). Promjena statusa
 * narudžbe već invalidira ovaj ključ, pa badge padne odmah, bez čekanja intervala.
 */
function usePendingOrdersCount() {
  const { data } = useQuery({
    queryKey: ["admin", "dashboard"],
    queryFn: () => adminApi.dashboard(),
    refetchInterval: 30_000,
  });
  return data?.pendingOrders ?? 0;
}

/** Broj prijava partnera u statusu Nova; invalidira se prefiksom ["admin","partneri"]. */
function useNewPartnersCount() {
  const { data } = useQuery({
    queryKey: queryKeys.adminPartneri("Nova"),
    queryFn: () => adminApi.partneri("Nova"),
    refetchInterval: 30_000,
  });
  return data?.length ?? 0;
}

function useBadgeCounts(): Record<BadgeVrsta, number> {
  return { narudzbe: usePendingOrdersCount(), partneri: useNewPartnersCount() };
}

function badgeLabel(vrsta: BadgeVrsta, count: number): string {
  return vrsta === "narudzbe"
    ? `${count} ${narudzbeRijec(count)} na čekanju`
    : `${count} ${prijaveRijec(count)}`;
}

function PendingBadge({ count, label, className }: { count: number; label: string; className?: string }) {
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

export function AdminSidebar() {
  const isActive = useActive();
  const { user, logout } = useAuth();
  const counts = useBadgeCounts();

  return (
    <aside className="sticky top-0 hidden h-screen w-60 shrink-0 flex-col bg-ink text-white lg:flex">
      <div className="flex h-16 items-center gap-2 px-5">
        <span className="grid h-8 w-8 place-items-center rounded-full bg-brand-500">
          <Sprout className="h-4 w-4" />
        </span>
        <span className="font-display font-extrabold">E piljara</span>
        <span className="ml-1 rounded bg-white/10 px-1.5 py-0.5 text-[10px] font-semibold">
          admin
        </span>
      </div>

      <nav className="flex-1 space-y-1 px-3 py-4">
        {ITEMS.map((item) => (
          <Link
            key={item.href}
            href={item.href}
            className={cn(
              "flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium transition",
              isActive(item.href)
                ? "bg-brand-600 text-white"
                : "text-white/70 hover:bg-white/10 hover:text-white",
            )}
          >
            <item.icon className="h-4 w-4" />
            {item.label}
            {item.badge && (
              <PendingBadge
                count={counts[item.badge]}
                label={badgeLabel(item.badge, counts[item.badge])}
                className="ml-auto"
              />
            )}
          </Link>
        ))}
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
    <header className="sticky top-0 z-30 border-b border-white/10 bg-ink text-white lg:hidden">
      <div className="flex h-14 items-center justify-between px-4">
        <span className="font-display font-bold">
          E piljara <span className="text-xs font-medium text-white/50">admin</span>
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
              <PendingBadge count={counts[item.badge]} label={badgeLabel(item.badge, counts[item.badge])} />
            )}
          </Link>
        ))}
      </nav>
    </header>
  );
}
