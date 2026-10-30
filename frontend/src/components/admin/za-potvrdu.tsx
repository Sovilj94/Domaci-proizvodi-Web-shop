"use client";

import { useQuery } from "@tanstack/react-query";
import { adminKorpiceApi } from "@/lib/api/endpoints";
import { cn } from "@/lib/utils";

/**
 * Koliko korpica na čekanju stanje dozvoljava da se potvrdi — po sedmici isporuke. Server
 * računa slobodno stanje samo naspram već potvrđenih korpica i uzima ove na čekanju redom kojim
 * su stigle, pa brojevi zajedno važe.
 */
export function ZaPotvrdu() {
  // Pod ["admin", "orders"], da ga promjena statusa narudžbe osvježi zajedno sa listom.
  const q = useQuery({
    queryKey: ["admin", "orders", "potvrdjivanje"],
    queryFn: () => adminKorpiceApi.potvrdjivanje(),
    refetchInterval: 30_000,
  });
  const sedmice = q.data ?? [];

  return (
    <section className="rounded-card border border-line bg-surface px-5 py-3">
      <h2 className="font-display text-lg font-bold text-ink">Korpice na čekanju — koliko može da se potvrdi</h2>
      {q.isLoading ? (
        <p className="mt-1 text-sm text-muted">Učitavanje…</p>
      ) : q.isError ? (
        <p className="mt-1 text-sm text-red-700">Ne mogu da učitam — osvježi stranicu.</p>
      ) : sedmice.length === 0 ? (
        <p className="mt-1 text-sm text-muted">Nema korpica na čekanju.</p>
      ) : (
        <div className="mt-2 space-y-3">
          {sedmice.map((s) => (
            <div key={`${s.godina}-${s.sedmica}`}>
              {sedmice.length > 1 && <h3 className="text-sm font-semibold text-ink">Sedmica {s.sedmica}.</h3>}
              <ul className="divide-y divide-line text-sm">
                {s.korpice.map((k) => {
                  const sve = k.mozeSePotvrditi >= k.naCekanju;
                  return (
                    <li key={k.korpicaId} className="flex flex-wrap justify-between gap-x-3 py-1.5">
                      <span>
                        {k.naziv}
                        {k.nedostaje && <span className="ml-1.5 text-xs text-muted">· nedostaje: {k.nedostaje}</span>}
                      </span>
                      <span className={cn("font-semibold tabular-nums", sve ? "text-brand-700" : "text-red-700")}>
                        {k.mozeSePotvrditi} od {k.naCekanju} kom
                      </span>
                    </li>
                  );
                })}
              </ul>
            </div>
          ))}
          <p className="text-xs text-muted">
            Računa se redom kojim su narudžbe stigle, od stanja umanjenog za već potvrđene korpice.
          </p>
        </div>
      )}
    </section>
  );
}
