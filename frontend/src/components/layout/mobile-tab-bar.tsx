"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { ClipboardList, Home, LayoutGrid, ShoppingCart, type LucideIcon } from "lucide-react";
import { useCart } from "@/lib/cart/cart-context";
import { cn } from "@/lib/utils";

/**
 * App-style bottom tab bar (Glovo/Wolt) shown only on phones. Desktop keeps the
 * top navbar. The cart tab carries a live item-count badge.
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
  { href: "/cart", label: "Korpa", icon: ShoppingCart, cart: true },
  { href: "/orders", label: "Narudžbe", icon: ClipboardList },
];

export function MobileTabBar() {
  const pathname = usePathname();
  const { count, isOpen, openCart } = useCart();

  return (
    <nav className="fixed inset-x-0 bottom-0 z-40 border-t border-black/5 bg-cream/95 backdrop-blur md:hidden">
      <ul className="mx-auto flex max-w-md items-stretch">
        {TABS.map((t) => {
          const active = t.cart
            ? isOpen
            : t.exact
              ? pathname === t.href
              : pathname === t.href || pathname.startsWith(t.href + "/");
          const cls = cn(
            "flex w-full flex-col items-center gap-0.5 py-2 text-[11px] font-medium transition",
            active ? "text-brand-700" : "text-muted",
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
                <Link href={t.href} className={cls}>
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
