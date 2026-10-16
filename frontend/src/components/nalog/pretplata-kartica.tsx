"use client";

import { useState } from "react";
import { AlertTriangle, Pause, Pencil, Play, RefreshCw, Trash2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Select } from "@/components/ui/field";
import { formatPrice, formatDate } from "@/lib/utils";
import { isporuka, PLAN_TEKST } from "@/lib/planovi";
import type { Pretplata } from "@/types/domain";

const STATUS_OZNAKA: Record<Pretplata["status"], { label: string; badge: string }> = {
  Aktivna: { label: "Aktivna", badge: "bg-brand-100 text-brand-700" },
  Pauzirana: { label: "Pauzirana", badge: "bg-honey-100 text-honey-600" },
  Otkazana: { label: "Otkazana", badge: "bg-zinc-200 text-zinc-600" },
};

export function PretplataKartica({
  pretplata,
  onIzmijeni,
  onPauziraj,
  onNastavi,
  onOtkazi,
  pending,
}: {
  pretplata: Pretplata;
  onIzmijeni: () => void;
  onPauziraj: (sedmica: number) => void;
  onNastavi: () => void;
  onOtkazi: () => Promise<unknown>;
  pending: boolean;
}) {
  // Inline potvrda i inline birač pauze umjesto `window.confirm`/`prompt` — nativni
  // dijalog zamrzava automatizaciju koja provjerava ovaj ekran (isto kao u adresaru).
  const [potvrdaOtkaza, setPotvrdaOtkaza] = useState(false);
  const [biracPauze, setBiracPauze] = useState(false);
  const [sedmica, setSedmica] = useState(1);

  const p = pretplata;
  const otkazana = p.status === "Otkazana";
  const status = STATUS_OZNAKA[p.status];

  return (
    <div className="flex flex-col gap-3 rounded-card border border-line bg-surface p-5 shadow-card">
      <div className="flex items-start justify-between gap-2">
        <div className="flex items-center gap-2">
          <span className="grid h-9 w-9 shrink-0 place-items-center rounded-full bg-sand text-brand-600">
            <RefreshCw className="h-4 w-4" />
          </span>
          <div>
            <p className="font-display font-semibold text-ink">{p.naziv}</p>
            {p.deliverySlotLabel && (
              <p className="text-xs text-muted">{p.deliverySlotLabel}</p>
            )}
          </div>
        </div>
        <span
          className={`shrink-0 rounded-full px-2.5 py-1 text-xs font-semibold ${status.badge}`}
        >
          {status.label}
        </span>
      </div>

      <div className="flex items-baseline gap-2">
        {p.popustAktivan && (
          <span className="text-sm text-muted line-through">{formatPrice(p.punaCijena)}</span>
        )}
        <span className="font-display text-lg font-bold text-ink">
          {formatPrice(p.trenutnaCijena)}
        </span>
        <span className="text-xs text-muted">po isporuci</span>
      </div>

      {/* Plan i obaveza (R2): pretplatnička cijena važi od prve isporuke. */}
      {!otkazana && (
        <div>
          <p className="text-xs font-medium text-brand-700">
            Plan: {PLAN_TEKST[p.plan]} · {isporuka(p.brojIsporuka)} iza tebe
          </p>
          {p.obavezaIsporuka > 0 && (
            <>
              <p className="mt-1 text-xs text-muted">
                {p.preostaloObaveze > 0
                  ? `Obaveza: još ${p.preostaloObaveze} od ${isporuka(p.obavezaIsporuka)}`
                  : `Obaveza od ${isporuka(p.obavezaIsporuka)} je ispunjena`}
              </p>
              <div className="mt-1.5 h-1.5 w-full overflow-hidden rounded-full bg-sand">
                <div
                  className="h-full rounded-full bg-brand-500 transition-[width]"
                  style={{
                    width: `${Math.round(((p.obavezaIsporuka - p.preostaloObaveze) / p.obavezaIsporuka) * 100)}%`,
                  }}
                />
              </div>
            </>
          )}
        </div>
      )}

      <p className="text-sm text-ink">{p.deliveryAddress}</p>
      {p.deliveryZoneName && <p className="text-xs text-muted">Zona: {p.deliveryZoneName}</p>}

      {!p.deliveryZoneActive && !otkazana && (
        <p className="flex items-start gap-1.5 rounded-lg bg-red-50 px-3 py-2 text-xs font-medium text-red-700">
          <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
          Zona dostave je ugašena — izmijeni pretplatu, inače naredna korpica neće biti
          naručena.
        </p>
      )}

      {p.status === "Pauzirana" && p.pauzaDoSedmica ? (
        <p className="rounded-lg bg-honey-100/60 px-3 py-2 text-xs font-medium text-honey-600">
          Pauzirano do {p.pauzaDoSedmica}. sedmice {p.pauzaDoGodina}.
        </p>
      ) : (
        !otkazana &&
        p.sljedecaIsporuka && (
          <p className="text-xs text-muted">
            Sljedeća isporuka: <span className="font-medium text-ink">{formatDate(p.sljedecaIsporuka)}</span>{" "}
            ({p.sljedecaSedmica}. sedmica)
          </p>
        )
      )}

      {!otkazana && (
        <div className="mt-auto flex flex-wrap items-center gap-2 border-t border-line pt-3">
          {potvrdaOtkaza ? (
            <>
              <span className="text-sm font-medium text-ink">Sigurno otkazuješ?</span>
              <Button
                variant="danger"
                size="sm"
                disabled={pending}
                onClick={() => void onOtkazi().catch(() => setPotvrdaOtkaza(false))}
              >
                {pending ? "Otkazujem…" : "Otkaži pretplatu"}
              </Button>
              <Button variant="secondary" size="sm" onClick={() => setPotvrdaOtkaza(false)}>
                Nazad
              </Button>
            </>
          ) : biracPauze ? (
            <>
              <span className="text-sm font-medium text-ink">Preskoči</span>
              <Select
                className="w-28"
                value={String(sedmica)}
                onChange={(e) => setSedmica(Number(e.target.value))}
              >
                {[1, 2, 3, 4, 5, 6, 7, 8].map((n) => (
                  <option key={n} value={n}>
                    {n} {n === 1 ? "isporuku" : "isporuke"}
                  </option>
                ))}
              </Select>
              <Button
                size="sm"
                disabled={pending}
                onClick={() => {
                  onPauziraj(sedmica);
                  setBiracPauze(false);
                }}
              >
                Pauziraj
              </Button>
              <Button variant="secondary" size="sm" onClick={() => setBiracPauze(false)}>
                Nazad
              </Button>
            </>
          ) : (
            <>
              <Button variant="secondary" size="sm" onClick={onIzmijeni}>
                <Pencil className="h-3.5 w-3.5" /> Izmijeni
              </Button>
              {p.status === "Aktivna" ? (
                <Button variant="secondary" size="sm" onClick={() => setBiracPauze(true)}>
                  <Pause className="h-3.5 w-3.5" /> Pauziraj
                </Button>
              ) : (
                <Button variant="secondary" size="sm" disabled={pending} onClick={onNastavi}>
                  <Play className="h-3.5 w-3.5" /> Nastavi
                </Button>
              )}
              {p.mozeOtkazati ? (
                <Button
                  variant="ghost"
                  size="sm"
                  className="text-red-700 hover:bg-red-50"
                  onClick={() => setPotvrdaOtkaza(true)}
                >
                  <Trash2 className="h-3.5 w-3.5" /> Otkaži
                </Button>
              ) : (
                <span className="self-center text-xs text-muted">Za otkaz tokom obaveze javi nam se.</span>
              )}
            </>
          )}
        </div>
      )}
    </div>
  );
}
