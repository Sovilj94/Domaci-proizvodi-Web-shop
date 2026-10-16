"use client";

import Link from "next/link";
import { useEffect } from "react";
import { usePathname } from "next/navigation";
import { ChevronRight, Languages, LayoutDashboard, X } from "lucide-react";
import { GLAVNA_NAVIGACIJA } from "@/components/layout/navigacija";
import { PismoPrekidac } from "@/components/pismo/pismo-prekidac";
import { buttonVariants } from "@/components/ui/button";
import { useAuth } from "@/lib/auth/auth-context";
import { cn } from "@/lib/utils";

/**
 * Mobilni meni preko cijelog ekrana, po uzoru na farmit (R1): X gore desno, red za pismo
 * (kod farmit-a je to jezik), stavke sa ikonom i strelicom, dugmad naloga na dnu.
 * Zaključava skrol stranice dok je otvoren; zatvara se na promjenu rute i na Escape.
 */
export function MobilniMeni({ otvoren, onZatvori }: { otvoren: boolean; onZatvori: () => void }) {
  const { user, isAdmin, logout } = useAuth();
  const pathname = usePathname();

  useEffect(() => {
    onZatvori();
    // Zatvaranje samo na promjenu rute; onZatvori je nova funkcija pri svakom renderu roditelja.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pathname]);

  useEffect(() => {
    if (!otvoren) return;
    const prije = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    const naTipku = (e: KeyboardEvent) => e.key === "Escape" && onZatvori();
    window.addEventListener("keydown", naTipku);
    return () => {
      document.body.style.overflow = prije;
      window.removeEventListener("keydown", naTipku);
    };
  }, [otvoren, onZatvori]);

  if (!otvoren) return null;

  const stavka = "flex items-center gap-4 border-b border-line/60 py-4 text-lg text-ink";

  return (
    <div role="dialog" aria-modal="true" aria-label="Meni" className="fixed inset-0 z-50 flex flex-col bg-white px-6 pb-6 pt-5 lg:hidden">
      <div className="flex justify-end">
        <button onClick={onZatvori} aria-label="Zatvori meni" className="grid h-10 w-10 place-items-center text-ink">
          <X className="h-7 w-7" />
        </button>
      </div>

      <div className="mt-4 flex items-center justify-between border-b border-line pb-5">
        <span className="flex items-center gap-4 text-lg text-ink">
          <Languages className="h-5 w-5" /> Pismo
        </span>
        <PismoPrekidac className="rounded-full bg-beige px-4 py-2 text-base font-medium text-ink" />
      </div>

      <nav className="mt-2 flex-1 overflow-y-auto">
        {GLAVNA_NAVIGACIJA.map(({ href, naziv, ikona: Ikona }) => (
          <Link key={href} href={href} className={stavka}>
            <Ikona className="h-5 w-5 text-ink" />
            <span className="flex-1">{naziv}</span>
            <ChevronRight className="h-5 w-5 text-muted" />
          </Link>
        ))}
        {isAdmin && (
          <Link href="/admin/dashboard" className={stavka}>
            <LayoutDashboard className="h-5 w-5" />
            <span className="flex-1">Admin panel</span>
            <ChevronRight className="h-5 w-5 text-muted" />
          </Link>
        )}
      </nav>

      <div className="mt-6 flex flex-col gap-3">
        {user ? (
          <>
            <Link href="/moj-nalog" className={cn(buttonVariants({ size: "lg" }), "w-full")}>
              Moj nalog
            </Link>
            <button onClick={logout} className={cn(buttonVariants({ variant: "secondary", size: "lg" }), "w-full")}>
              Odjava
            </button>
          </>
        ) : (
          <>
            <Link href="/login" className={cn(buttonVariants({ size: "lg" }), "w-full")}>
              Prijavi se
            </Link>
            <Link href="/register" className={cn(buttonVariants({ variant: "secondary", size: "lg" }), "w-full")}>
              Registruj se
            </Link>
          </>
        )}
      </div>
    </div>
  );
}
