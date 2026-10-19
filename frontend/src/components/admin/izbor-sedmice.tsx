"use client";

import { useMemo } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { ChevronLeft, ChevronRight } from "lucide-react";
import { Button } from "@/components/ui/button";
import { formatDate } from "@/lib/utils";

/** ISO broj sedmice za dati datum — isti račun kao na backendu (ISO-8601, četvrtak određuje sedmicu). */
export function isoSedmica(d: Date): { godina: number; sedmica: number } {
  const t = new Date(Date.UTC(d.getFullYear(), d.getMonth(), d.getDate()));
  t.setUTCDate(t.getUTCDate() + 4 - (t.getUTCDay() || 7));
  const prvi = new Date(Date.UTC(t.getUTCFullYear(), 0, 1));
  const sedmica = Math.ceil(((t.getTime() - prvi.getTime()) / 86400000 + 1) / 7);
  return { godina: t.getUTCFullYear(), sedmica };
}

/** Ponedjeljak–nedjelja te ISO sedmice, samo za prikaz raspona datuma. */
export function raspon(d: Date): { od: Date; do: Date } {
  const cetvrtak = new Date(Date.UTC(d.getFullYear(), d.getMonth(), d.getDate()));
  cetvrtak.setUTCDate(cetvrtak.getUTCDate() + 4 - (cetvrtak.getUTCDay() || 7));
  const od = new Date(cetvrtak);
  od.setUTCDate(cetvrtak.getUTCDate() - 3);
  const doDatum = new Date(cetvrtak);
  doDatum.setUTCDate(cetvrtak.getUTCDate() + 3);
  return { od, do: doDatum };
}

/**
 * Izabrana sedmica živi u URL-u (?datum=) kao sidro — bilo koji dan te ISO sedmice radi, a
 * „Sljedeća/Prethodna" samo pomjeraju sidro za 7 dana, bez ručnog računanja prelaska godine.
 * Dijele je Korpice, Pakovanje i Narudžbe, pa link sa jednog ekrana nosi istu sedmicu na drugi.
 */
export function useIzabranaSedmica() {
  const params = useSearchParams();
  const router = useRouter();

  const anchor = useMemo(() => {
    const raw = params.get("datum");
    const d = raw ? new Date(`${raw}T00:00:00Z`) : null;
    return d && !Number.isNaN(d.getTime()) ? d : new Date();
  }, [params]);

  const { godina, sedmica } = useMemo(() => isoSedmica(anchor), [anchor]);
  const { od, do: doDatum } = useMemo(() => raspon(anchor), [anchor]);

  function pomjeri(dani: number) {
    const sljedeci = new Date(anchor);
    sljedeci.setUTCDate(sljedeci.getUTCDate() + dani);
    const next = new URLSearchParams(params.toString());
    next.set("datum", sljedeci.toISOString().slice(0, 10));
    router.replace(`?${next}`, { scroll: false });
  }

  function idiNaDanas() {
    const next = new URLSearchParams(params.toString());
    next.delete("datum");
    router.replace(next.toString() ? `?${next}` : "?", { scroll: false });
  }

  return { godina, sedmica, od, doDatum, pomjeri, idiNaDanas, datumParam: params.get("datum") };
}

export function IzborSedmice({ s }: { s: ReturnType<typeof useIzabranaSedmica> }) {
  return (
    <div className="flex items-center gap-2">
      <button
        type="button"
        onClick={() => s.pomjeri(-7)}
        aria-label="Prethodna sedmica"
        className="grid h-9 w-9 shrink-0 place-items-center rounded-full border border-line text-muted transition hover:bg-sand hover:text-ink"
      >
        <ChevronLeft className="h-4 w-4" />
      </button>
      <div className="w-36 text-center">
        <p className="text-sm font-semibold text-ink">
          Sedmica {s.sedmica} · {s.godina}
        </p>
        <p className="text-xs text-muted">
          {formatDate(s.od.toISOString())} – {formatDate(s.doDatum.toISOString())}
        </p>
      </div>
      <button
        type="button"
        onClick={() => s.pomjeri(7)}
        aria-label="Sljedeća sedmica"
        className="grid h-9 w-9 shrink-0 place-items-center rounded-full border border-line text-muted transition hover:bg-sand hover:text-ink"
      >
        <ChevronRight className="h-4 w-4" />
      </button>
      <Button variant="secondary" size="sm" onClick={s.idiNaDanas}>
        Danas
      </Button>
    </div>
  );
}
