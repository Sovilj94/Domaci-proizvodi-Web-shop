"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  LayoutDashboard,
  MapPin,
  RefreshCw,
  ShoppingBag,
  ShoppingBasket,
  User,
  type LucideIcon,
} from "lucide-react";
import { cn } from "@/lib/utils";

export type NalogStavka = {
  href: string;
  label: string;
  icon: LucideIcon;
  /** Stiže u kasnijem potprojektu — prikazuje se, ali ne vodi nigdje. */
  uskoro?: boolean;
};

export const NALOG_STAVKE: NalogStavka[] = [
  { href: "/moj-nalog", label: "Pregled", icon: LayoutDashboard },
  { href: "/moj-nalog/podaci", label: "Lični podaci", icon: User },
  { href: "/moj-nalog/adrese", label: "Adrese", icon: MapPin },
  { href: "/moj-nalog/narudzbe", label: "Narudžbe", icon: ShoppingBag },
  { href: "/moj-nalog/korpica", label: "Moja korpica", icon: ShoppingBasket, uskoro: true },
  { href: "/moj-nalog/pretplate", label: "Pretplate", icon: RefreshCw, uskoro: true },
];

/**
 * Admin-ova verzija (`admin-nav.tsx`) smatra aktivnim sve čiji pathname počinje sa
 * `href + "/"`, jer su tamo sve rute sestre — nijedna nije prefiks druge. Ovdje
 * "/moj-nalog" (Pregled) JE prefiks svih ostalih ("/moj-nalog/adrese" itd.), pa bi
 * isto pravilo upalilo Pregled na svakoj podstranici. Zato Pregled traži tačno
 * poklapanje; ostale stavke i dalje prihvataju i svoje podrute.
 */
function useActive() {
  const pathname = usePathname();
  return (href: string) =>
    pathname === href || (href !== "/moj-nalog" && pathname.startsWith(href + "/"));
}

function NalogStavkaLink({
  stavka,
  active,
  className,
}: {
  stavka: NalogStavka;
  active: boolean;
  className?: string;
}) {
  if (stavka.uskoro) {
    return (
      <span
        className={cn(
          "flex items-center gap-3 rounded-[10px] px-3 py-2.5 text-sm font-medium text-muted/60",
          className,
        )}
      >
        <stavka.icon className="h-4 w-4" />
        {stavka.label}
        <span className="ml-auto rounded-full bg-sand px-2 py-0.5 text-[10px] font-semibold text-muted">
          uskoro
        </span>
      </span>
    );
  }

  return (
    <Link
      href={stavka.href}
      className={cn(
        "flex items-center gap-3 rounded-[10px] px-3 py-2.5 text-sm font-medium transition",
        active ? "bg-brand-600 text-white" : "text-ink hover:bg-sand",
        className,
      )}
    >
      <stavka.icon className="h-4 w-4" />
      {stavka.label}
    </Link>
  );
}

export function NalogSidebar() {
  const isActive = useActive();

  return (
    <aside className="hidden w-60 shrink-0 lg:block">
      <nav className="sticky top-24 space-y-1 rounded-card border border-line bg-surface p-3 shadow-card">
        {NALOG_STAVKE.map((stavka) => (
          <NalogStavkaLink
            key={stavka.href}
            stavka={stavka}
            active={isActive(stavka.href)}
          />
        ))}
      </nav>
    </aside>
  );
}

export function NalogMobileBar() {
  const isActive = useActive();

  return (
    <nav className="no-scrollbar mb-4 flex gap-1 overflow-x-auto rounded-card border border-line bg-surface p-2 lg:hidden">
      {NALOG_STAVKE.map((stavka) => (
        <NalogStavkaLink
          key={stavka.href}
          stavka={stavka}
          active={isActive(stavka.href)}
          className="whitespace-nowrap"
        />
      ))}
    </nav>
  );
}
