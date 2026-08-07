"use client";

import Link from "next/link";
import { useState } from "react";
import { usePathname, useRouter } from "next/navigation";
import { LayoutDashboard, LogOut, MapPin, Menu, Search, User, X } from "lucide-react";
import { Logo } from "@/components/layout/logo";
import { CartButton } from "@/components/shop/cart-button";
import { useAuth } from "@/lib/auth/auth-context";

const searchInputClass =
  "w-full rounded-full border border-line bg-sand py-2.5 pl-9 pr-4 text-sm transition focus:border-brand-400 focus:bg-white focus:outline-none focus:ring-2 focus:ring-brand-400";

export function ShopNavbar() {
  const [open, setOpen] = useState(false);
  const [q, setQ] = useState("");
  const { user, isAdmin, logout, loading } = useAuth();
  const router = useRouter();
  const pathname = usePathname();
  // The products page has its own live search — don't duplicate it in the navbar there.
  const hideSearch = pathname === "/products";

  function search(e: React.FormEvent) {
    e.preventDefault();
    const t = q.trim();
    router.push(t ? `/products?search=${encodeURIComponent(t)}` : "/products");
    setOpen(false);
  }

  const locationChip = (
    <span className="flex shrink-0 items-center gap-1.5 rounded-full border border-line bg-white px-3 py-1.5 text-sm">
      <MapPin className="h-4 w-4 text-brand-600" />
      <span className="font-medium">Banja Luka</span>
    </span>
  );

  return (
    <header className="sticky top-0 z-40 border-b border-line bg-cream/85 backdrop-blur">
      <div className="mx-auto max-w-6xl px-4">
        {/* Top row */}
        <div className="flex h-16 items-center gap-3">
          <Logo />
          <div className="hidden sm:block">{locationChip}</div>

          {/* Desktop search */}
          {!hideSearch && (
            <form onSubmit={search} className="relative mx-2 hidden max-w-md flex-1 md:block">
              <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted" />
              <input
                value={q}
                onChange={(e) => setQ(e.target.value)}
                placeholder="Traži proizvode…"
                className={searchInputClass}
              />
            </form>
          )}

          <div className="flex flex-1 items-center justify-end gap-1 md:flex-none">
            {isAdmin && (
              <Link
                href="/admin/dashboard"
                className="mr-1 hidden items-center gap-1.5 rounded-lg bg-ink px-3 py-1.5 text-xs font-semibold text-white sm:inline-flex"
              >
                <LayoutDashboard className="h-3.5 w-3.5" />
                Admin
              </Link>
            )}
            <CartButton />

            {!loading &&
              (user ? (
                <div className="hidden items-center gap-1 sm:flex">
                  <Link
                    href="/orders"
                    className="flex items-center gap-2 rounded-lg px-3 py-2 text-sm font-medium text-ink transition hover:bg-sand"
                  >
                    <User className="h-4 w-4" />
                    {user.name.split(" ")[0]}
                  </Link>
                  <button
                    onClick={logout}
                    aria-label="Odjava"
                    className="grid h-10 w-10 place-items-center rounded-full text-muted transition hover:bg-sand hover:text-ink"
                  >
                    <LogOut className="h-4 w-4" />
                  </button>
                </div>
              ) : (
                <Link
                  href="/login"
                  className="hidden rounded-lg bg-brand-600 px-4 py-2 text-sm font-semibold text-white transition hover:bg-brand-700 sm:inline-flex"
                >
                  Prijava
                </Link>
              ))}

            <button
              onClick={() => setOpen((v) => !v)}
              aria-label="Meni"
              className="grid h-10 w-10 place-items-center rounded-full text-ink transition hover:bg-sand md:hidden"
            >
              {open ? <X className="h-5 w-5" /> : <Menu className="h-5 w-5" />}
            </button>
          </div>
        </div>

        {/* Mobile search row */}
        {!hideSearch && (
          <form onSubmit={search} className="relative flex items-center gap-2 pb-3 md:hidden">
            <div className="sm:hidden">{locationChip}</div>
            <div className="relative flex-1">
              <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted" />
              <input
                value={q}
                onChange={(e) => setQ(e.target.value)}
                placeholder="Traži proizvode…"
                className={searchInputClass}
              />
            </div>
          </form>
        )}
      </div>

      {/* Mobile account menu (main nav lives in the bottom bar) */}
      {open && (
        <div className="border-t border-line bg-cream md:hidden">
          <nav className="mx-auto flex max-w-6xl flex-col gap-1 px-4 py-3">
            {user ? (
              <>
                {isAdmin && (
                  <Link
                    href="/admin/dashboard"
                    onClick={() => setOpen(false)}
                    className="rounded-lg px-3 py-2.5 text-sm font-medium text-ink hover:bg-sand"
                  >
                    Admin panel
                  </Link>
                )}
                <Link
                  href="/orders"
                  onClick={() => setOpen(false)}
                  className="rounded-lg px-3 py-2.5 text-sm font-medium text-ink hover:bg-sand"
                >
                  Moje narudžbe
                </Link>
                <button
                  onClick={() => {
                    logout();
                    setOpen(false);
                  }}
                  className="rounded-lg px-3 py-2.5 text-left text-sm font-medium text-red-600 hover:bg-sand"
                >
                  Odjava
                </button>
              </>
            ) : (
              <Link
                href="/login"
                onClick={() => setOpen(false)}
                className="rounded-lg bg-brand-600 px-3 py-2.5 text-center text-sm font-semibold text-white"
              >
                Prijava
              </Link>
            )}
          </nav>
        </div>
      )}
    </header>
  );
}
