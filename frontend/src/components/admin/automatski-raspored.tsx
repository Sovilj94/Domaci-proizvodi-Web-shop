"use client";

import { useState } from "react";
import { Wand2, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useToast } from "@/components/ui/toast";
import { adminApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import type { Driver, PrijedlogNaselje, PrijedlogRasporeda } from "@/lib/api/contracts";

/**
 * „Rasporedi automatski": server predloži koja naselja idu u koji kombi (susjedna naselja istom
 * vozaču, od najbližeg magacinu). Admin vidi prijedlog, po potrebi prebaci naselje drugom
 * vozaču, pa potvrdi. Potvrda je grupna dodjela po vozaču — ista koja i inače optimizuje rute,
 * pa se rute same slože čim vozač dobije svoja naselja.
 */
export function AutomatskiRaspored({
  date,
  godina,
  sedmica,
  vozaci,
  onGotovo,
}: {
  date: string;
  godina: number;
  sedmica: number;
  vozaci: Driver[];
  onGotovo: () => void;
}) {
  const { show } = useToast();
  const [prijedlog, setPrijedlog] = useState<PrijedlogRasporeda | null>(null);
  /** naselje → vozač ("" = ne dodjeljuj sada) */
  const [plan, setPlan] = useState<Record<string, string>>({});
  const [radi, setRadi] = useState<"prijedlog" | "potvrda" | null>(null);

  const naselja: PrijedlogNaselje[] = prijedlog
    ? [...prijedlog.kombiji.flatMap((k) => k.naselja), ...prijedlog.nerasporedjeno]
    : [];
  const aktivni = vozaci.filter((v) => v.aktivan);

  async function predlozi() {
    setRadi("prijedlog");
    try {
      const p = await adminApi.prijedlogRasporeda(date, godina, sedmica);
      setPrijedlog(p);
      setPlan(
        Object.fromEntries([
          ...p.kombiji.flatMap((k) => k.naselja.map((n) => [n.zonaId, k.vozacId] as const)),
          ...p.nerasporedjeno.map((n) => [n.zonaId, ""] as const),
        ]),
      );
    } catch (e) {
      show(e instanceof ApiRequestError ? e.message : "Prijedlog nije napravljen", "error");
    } finally {
      setRadi(null);
    }
  }

  async function potvrdi() {
    setRadi("potvrda");
    const poVozacu = new Map<string, string[]>();
    for (const n of naselja) {
      const v = plan[n.zonaId];
      if (v) poVozacu.set(v, [...(poVozacu.get(v) ?? []), ...n.orderIds]);
    }
    const greske: string[] = [];
    // Redom, ne paralelno: dodjela jednog vozača čita i mijenja raspored cijelog dana.
    for (const [driverId, orderIds] of poVozacu) {
      try {
        await adminApi.assignOrdersBulk({ orderIds, driverId, date });
      } catch (e) {
        const ime = aktivni.find((v) => v.id === driverId)?.name ?? "Vozač";
        greske.push(`${ime}: ${e instanceof ApiRequestError ? e.message : "nije dodijeljeno"}`);
      }
    }
    setRadi(null);
    onGotovo();
    if (greske.length) show(greske.join(" · "), "error");
    else {
      show(`Raspoređeno ${poVozacu.size} ${poVozacu.size === 1 ? "vozaču" : "vozača"} — rute su optimizovane`);
      setPrijedlog(null);
    }
  }

  if (!prijedlog)
    return (
      <Button variant="secondary" onClick={predlozi} disabled={radi !== null}>
        <Wand2 className="h-4 w-4" /> {radi === "prijedlog" ? "Računam…" : "Rasporedi automatski"}
      </Button>
    );

  const teret = (vozacId: string) =>
    naselja.filter((n) => plan[n.zonaId] === vozacId).reduce((a, n) => a + n.orderIds.length, 0);

  return (
    <div className="w-full space-y-3 rounded-card border border-brand-200 bg-brand-50/60 p-4">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h2 className="font-display text-lg font-bold text-ink">Prijedlog raspodjele po kombijima</h2>
          <p className="text-sm text-muted">
            Susjedna naselja idu istom vozaču, od najbližeg magacinu. Prebaci naselje ako treba, pa potvrdi —
            rute se optimizuju same.
          </p>
        </div>
        <button
          type="button"
          onClick={() => setPrijedlog(null)}
          aria-label="Zatvori prijedlog"
          className="grid h-8 w-8 place-items-center rounded-full text-muted hover:bg-white hover:text-ink"
        >
          <X className="h-4 w-4" />
        </button>
      </div>

      {naselja.length === 0 ? (
        <p className="text-sm text-muted">Nema nedodijeljenih narudžbi za ovaj dan.</p>
      ) : (
        <>
          <div className="flex flex-wrap gap-2 text-xs">
            {aktivni
              .filter((v) => teret(v.id) > 0)
              .map((v) => (
                <span
                  key={v.id}
                  className={`rounded-full px-2.5 py-1 font-semibold ${teret(v.id) > v.maxStanica ? "bg-red-100 text-red-700" : "bg-white text-ink"}`}
                >
                  {v.name}: {teret(v.id)} stanica
                </span>
              ))}
          </div>
          <ul className="divide-y divide-line overflow-hidden rounded-xl bg-white">
            {naselja.map((n) => (
              <li key={n.zonaId} className="flex flex-wrap items-center justify-between gap-2 px-3 py-2 text-sm">
                <span className="font-medium text-ink">
                  {n.naziv} <span className="text-muted">· {n.orderIds.length}</span>
                </span>
                <select
                  aria-label={`Vozač za ${n.naziv}`}
                  value={plan[n.zonaId] ?? ""}
                  onChange={(e) => setPlan((p) => ({ ...p, [n.zonaId]: e.target.value }))}
                  className="rounded-lg border border-line bg-white px-2 py-1 text-sm"
                >
                  <option value="">— ne dodjeljuj sada —</option>
                  {aktivni.map((v) => (
                    <option key={v.id} value={v.id}>
                      {v.name}
                    </option>
                  ))}
                </select>
              </li>
            ))}
          </ul>
          {prijedlog.nerasporedjeno.length > 0 && (
            <p className="text-sm text-honey-700">
              Nije stalo u slobodne kapacitete: {prijedlog.nerasporedjeno.map((n) => n.naziv).join(", ")} — dodijeli
              ručno ili dodaj vozača.
            </p>
          )}
          <div className="flex justify-end gap-2">
            <Button variant="secondary" size="sm" onClick={() => setPrijedlog(null)}>
              Odustani
            </Button>
            <Button size="sm" onClick={potvrdi} disabled={radi !== null}>
              {radi === "potvrda" ? "Raspoređujem…" : "Potvrdi raspodjelu"}
            </Button>
          </div>
        </>
      )}
    </div>
  );
}
