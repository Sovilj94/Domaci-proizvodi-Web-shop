"use client";

import Link from "next/link";
import { Suspense, useState } from "react";
import { usePathname, useRouter } from "next/navigation";
import { LayoutDashboard, LogOut, Menu, Search, User, X } from "lucide-react";
import { Logo } from "@/components/layout/logo";
import { CategoryRail } from "@/components/layout/category-rail";
import { CartButton } from "@/components/shop/cart-button";
import { useAuth } from "@/lib/auth/auth-context";

function ShopNavbarInner() {
  const [open, setOpen] = useState(false);
  const [q, setQ] = useState("");
  const { user, isAdmin, logout, loading } = useAuth();
  const router = useRouter();
  const pathname = usePathname();
  // Stranica proizvoda ima svoju živu pretragu — ne dupliraj je u headeru.
  const hideSearch = pathname === "/products";

  function search(e: React.FormEvent) {
    e.preventDefault();
    const t = q.trim();
    router.push(t ? `/products?search=${encodeURIComponent(t)}` : "/products");
    setOpen(false);
  }

  const searchInputClass =
    "w-full rounded-[10px] border border-line bg-surface py-2.5 pl-9 pr-4 text-sm transition placeholder:text-muted focus:border-brand-400 focus:outline-none focus:ring-2 focus:ring-brand-600/20";

  return (
    <header className="sticky top-0 z-40 border-b border-line bg-cream/95 backdrop-blur">
      <div className="mx-auto max-w-6xl px-4">
        <div className="flex h-16 items-center gap-3">
          <Logo />

          {/* Pretraga na desktopu */}
          {!hideSearch && (
            <form onSubmit={search} className="relative mx-2 hidden max-w-md flex-1 md:block">
              <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted" />
              <input
                value={q}
                onChange={(e) => setQ(e.target.value)}
                placeholder="Pretraži proizvode…"
                className={searchInputClass}
              />
            </form>
          )}

          <div className="flex flex-1 items-center justify-end gap-1 md:flex-none">
            {isAdmin && (
              <Link
                href="/admin/dashboard"
                className="mr-1 hidden items-center gap-1.5 rounded-[10px] bg-ink px-3 py-1.5 text-xs font-medium text-cream transition hover:bg-ink/85 sm:inline-flex"
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
                    className="flex items-center gap-2 rounded-[10px] px-3 py-2 text-sm font-medium text-ink transition hover:bg-sand"
                  >
                    <User className="h-4 w-4" />
                    {user.name.split(" ")[0]}
                  </Link>
                  <button
                    onClick={logout}
                    aria-label="Odjava"
                    className="grid h-10 w-10 place-items-center rounded-[10px] text-muted transition hover:bg-sand hover:text-ink"
                  >
                    <LogOut className="h-4 w-4" />
                  </button>
                </div>
              ) : (
                <Link
                  href="/login"
                  className="hidden rounded-[10px] bg-brand-600 px-5 py-2 text-sm font-medium text-white transition hover:bg-brand-700 sm:inline-flex"
                >
                  Prijava
                </Link>
              ))}

            <button
              onClick={() => setOpen((v) => !v)}
              aria-label="Meni"
              className="grid h-10 w-10 place-items-center rounded-[10px] text-ink transition hover:bg-sand md:hidden"
            >
              {open ? <X className="h-5 w-5" /> : <Menu className="h-5 w-5" />}
            </button>
          </div>
        </div>

        {/* Pretraga na telefonu */}
        {!hideSearch && (
          <form onSubmit={search} className="relative pb-3 md:hidden">
            <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted" />
            <input
              value={q}
              onChange={(e) => setQ(e.target.value)}
              placeholder="Pretraži proizvode…"
              className={searchInputClass}
            />
          </form>
        )}
      </div>

      {/* Traka kategorija — puna širina, sama se skroluje */}
      <div className="border-t border-line">
        <CategoryRail />
      </div>

      {/*
        Meni naloga na telefonu. Puni drawer sa svim stavkama (Kategorije, O nama,
        Kontakt…) stiže u Fazi 4; dotle ovdje stoji samo ono što već postoji.
      */}
      {open && (
        <div className="border-t border-line bg-cream md:hidden">
          <nav className="mx-auto flex max-w-6xl flex-col gap-1 px-4 py-3">
            {user ? (
              <>
                {isAdmin && (
                  <Link
                    href="/admin/dashboard"
                    onClick={() => setOpen(false)}
                    className="rounded-[10px] px-3 py-2.5 text-sm font-medium text-ink hover:bg-sand"
                  >
                    Admin panel
                  </Link>
                )}
                <Link
                  href="/orders"
                  onClick={() => setOpen(false)}
                  className="rounded-[10px] px-3 py-2.5 text-sm font-medium text-ink hover:bg-sand"
                >
                  Moje narudžbe
                </Link>
                <button
                  onClick={() => {
                    logout();
                    setOpen(false);
                  }}
                  className="rounded-[10px] px-3 py-2.5 text-left text-sm font-medium text-red-700 hover:bg-sand"
                >
                  Odjava
                </button>
              </>
            ) : (
              <Link
                href="/login"
                onClick={() => setOpen(false)}
                className="rounded-[10px] bg-brand-600 px-3 py-2.5 text-center text-sm font-medium text-white"
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

/*
 * CategoryRail čita `useSearchParams()`. Bez <Suspense> oko njega `npm run build` pada
 * u prerender prolazu, dok tsc i eslint prolaze čisto — u ovom repozitoriju je to već
 * oborilo /login, /register i /admin/dostava.
 *
 * Visina fallbacka drži 64px gornjeg reda + ~42px trake; bez nje stranica skoči pri
 * hidraciji.
 */
export function ShopNavbar() {
  return (
    <Suspense
      fallback={<div className="sticky top-0 z-40 h-[106px] border-b border-line bg-cream" />}
    >
      <ShopNavbarInner />
    </Suspense>
  );
}
