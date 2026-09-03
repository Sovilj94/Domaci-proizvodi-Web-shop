"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { usePathname, useRouter } from "next/navigation";
import { LayoutDashboard, LogOut, MapPin, Menu, Search, User, X } from "lucide-react";
import { Logo } from "@/components/layout/logo";
import { CartButton } from "@/components/shop/cart-button";
import { useAuth } from "@/lib/auth/auth-context";
import { cn } from "@/lib/utils";

export function ShopNavbar() {
  const [open, setOpen] = useState(false);
  const [q, setQ] = useState("");
  const [skrolovano, setSkrolovano] = useState(false);
  const { user, isAdmin, logout, loading } = useAuth();
  const router = useRouter();
  const pathname = usePathname();
  // The products page has its own live search — don't duplicate it in the navbar there.
  const hideSearch = pathname === "/products";

  /*
   * Samo naslovna ima fotografiju pod navbarom, pa samo tamo navbar pluta providan.
   * Na svim ostalim stranicama je krem od prvog piksela — providan navbar nad krem
   * sadržajem bi bio nevidljiv okvir koji ništa ne radi.
   */
  const nadHerojem = pathname === "/" && !skrolovano && !open;

  useEffect(() => {
    if (pathname !== "/") return;
    const naSkrol = () => setSkrolovano(window.scrollY > 24);
    naSkrol();
    window.addEventListener("scroll", naSkrol, { passive: true });
    return () => window.removeEventListener("scroll", naSkrol);
  }, [pathname]);

  function search(e: React.FormEvent) {
    e.preventDefault();
    const t = q.trim();
    router.push(t ? `/products?search=${encodeURIComponent(t)}` : "/products");
    setOpen(false);
  }

  const searchInputClass = cn(
    "w-full rounded-full border py-2.5 pl-9 pr-4 text-sm transition focus:outline-none focus:ring-2 focus:ring-brand-500",
    nadHerojem
      ? "border-white/25 bg-white/10 text-cream placeholder:text-cream/60 backdrop-blur focus:bg-white/20"
      : "border-line bg-sand placeholder:text-muted focus:border-brand-400 focus:bg-cream",
  );

  const locationChip = (
    <span
      className={cn(
        "flex shrink-0 items-center gap-1.5 rounded-full border px-3 py-1.5 text-sm",
        nadHerojem ? "border-white/25 bg-white/10 text-cream backdrop-blur" : "border-line bg-cream",
      )}
    >
      <MapPin className={cn("h-4 w-4", nadHerojem ? "text-honey-500" : "text-brand-600")} />
      <span className="font-medium">Banja Luka</span>
    </span>
  );

  return (
    <header
      className={cn(
        "z-40 transition-colors duration-300",
        /*
          Na naslovnoj je `fixed`, ne `sticky`: sticky navbar zauzima mjesto u toku pa
          bi hero počeo ispod njega i providnost ne bi imala šta da pokaže. Fixed ostaje
          i nakon skrola — tada je krem i sadržaj mu prolazi ispod, kao na predlošku.
          Prebacivanje fixed↔sticky na skrol bi napravilo skok od 64px, pa se ne dira.
        */
        pathname === "/" ? "fixed inset-x-0 top-0" : "sticky top-0",
        nadHerojem
          ? "border-b border-transparent"
          : "border-b border-line bg-cream/90 backdrop-blur",
      )}
    >
      {/*
        Providan navbar stoji nad fotografijom čija desna strana je svijetla — korpa i
        ikone naloga tamo padnu ispod čitljivosti. Ova prekrivka ide samo iza navbara,
        gasi se prije nego što stigne do naslova i ne dira utisak providnosti.
      */}
      {nadHerojem && (
        <div
          aria-hidden
          className="pointer-events-none absolute inset-x-0 top-0 h-28"
          style={{
            background: "linear-gradient(180deg, rgba(20,18,12,0.6) 0%, rgba(20,18,12,0) 100%)",
          }}
        />
      )}
      <div className="relative mx-auto max-w-6xl px-4">
        {/* Top row */}
        <div className="flex h-16 items-center gap-3">
          <Logo tone={nadHerojem ? "dark" : "light"} />
          <div className="hidden sm:block">{locationChip}</div>

          {/* Desktop search */}
          {!hideSearch && (
            <form onSubmit={search} className="relative mx-2 hidden max-w-md flex-1 md:block">
              <Search
                className={cn(
                  "pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2",
                  nadHerojem ? "text-cream/70" : "text-muted",
                )}
              />
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
                className={cn(
                  "mr-1 hidden items-center gap-1.5 rounded-full px-3 py-1.5 text-xs font-medium transition sm:inline-flex",
                  nadHerojem
                    ? "border border-white/25 text-cream hover:bg-white/10"
                    : "bg-ink text-cream hover:bg-ink/85",
                )}
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
                    className={cn(
                      "flex items-center gap-2 rounded-full px-3 py-2 text-sm font-medium transition",
                      nadHerojem ? "text-cream hover:bg-white/10" : "text-ink hover:bg-sand",
                    )}
                  >
                    <User className="h-4 w-4" />
                    {user.name.split(" ")[0]}
                  </Link>
                  <button
                    onClick={logout}
                    aria-label="Odjava"
                    className={cn(
                      "grid h-10 w-10 place-items-center rounded-full transition",
                      nadHerojem
                        ? "text-cream/70 hover:bg-white/10 hover:text-cream"
                        : "text-muted hover:bg-sand hover:text-ink",
                    )}
                  >
                    <LogOut className="h-4 w-4" />
                  </button>
                </div>
              ) : (
                <Link
                  href="/login"
                  className="hidden rounded-full bg-brand-600 px-5 py-2 text-sm font-medium text-white transition hover:bg-brand-700 sm:inline-flex"
                >
                  Prijava
                </Link>
              ))}

            <button
              onClick={() => setOpen((v) => !v)}
              aria-label="Meni"
              className={cn(
                "grid h-10 w-10 place-items-center rounded-full transition md:hidden",
                nadHerojem ? "text-cream hover:bg-white/10" : "text-ink hover:bg-sand",
              )}
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
              <Search
                className={cn(
                  "pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2",
                  nadHerojem ? "text-cream/70" : "text-muted",
                )}
              />
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
                  className="rounded-lg px-3 py-2.5 text-left text-sm font-medium text-red-700 hover:bg-sand"
                >
                  Odjava
                </button>
              </>
            ) : (
              <Link
                href="/login"
                onClick={() => setOpen(false)}
                className="rounded-full bg-brand-600 px-3 py-2.5 text-center text-sm font-medium text-white"
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
