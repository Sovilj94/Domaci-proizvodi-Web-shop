"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import {
  CornerDownLeft,
  Package,
  PackageCheck,
  Plus,
  Search,
  ShoppingBag,
  Sparkles,
  UserRound,
  type LucideIcon,
} from "lucide-react";
import { ITEMS } from "@/components/admin/admin-nav";
import { adminApi } from "@/lib/api/endpoints";
import { OTVORI_PALETU, rangiraj } from "@/lib/admin/paleta";
import { STATUS_META } from "@/lib/orders";
import { cn, formatPrice } from "@/lib/utils";

interface Stavka {
  kljuc: string;
  grupa: string;
  naslov: string;
  opis?: string;
  ikona: LucideIcon;
  href: string;
}

const AKCIJE: Stavka[] = [
  { kljuc: "a-narudzba", grupa: "Akcije", naslov: "Nova narudžba", opis: "telefonom ili uživo", ikona: Plus, href: "/admin/orders/new" },
  { kljuc: "a-proizvod", grupa: "Akcije", naslov: "Novi proizvod", ikona: Plus, href: "/admin/products?novi=1" },
  { kljuc: "a-korpice", grupa: "Akcije", naslov: "Generiši sadržaj korpica", opis: "za sedmicu koja se prodaje", ikona: Sparkles, href: "/admin/korpice" },
  { kljuc: "a-pakovanje", grupa: "Akcije", naslov: "Pakerska lista", ikona: PackageCheck, href: "/admin/pakovanje" },
  { kljuc: "a-zaliha", grupa: "Akcije", naslov: "Artikli bez zalihe", ikona: Package, href: "/admin/products?filter=bez-zalihe" },
];

const STRANICE: Stavka[] = ITEMS.map((i) => ({
  kljuc: `s-${i.href}`,
  grupa: "Stranice",
  naslov: i.label,
  ikona: i.icon,
  href: i.href,
}));

/**
 * Ctrl+K (ili ⌘K, ili „/" van polja za unos): jedno polje za skok na stranicu, akciju,
 * proizvod, narudžbu ili kupca. Strelice biraju, Enter otvara, Esc zatvara.
 */
export function KomandnaPaleta() {
  const router = useRouter();
  const [otvorena, setOtvorena] = useState(false);
  const [upit, setUpit] = useState("");
  const [trazi, setTrazi] = useState("");
  const [izbor, setIzbor] = useState(0);
  const polje = useRef<HTMLInputElement>(null);
  const lista = useRef<HTMLUListElement>(null);

  useEffect(() => {
    function naTaster(e: KeyboardEvent) {
      const meta = e.ctrlKey || e.metaKey;
      if (meta && e.key.toLowerCase() === "k") {
        e.preventDefault();
        setOtvorena((o) => !o);
        return;
      }
      const cilj = e.target as HTMLElement | null;
      const kuca = cilj && (cilj.isContentEditable || ["INPUT", "TEXTAREA", "SELECT"].includes(cilj.tagName));
      if (e.key === "/" && !kuca && !meta) {
        e.preventDefault();
        setOtvorena(true);
      }
    }
    const otvori = () => setOtvorena(true);
    window.addEventListener("keydown", naTaster);
    window.addEventListener(OTVORI_PALETU, otvori);
    return () => {
      window.removeEventListener("keydown", naTaster);
      window.removeEventListener(OTVORI_PALETU, otvori);
    };
  }, []);

  useEffect(() => {
    if (!otvorena) return;
    setUpit("");
    setTrazi("");
    setIzbor(0);
    requestAnimationFrame(() => polje.current?.focus());
  }, [otvorena]);

  // Server se pita tek poslije kratke pauze u kucanju.
  useEffect(() => {
    const t = setTimeout(() => setTrazi(upit.trim()), 200);
    return () => clearTimeout(t);
  }, [upit]);

  // Proizvodi su ionako u kešu admina; učitavaju se tek kad se paleta otvori.
  const proizvodi = useQuery({
    queryKey: ["admin", "products"],
    queryFn: () => adminApi.products(),
    enabled: otvorena,
    staleTime: 60_000,
  });
  const serverski = otvorena && trazi.length >= 2;
  const narudzbe = useQuery({
    queryKey: ["admin", "orders", { status: null, pretraga: trazi }],
    queryFn: () => adminApi.orders({ pretraga: trazi }),
    enabled: serverski,
    staleTime: 15_000,
  });
  const kupci = useQuery({
    queryKey: ["admin", "kupci", trazi],
    queryFn: () => adminApi.kupci(trazi),
    enabled: serverski,
    staleTime: 15_000,
  });

  const stavke = useMemo<Stavka[]>(() => {
    const q = upit.trim();
    if (!q) return [...AKCIJE, ...STRANICE.slice(0, 6)];
    const p = rangiraj(proizvodi.data ?? [], q, (x) => `${x.name} ${x.producer?.name ?? ""} ${x.categoryName}`, 5).map(
      (x): Stavka => ({
        kljuc: `p-${x.id}`,
        grupa: "Proizvodi",
        naslov: x.name,
        opis: `${formatPrice(x.price)} · zaliha ${x.stock.toLocaleString("sr-Latn-BA")} ${x.unit}${x.isAvailable ? "" : " · skriven"}`,
        ikona: Package,
        href: `/admin/products?q=${encodeURIComponent(x.name)}`,
      }),
    );
    const n = (serverski && trazi === q ? (narudzbe.data ?? []) : []).slice(0, 5).map(
      (o): Stavka => ({
        kljuc: `n-${o.id}`,
        grupa: "Narudžbe",
        naslov: `${o.code} · ${o.customerName}`,
        opis: `${STATUS_META[o.status].label} · ${formatPrice(o.totalAmount)}`,
        ikona: ShoppingBag,
        href: `/admin/orders?prikaz=lista&pretraga=${encodeURIComponent(o.code)}`,
      }),
    );
    const k = (serverski && trazi === q ? (kupci.data ?? []) : []).slice(0, 4).map(
      (x): Stavka => ({
        kljuc: `k-${x.id}`,
        grupa: "Kupci",
        naslov: x.ime,
        opis: `${x.email} · ${x.brojNarudzbi} narudžbi`,
        ikona: UserRound,
        href: `/admin/kupci?pretraga=${encodeURIComponent(x.email)}&kupac=${x.id}`,
      }),
    );
    return [
      ...rangiraj(AKCIJE, q, (a) => `${a.naslov} ${a.opis ?? ""}`, 3),
      ...rangiraj(STRANICE, q, (s) => s.naslov, 4),
      ...p,
      ...n,
      ...k,
    ];
  }, [upit, trazi, serverski, proizvodi.data, narudzbe.data, kupci.data]);

  useEffect(() => setIzbor(0), [upit]);
  useEffect(() => {
    lista.current?.querySelector(`[data-idx="${izbor}"]`)?.scrollIntoView({ block: "nearest" });
  }, [izbor]);

  function otvori(s: Stavka | undefined) {
    if (!s) return;
    setOtvorena(false);
    router.push(s.href);
  }

  if (!otvorena) return null;

  const ucitava = serverski && (narudzbe.isFetching || kupci.isFetching || trazi !== upit.trim());
  let prethodnaGrupa = "";

  return (
    <div
      className="fixed inset-0 z-[60] flex items-start justify-center bg-ink/40 px-4 pt-[12vh] backdrop-blur-sm"
      onMouseDown={(e) => e.target === e.currentTarget && setOtvorena(false)}
    >
      <div role="dialog" aria-modal="true" aria-label="Pretraga admina" className="w-full max-w-xl overflow-hidden rounded-card bg-white shadow-2xl">
        <div className="flex items-center gap-3 border-b border-line px-4">
          <Search className="h-5 w-5 shrink-0 text-muted" />
          <input
            ref={polje}
            value={upit}
            onChange={(e) => setUpit(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "ArrowDown") {
                e.preventDefault();
                setIzbor((i) => Math.min(i + 1, stavke.length - 1));
              } else if (e.key === "ArrowUp") {
                e.preventDefault();
                setIzbor((i) => Math.max(i - 1, 0));
              } else if (e.key === "Enter") {
                e.preventDefault();
                otvori(stavke[izbor]);
              } else if (e.key === "Escape") {
                setOtvorena(false);
              }
            }}
            placeholder="Stranica, proizvod, šifra narudžbe, kupac…"
            aria-label="Traži"
            aria-activedescendant={stavke[izbor] ? `paleta-${stavke[izbor].kljuc}` : undefined}
            className="h-14 flex-1 bg-transparent text-base outline-none"
          />
          <kbd className="rounded border border-line px-1.5 py-0.5 text-[11px] text-muted">Esc</kbd>
        </div>
        <ul ref={lista} role="listbox" className="max-h-[55vh] overflow-y-auto p-2">
          {stavke.map((s, i) => {
            const naslovGrupe = s.grupa !== prethodnaGrupa ? s.grupa : null;
            prethodnaGrupa = s.grupa;
            return (
              <li key={s.kljuc} role="presentation">
                {naslovGrupe && (
                  <p className="px-3 pb-1 pt-2 text-[11px] font-semibold uppercase tracking-wide text-muted">{naslovGrupe}</p>
                )}
                <button
                  type="button"
                  id={`paleta-${s.kljuc}`}
                  role="option"
                  aria-selected={i === izbor}
                  data-idx={i}
                  onMouseMove={() => setIzbor(i)}
                  onClick={() => otvori(s)}
                  className={cn(
                    "flex w-full items-center gap-3 rounded-lg px-3 py-2 text-left text-sm",
                    i === izbor ? "bg-brand-600 text-white" : "text-ink",
                  )}
                >
                  <s.ikona className={cn("h-4 w-4 shrink-0", i === izbor ? "text-white" : "text-muted")} />
                  <span className="min-w-0 flex-1 truncate">
                    <span className="font-medium">{s.naslov}</span>
                    {s.opis && <span className={cn("ml-2 text-xs", i === izbor ? "text-white/75" : "text-muted")}>{s.opis}</span>}
                  </span>
                  {i === izbor && <CornerDownLeft className="h-4 w-4 shrink-0" />}
                </button>
              </li>
            );
          })}
          {stavke.length === 0 && !ucitava && (
            <li className="px-3 py-8 text-center text-sm text-muted">Ništa za „{upit}“.</li>
          )}
          {ucitava && <li className="px-3 py-2 text-xs text-muted">Tražim narudžbe i kupce…</li>}
        </ul>
        <div className="flex items-center gap-4 border-t border-line bg-sand/40 px-4 py-2 text-[11px] text-muted">
          <span><kbd className="font-semibold">↑↓</kbd> biraj</span>
          <span><kbd className="font-semibold">Enter</kbd> otvori</span>
          <span className="ml-auto"><kbd className="font-semibold">Ctrl K</kbd> ili <kbd className="font-semibold">/</kbd> bilo gdje u adminu</span>
        </div>
      </div>
    </div>
  );
}
