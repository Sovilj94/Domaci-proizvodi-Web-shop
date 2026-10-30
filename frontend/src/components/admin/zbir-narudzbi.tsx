"use client";

import { useEffect, useMemo, useState } from "react";
import { ChevronDown } from "lucide-react";
import { saberi, type ZbirRed } from "@/lib/admin/zbir";
import { cn } from "@/lib/utils";
import type { Order } from "@/types/domain";

const OTVOREN_KLJUC = "epiljara.admin.zbirOtvoren";

/**
 * Zbir narudžbi koje su na ekranu: koliko kojih korpica i koliko ostalih artikala traže narudžbe
 * na čekanju i potvrđene — da se pri potvrđivanju vidi šta je već obećano.
 */
export function ZbirNarudzbi({ narudzbe }: { narudzbe: Order[] }) {
  const zbir = useMemo(() => saberi(narudzbe), [narudzbe]);
  const [otvoren, setOtvoren] = useState(true);
  useEffect(() => {
    try {
      if (localStorage.getItem(OTVOREN_KLJUC) === "0") setOtvoren(false);
    } catch {
      // Bez localStorage panel ostaje otvoren.
    }
  }, []);
  function prebaci() {
    setOtvoren((prije) => {
      try {
        localStorage.setItem(OTVOREN_KLJUC, prije ? "0" : "1");
      } catch {
        // Nije bitno ako se ne zapamti.
      }
      return !prije;
    });
  }

  const brojKorpica = zbir.korpice.reduce((s, r) => s + r.ukupno, 0);

  return (
    <section className="rounded-card border border-line bg-surface">
      <button
        type="button"
        onClick={prebaci}
        aria-expanded={otvoren}
        className="flex w-full items-center justify-between gap-3 px-5 py-3 text-left"
      >
        <span>
          <span className="font-display text-lg font-bold text-ink">Zbir</span>
          <span className="ml-2 text-sm text-muted">
            na čekanju i potvrđene · {brojKorpica} {oblik(brojKorpica, "korpica", "korpice", "korpica")} ·{" "}
            {zbir.ostalo.length} {oblik(zbir.ostalo.length, "ostali artikal", "ostala artikla", "ostalih artikala")}
          </span>
        </span>
        <ChevronDown className={cn("h-5 w-5 shrink-0 text-muted transition", otvoren && "rotate-180")} />
      </button>
      {otvoren && (
        <div className="grid gap-6 border-t border-line px-5 py-4 lg:grid-cols-2">
          <Tabela naslov="Korpice" redovi={zbir.korpice} prazno="Nema korpica u ovim narudžbama." />
          <Tabela naslov="Ostali artikli" redovi={zbir.ostalo} prazno="Nema drugih artikala osim korpica." />
        </div>
      )}
    </section>
  );
}

/** Srpska množina: 1 korpica, 2–4 korpice, 5+ i 11–14 korpica. */
function oblik(n: number, jedan: string, dva: string, pet: string) {
  const jedinice = n % 10;
  const desetice = n % 100;
  if (jedinice === 1 && desetice !== 11) return jedan;
  if (jedinice >= 2 && jedinice <= 4 && (desetice < 12 || desetice > 14)) return dva;
  return pet;
}

function kol(x: number, jedinica: string) {
  return x === 0 ? "—" : `${Number(x.toFixed(2))} ${jedinica}`;
}

function Tabela({ naslov, redovi, prazno }: { naslov: string; redovi: ZbirRed[]; prazno: string }) {
  return (
    <div>
      <h3 className="text-sm font-semibold text-ink">{naslov}</h3>
      {redovi.length === 0 ? (
        <p className="mt-2 text-sm text-muted">{prazno}</p>
      ) : (
        <table className="mt-2 w-full text-sm">
          <thead>
            <tr className="text-left text-xs text-muted">
              <th className="py-1 font-medium">Artikal</th>
              <th className="py-1 text-right font-medium">Na čekanju</th>
              <th className="py-1 text-right font-medium">Potvrđeno</th>
              <th className="py-1 text-right font-medium">Ukupno</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-line">
            {redovi.map((r) => (
              <tr key={r.productId}>
                <td className="py-1.5 pr-2">{r.naziv}</td>
                <td className="py-1.5 text-right tabular-nums">{kol(r.naCekanju, r.jedinica)}</td>
                <td className="py-1.5 text-right tabular-nums">{kol(r.potvrdjeno, r.jedinica)}</td>
                <td className="py-1.5 text-right font-semibold tabular-nums">{kol(r.ukupno, r.jedinica)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
