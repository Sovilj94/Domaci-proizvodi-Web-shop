"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  LayoutDashboard,
  LogOut,
  Package,
  ShoppingBag,
  Sprout,
  Store,
  Tags,
  Tractor,
  Truck,
} from "lucide-react";
import { useAuth } from "@/lib/auth/auth-context";
import { cn } from "@/lib/utils";

const ITEMS = [
  { href: "/admin/dashboard", label: "Pregled", icon: LayoutDashboard },
  { href: "/admin/orders", label: "Narudžbe", icon: ShoppingBag },
  { href: "/admin/products", label: "Proizvodi", icon: Package },
  { href: "/admin/categories", label: "Kategorije", icon: Tags },
  { href: "/admin/producers", label: "Proizvođači", icon: Tractor },
  { href: "/admin/routes", label: "Rute dostave", icon: Truck },
];

function useActive() {
  const pathname = usePathname();
  return (href: string) => pathname === href || pathname.startsWith(href + "/");
}

export function AdminSidebar() {
  const isActive = useActive();
  const { user, logout } = useAuth();

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
          </Link>
        ))}
      </nav>

      <div className="border-t border-white/10 p-3">
        <Link
          href="/"
          className="flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium text-white/70 transition hover:bg-white/10 hover:text-white"
        >
          <Store className="h-4 w-4" /> Web shop
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

  return (
    <header className="sticky top-0 z-30 border-b border-white/10 bg-ink text-white lg:hidden">
      <div className="flex h-14 items-center justify-between px-4">
        <span className="font-display font-extrabold">
          E piljara <span className="text-xs font-medium text-white/50">admin</span>
        </span>
        <button onClick={logout} aria-label="Odjava" className="text-white/70">
          <LogOut className="h-4 w-4" />
        </button>
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
          </Link>
        ))}
      </nav>
    </header>
  );
}
