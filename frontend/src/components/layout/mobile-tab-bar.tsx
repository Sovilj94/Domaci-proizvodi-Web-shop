"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { Grid2x2, Home, LayoutGrid, ShoppingCart, User, type LucideIcon } from "lucide-react";
import { useCart } from "@/lib/cart/cart-context";
import { useAuth } from "@/lib/auth/auth-context";
import { cn } from "@/lib/utils";

/**
 * Donja traka u stilu aplikacije (Glovo/Wolt), samo na telefonima — desktop zadržava
 * gornji navbar. Tab korpe nosi živi broj artikala i otvara korpu umjesto da navigira.
 */
interface Tab {
  href: string;
  label: string;
  icon: LucideIcon;
  exact?: boolean;
  cart?: boolean;
}

const TABS: Tab[] = [
  { href: "/", label: "Početna", icon: Home, exact: true },
  { href: "/products", label: "Proizvodi", icon: LayoutGrid },
  { href: "/kategorije", label: "Kategorije", icon: Grid2x2 },
  { href: "/cart", label: "Korpa", icon: ShoppingCart, cart: true },
  { href: "/account", label: "Profil", icon: User },
];

export function MobileTabBar() {
  const pathname = usePathname();
  const { count, isOpen, openCart } = useCart();
  const { user } = useAuth();

  return (
    <nav className="fixed inset-x-0 bottom-0 z-40 border-t border-line bg-surface/95 pb-[env(safe-area-inset-bottom)] backdrop-blur md:hidden">
      <ul className="mx-auto flex max-w-md items-stretch">
        {TABS.map((t) => {
          /* /account stiže u Fazi 4; do tada Profil vodi na prijavu ili narudžbe. */
          const href =
            t.href === "/account" ? (user ? "/orders" : "/login") : t.href;
          /*
           * Aktivno stanje se računa iz `href`, ne iz `t.href`: Profil vodi na
           * izvedeno odredište, pa bi poređenje sa "/account" — rute koja još ne
           * postoji — značilo da se taj tab nikad ne istakne.
           */
          const active = t.cart
            ? isOpen
            : t.exact
              ? pathname === href
              : pathname === href || pathname.startsWith(href + "/");
          const cls = cn(
            "flex w-full flex-col items-center gap-0.5 py-2 text-[11px] font-medium transition",
            active ? "text-brand-600" : "text-muted",
          );
          const inner = (
            <>
              <span className="relative">
                <t.icon className={cn("h-5 w-5", active && "stroke-[2.5]")} />
                {t.cart && count > 0 && (
                  <span
                    id="cart-target-mobile"
                    className="absolute -right-2 -top-1.5 grid h-4 min-w-4 place-items-center rounded-full bg-brand-600 px-1 text-[10px] font-bold text-white"
                  >
                    {count}
                  </span>
                )}
              </span>
              {t.label}
            </>
          );
          return (
            <li key={t.href} className="flex-1">
              {t.cart ? (
                <button type="button" onClick={openCart} className={cls}>
                  {inner}
                </button>
              ) : (
                <Link href={href} className={cls}>
                  {inner}
                </Link>
              )}
            </li>
          );
        })}
      </ul>
    </nav>
  );
}
