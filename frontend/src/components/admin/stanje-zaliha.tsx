"use client";

import { useEffect, useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { ChevronDown } from "lucide-react";
import { catalogApi, korpiceApi } from "@/lib/api/endpoints";
import { queryKeys } from "@/lib/api/hooks";
import { stanje, type Oznaka, type StanjeRed } from "@/lib/admin/stanje";
import { cn } from "@/lib/utils";

const OTVOREN_KLJUC = "epiljara.admin.stanjeOtvoreno";

/** Isti ritam kao lista narudžbi: nova narudžba umanjuje zalihu, pa se i ovo osvježava. */
const OSVJEZI = 30_000;

const TON: Record<Oznaka, string> = {
  ok: "text-ink",
  malo: "text-honey-700",
  rasprodato: "text-red-700",
  vanProdaje: "text-muted",
};

/** Koliko je korpica i artikala iz Špajza ostalo za prodaju — da se pri potvrđivanju zna šta još ima. */
export function StanjeZaliha() {
  const korpice = useQuery({ queryKey: queryKeys.korpiceLista(), queryFn: () => korpiceApi.lista(), refetchInterval: OSVJEZI });
  const proizvodi = useQuery({ queryKey: queryKeys.products({}), queryFn: () => catalogApi.products({}), refetchInterval: OSVJEZI });
  const s = useMemo(() => stanje(korpice.data ?? [], proizvodi.data ?? []), [korpice.data, proizvodi.data]);

  const [otvoreno, setOtvoreno] = useState(true);
  useEffect(() => {
    try {
      if (localStorage.getItem(OTVOREN_KLJUC) === "0") setOtvoreno(false);
    } catch {
      // Bez localStorage panel ostaje otvoren.
    }
  }, []);
  function prebaci() {
    setOtvoreno((prije) => {
      try {
        localStorage.setItem(OTVOREN_KLJUC, prije ? "0" : "1");
      } catch {
        // Nije bitno ako se ne zapamti.
      }
      return !prije;
    });
  }

  const svi = [...s.korpice, ...s.spajz];
  const rasprodato = svi.filter((r) => r.oznaka === "rasprodato").length;
  const malo = svi.filter((r) => r.oznaka === "malo").length;

  return (
    <section className="rounded-card border border-line bg-surface">
      <button
        type="button"
        onClick={prebaci}
        aria-expanded={otvoreno}
        className="flex w-full items-center justify-between gap-3 px-5 py-3 text-left"
      >
        <span>
          <span className="font-display text-lg font-bold text-ink">Preostalo za prodaju</span>
          {rasprodato > 0 && <span className="ml-2 text-sm font-medium text-red-700">{rasprodato} rasprodato</span>}
          {malo > 0 && <span className="ml-2 text-sm font-medium text-honey-700">{malo} pri kraju</span>}
        </span>
        <ChevronDown className={cn("h-5 w-5 shrink-0 text-muted transition", otvoreno && "rotate-180")} />
      </button>
      {otvoreno && (
        <div className="grid gap-6 border-t border-line px-5 py-4 lg:grid-cols-2">
          <Spisak naslov="Korpice" redovi={s.korpice} ucitava={korpice.isLoading} />
          <Spisak naslov="Špajz" redovi={s.spajz} ucitava={proizvodi.isLoading} />
        </div>
      )}
    </section>
  );
}

function Spisak({ naslov, redovi, ucitava }: { naslov: string; redovi: StanjeRed[]; ucitava: boolean }) {
  return (
    <div>
      <h3 className="text-sm font-semibold text-ink">{naslov}</h3>
      {ucitava ? (
        <p className="mt-2 text-sm text-muted">Učitavanje…</p>
      ) : redovi.length === 0 ? (
        <p className="mt-2 text-sm text-muted">Ništa nije u ponudi.</p>
      ) : (
        <ul className="mt-2 divide-y divide-line text-sm">
          {redovi.map((r) => (
            <li key={r.id} className={cn("flex justify-between gap-3 py-1.5", r.oznaka === "vanProdaje" && "text-muted")}>
              <span>
                {r.naziv}
                {r.oznaka === "vanProdaje" && <span className="ml-1.5 text-xs">· skinuto iz prodaje</span>}
              </span>
              <span className={cn("font-semibold tabular-nums", TON[r.oznaka])}>
                {r.oznaka === "rasprodato" ? "rasprodato" : `${r.ostalo} ${r.jedinica}`}
              </span>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
