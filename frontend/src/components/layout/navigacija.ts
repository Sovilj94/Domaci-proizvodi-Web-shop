import { BarChart3, ChefHat, House, Package, ShoppingBasket, type LucideIcon } from "lucide-react";

/** Glavne stavke zaglavlja i mobilnog menija — po uzoru na farmit (Početna · Gajbice · Špajz · Analize · Recepti). */
export const GLAVNA_NAVIGACIJA: { href: string; naziv: string; ikona: LucideIcon }[] = [
  { href: "/", naziv: "Početna", ikona: House },
  { href: "/korpice", naziv: "Korpice", ikona: Package },
  { href: "/spajz", naziv: "Špajz", ikona: ShoppingBasket },
  { href: "/analize", naziv: "Analize", ikona: BarChart3 },
  { href: "/recepti", naziv: "Recepti", ikona: ChefHat },
];

/**
 * Aktivna stavka: tačno `/`, a ostale po prefiksu (stranica korpice je i dalje „Korpice").
 * Stranica proizvoda je `/products/{slug}`, ali pripada Špajzu.
 */
export function jeAktivna(href: string, pathname: string): boolean {
  if (href === "/") return pathname === "/";
  if (href === "/spajz" && pathname.startsWith("/products/")) return true;
  return pathname === href || pathname.startsWith(href + "/");
}
