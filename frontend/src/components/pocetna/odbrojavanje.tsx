"use client";

import { useEffect, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { queryKeys, useSedmica } from "@/lib/api/hooks";
import { aktivniKrug, isporukaTekst, rokTekst } from "@/lib/sedmica";

const DAN = 86_400;

function rastavi(ms: number) {
  const s = Math.max(0, Math.floor(ms / 1000));
  return {
    dani: Math.floor(s / DAN),
    sati: Math.floor((s % DAN) / 3600),
    minute: Math.floor((s % 3600) / 60),
    sekunde: s % 60,
  };
}

/**
 * Odbrojavanje do roka naručivanja za krug koji je trenutno otvoren (vidi `aktivniKrug`).
 *
 * Sat se čita tek u efektu: početna je klijentska, ali je Next ipak prerenderuje, pa bi
 * `Date.now()` u renderu dao različit tekst na serveru i klijentu (greška hidratacije).
 *
 * Kad brojač dođe do nule, sedmica se ponovo traži sa servera — server odlučuje da je rok
 * prošao i prebacuje na sljedeći krug. Dok odgovor ne stigne (ili ako je sat klijenta
 * ispred serverskog), piše „Rok je upravo istekao" umjesto nula ili negativnih brojeva.
 * Dok je tako (istekao, a server još nije prebacio na sljedeći krug), sedmica se traži
 * ponovo na svakih ~10 s; interval staje čim server prebaci krug ili se komponenta ukloni.
 */
export function Odbrojavanje() {
  const { data } = useSedmica();
  const qc = useQueryClient();
  const [sada, setSada] = useState<number | null>(null);

  useEffect(() => {
    setSada(Date.now());
    const id = setInterval(() => setSada(Date.now()), 1000);
    return () => clearInterval(id);
  }, []);

  const krug = data ? aktivniKrug(data) : null;
  const preostalo = krug && sada !== null ? krug.rok.getTime() - sada : null;
  const istekao = preostalo !== null && preostalo <= 0;

  const ceka = istekao && !krug?.zaSljedecu;

  useEffect(() => {
    if (istekao) qc.invalidateQueries({ queryKey: queryKeys.sedmica() });
  }, [istekao, qc]);

  useEffect(() => {
    if (!ceka) return;
    const id = setInterval(() => qc.invalidateQueries({ queryKey: queryKeys.sedmica() }), 10_000);
    return () => clearInterval(id);
  }, [ceka, qc]);

  if (!krug || preostalo === null) return null;

  const { dani, sati, minute, sekunde } = rastavi(preostalo);
  // Sekunde tek u posljednja 24 sata (spec, O8) — do tada stranica ne treba da treperi.
  const polja: [number, string][] = [
    [dani, "dana"],
    [sati, "sati"],
    [minute, "minuta"],
    ...(dani === 0 ? ([[sekunde, "sekundi"]] as [number, string][]) : []),
  ];

  // Izgled po uzoru na farmit (R1): krem kutija, ⏱ ROK NARUČIVANJA, brojevi razdvojeni dvotačkom.
  return (
    <div
      className="mx-auto w-fit min-w-[260px] rounded-2xl bg-beige px-8 py-5 text-center shadow-[0_6px_20px_rgba(64,74,61,0.12)]"
      title={`Poruči do ${rokTekst(krug.rok)} — korpica stiže u ${isporukaTekst(krug.isporuka)}`}
    >
      <p className="text-sm font-semibold uppercase tracking-wide text-brand-600">
        ⏱ {krug.zaSljedecu ? "Rok naručivanja za sljedeću sedmicu" : "Rok naručivanja ove sedmice"}
      </p>

      {istekao ? (
        <p className="mt-3 text-lg font-semibold text-ink">Rok je upravo istekao.</p>
      ) : (
        <div className="mt-3 flex items-start justify-center gap-3" role="timer" aria-label="Preostalo vrijeme do roka">
          {polja.map(([vrijednost, oznaka], i) => (
            <div key={oznaka} className="flex items-start gap-3">
              {i > 0 && <span className="text-2xl font-medium text-ink">:</span>}
              <div className="text-center">
                <span className="block text-3xl font-medium tabular-nums text-ink">
                  {String(vrijednost).padStart(2, "0")}
                </span>
                <span className="text-[11px] uppercase text-ink/80">{oznaka}</span>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
