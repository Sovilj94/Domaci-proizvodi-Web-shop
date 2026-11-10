"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { usePathname } from "next/navigation";
import { LayoutDashboard, LogOut, Menu, User } from "lucide-react";
import { Logo } from "@/components/layout/logo";
import { MobilniMeni } from "@/components/layout/mobilni-meni";
import { GLAVNA_NAVIGACIJA, jeAktivna } from "@/components/layout/navigacija";
import { CartButton } from "@/components/shop/cart-button";
import { JezikPrekidac } from "@/components/jezik/jezik-prekidac";
import { buttonVariants } from "@/components/ui/button";
import { useAuth } from "@/lib/auth/auth-context";
import { cn } from "@/lib/utils";

/**
 * Zaglavlje po uzoru na farmit (R1). Na početnoj leži preko hero fotografije, prozirno sa
 * bijelim tekstom, i postaje bijelo čim se stranica pomjeri — inače bi bijeli tekst nestao
 * preko bijelih sekcija. Na ostalim stranicama je uvijek bijelo sa zelenim linkovima.
 * Aktivna stavka ima narandžastu podvlaku.
 */
export function ShopNavbar() {
  const [meniOtvoren, setMeniOtvoren] = useState(false);
  const [skrolovano, setSkrolovano] = useState(false);
  const { user, isAdmin, logout, loading } = useAuth();
  const pathname = usePathname();
  const naPocetnoj = pathname === "/";

  // Na telefonu (ispod lg) se zaglavlje sklanja dok se skroluje nadolje i vraća čim se krene
  // nagore — kao na uzoru, da sadržaj dobije cijeli ekran.
  const [skriveno, setSkriveno] = useState(false);

  useEffect(() => {
    let prethodni = window.scrollY;
    const telefon = window.matchMedia("(max-width: 1023.98px)");
    const azuriraj = () => {
      const y = window.scrollY;
      setSkrolovano(y > 40);
      if (!telefon.matches || y < 120) setSkriveno(false);
      else if (Math.abs(y - prethodni) > 6) setSkriveno(y > prethodni);
      prethodni = y;
    };
    azuriraj();
    window.addEventListener("scroll", azuriraj, { passive: true });
    return () => window.removeEventListener("scroll", azuriraj);
  }, []);

  // Nova stranica počinje sa vidljivim zaglavljem.
  useEffect(() => setSkriveno(false), [pathname]);

  const sakriveno = skriveno && !meniOtvoren;

  // Ljepljivi elementi stranica (LjepljiveKartice) čitaju ovo da bi stali ispod zaglavlja dok je vidljivo.
  useEffect(() => {
    document.documentElement.dataset.zaglavlje = sakriveno ? "skriveno" : "vidljivo";
  }, [sakriveno]);

  const proziran = naPocetnoj && !skrolovano && !meniOtvoren;

  return (
    <>
      <header
        className={cn(
          "inset-x-0 top-0 z-40 transition-[background-color,box-shadow,transform] duration-200",
          naPocetnoj ? "fixed" : "sticky",
          proziran ? "bg-transparent" : "bg-white shadow-[0_2px_12px_rgba(64,74,61,0.08)]",
          sakriveno && "-translate-y-full shadow-none",
        )}
      >
        <div className="mx-auto flex h-[72px] max-w-7xl items-center gap-1.5 px-3 sm:gap-3 sm:px-4 lg:h-24 lg:px-8">
          <button
            onClick={() => setMeniOtvoren(true)}
            aria-label="Otvori meni"
            className={cn(
              "grid h-10 w-10 shrink-0 place-items-center rounded-full lg:hidden",
              proziran ? "text-white" : "text-brand-600",
            )}
          >
            <Menu className="h-7 w-7" />
          </button>

          <Logo tone={proziran ? "dark" : "light"} />

          <nav className="ml-auto hidden items-center gap-6 lg:flex">
            {GLAVNA_NAVIGACIJA.map((s) => {
              const aktivna = jeAktivna(s.href, pathname);
              return (
                <Link
                  key={s.href}
                  href={s.href}
                  className={cn(
                    "border-b-2 px-2 pb-1 pt-1.5 text-lg font-semibold transition",
                    proziran ? "text-white hover:text-white/80" : "text-brand-600 hover:text-brand-700",
                    aktivna ? "border-honey-500" : "border-transparent",
                  )}
                >
                  {s.naziv}
                </Link>
              );
            })}
          </nav>

          <div className="ml-auto flex shrink-0 items-center gap-1 sm:gap-2 lg:ml-6">
            {isAdmin && (
              <Link
                href="/admin/dashboard"
                aria-label="Admin panel"
                className={cn(
                  "hidden h-10 w-10 place-items-center rounded-full transition hover:bg-black/5 lg:grid",
                  proziran ? "text-white" : "text-ink",
                )}
              >
                <LayoutDashboard className="h-5 w-5" />
              </Link>
            )}

            {!loading && user && (
                <div className="hidden items-center gap-1 lg:flex">
                  <Link
                    href="/moj-nalog"
                    className={cn(
                      "flex items-center gap-2 rounded-full px-3 py-2 text-base font-semibold transition hover:bg-black/5",
                      proziran ? "text-white" : "text-brand-600",
                    )}
                  >
                    <User className="h-5 w-5" />
                    {user.name.split(" ")[0]}
                  </Link>
                  <button
                    onClick={logout}
                    aria-label="Odjava"
                    className={cn(
                      "grid h-10 w-10 place-items-center rounded-full transition hover:bg-black/5",
                      proziran ? "text-white" : "text-muted",
                    )}
                  >
                    <LogOut className="h-5 w-5" />
                  </button>
                </div>
            )}

            {!loading && user && (
              <Link
                href="/moj-nalog"
                aria-label="Moj nalog"
                className={cn(
                  "grid h-10 w-10 place-items-center rounded-full lg:hidden",
                  proziran ? "text-white" : "text-brand-600",
                )}
              >
                <User className="h-6 w-6" />
              </Link>
            )}

            {!loading && !user && (
                <>
                  <Link href="/login" className={cn(buttonVariants({ size: "lg" }), "hidden lg:inline-flex")}>
                    Prijavi se
                  </Link>
                  <Link
                    href="/register"
                    className={cn(buttonVariants({ variant: "secondary", size: "lg" }), "hidden lg:inline-flex")}
                  >
                    Registruj se
                  </Link>
                  <Link
                    href="/login"
                    className="inline-flex h-9 items-center whitespace-nowrap rounded-full border border-honey-500 bg-white px-3.5 text-sm font-semibold text-honey-500 sm:h-10 sm:px-4 sm:text-base lg:hidden"
                  >
                    Prijavi se
                  </Link>
                </>
            )}

            <JezikPrekidac
              className={cn(
                "hidden rounded-full px-3 py-2 text-sm font-medium hover:bg-black/5 lg:inline-flex",
                proziran ? "text-white" : "text-ink",
              )}
            />
            <CartButton className={proziran ? "text-white" : "text-brand-600"} />
          </div>
        </div>
      </header>

      <MobilniMeni otvoren={meniOtvoren} onZatvori={() => setMeniOtvoren(false)} />
    </>
  );
}
