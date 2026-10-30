"use client";

import { usePodesavanja } from "@/lib/api/hooks";
import { useState } from "react";
import { ArrowLeftRight, Minus, Plus, Search, X } from "lucide-react";
import { SmartImage } from "@/components/ui/smart-image";
import {
  type KutijaRed,
  kolicinaZaDodavanje,
  korak,
  naKorak,
  zamijeni,
  zbir,
} from "@/lib/admin/kutija";
import { bezKvacica } from "@/lib/admin/proizvodi-tabela";
import { formatMarza, tonMarze } from "@/lib/marza";
import type { Product } from "@/types/domain";
import { cn, formatPrice, unitSuffix } from "@/lib/utils";

const TON = { nema: "text-muted", losa: "text-red-700", tijesna: "text-honey-700", dobra: "text-brand-700" } as const;

/**
 * Kutija koju admin uređuje — isti alat za sedmični sadržaj korpice i za korpicu jednog
 * kupca. Količina se mijenja na + / − (ili upisom), artikal se mijenja drugim iste
 * vrijednosti (⇄), a dodaje se klikom na pločicu sezonske ponude — u količini koja popuni
 * ono što fali do cilja. Gore stalno stoji vrijednost naspram cilja i marža.
 */
export function KutijaEditor({
  redovi,
  onChange,
  cilj,
  cijenaKorpice,
  minimalnaMarza,
  ponuda,
  samoPregled = false,
  izbjegnuti,
}: {
  redovi: KutijaRed[];
  onChange: (redovi: KutijaRed[]) => void;
  cilj: number;
  cijenaKorpice: number;
  /** Iz baze (Podesavanja); dok ne stigne, marža se ne boji. */
  minimalnaMarza: number | undefined;
  /** Sezonska ponuda za taj mjesec (vidi `sezonskaPonuda`). */
  ponuda: Product[];
  samoPregled?: boolean;
  /** Kupčev spisak „ne stavljaj mi" — red sa takvim artiklom se istakne. */
  izbjegnuti?: Set<string>;
}) {
  const [zamjenaZa, setZamjenaZa] = useState<string | null>(null);
  const z = zbir(redovi, cijenaKorpice);
  const uKutiji = new Set(redovi.map((r) => r.productId));
  // Prvo kategorije kojih već ima u kutiji (za korpicu povrća — povrće), pa ostala ponuda.
  const kategorijeKutije = new Set(ponuda.filter((p) => uKutiji.has(p.id)).map((p) => p.categoryId));
  const slobodno = ponuda
    .filter((p) => !uKutiji.has(p.id))
    .sort((a, b) => Number(kategorijeKutije.has(b.categoryId)) - Number(kategorijeKutije.has(a.categoryId)));
  const pct = cilj > 0 ? Math.round((z.vrijednost / cilj) * 100) : 0;
  const tonVrijednosti = cilj <= 0 ? "text-ink" : pct < 95 ? "text-honey-700" : pct > 105 ? "text-red-700" : "text-brand-700";

  function promijeni(productId: string, kolicina: number) {
    onChange(redovi.map((r) => (r.productId === productId ? { ...r, kolicina: Math.max(0, kolicina) } : r)));
  }

  function dodaj(p: Product) {
    onChange([
      ...redovi,
      {
        productId: p.id,
        naziv: p.name,
        jedinica: p.unit,
        kolicina: kolicinaZaDodavanje(cilj, z.vrijednost, p.price, p.unit),
        cijena: p.price,
        nabavna: p.nabavnaCijena ?? null,
        imageUrl: p.imageUrl,
        boja: p.boja,
        izmijenjeno: true,
      },
    ]);
  }

  return (
    <div className="flex flex-col gap-4">
      {/* Zbir: vrijednost naspram cilja i marža, stalno na vrhu. */}
      <div className="rounded-xl bg-sand p-3">
        <div className="flex items-baseline justify-between gap-3 text-sm">
          <span className="text-muted">Vrijednost</span>
          <span className={cn("font-semibold tabular-nums", tonVrijednosti)}>
            {formatPrice(z.vrijednost)}
            {cilj > 0 && <span className="font-normal text-muted"> / cilj {formatPrice(cilj)} ({pct}%)</span>}
          </span>
        </div>
        {cilj > 0 && (
          <div className="mt-1.5 h-1.5 overflow-hidden rounded-full bg-white">
            <div
              className={cn("h-full rounded-full transition-all", pct < 95 ? "bg-honey-500" : pct > 105 ? "bg-red-500" : "bg-brand-500")}
              style={{ width: `${Math.min(100, pct)}%` }}
            />
          </div>
        )}
        <div className="mt-2 flex items-baseline justify-between gap-3 text-sm">
          <span className="text-muted">Marža (cijena {formatPrice(cijenaKorpice)})</span>
          <span className={cn("font-semibold", TON[tonMarze(z.marza, minimalnaMarza)])}>
            {formatMarza(z.marza)}
            <span className="font-normal text-muted"> · nabavno {formatPrice(z.nabavno)}</span>
          </span>
        </div>
        {z.bezNabavne > 0 && (
          <p className="mt-1 text-xs text-honey-700">{z.bezNabavne} bez nabavne cijene — nisu u marži</p>
        )}
      </div>

      <ul className="flex flex-col gap-1.5">
        {redovi.length === 0 && <li className="rounded-xl border border-dashed border-line p-4 text-center text-sm text-muted">Kutija je prazna — dodaj artikle ispod.</li>}
        {redovi.map((r) => {
          const otvoren = zamjenaZa === r.productId;
          const izbjegnut = izbjegnuti?.has(r.productId);
          return (
            <li key={r.productId} className={cn("rounded-xl border bg-white p-2", izbjegnut ? "border-red-300" : otvoren ? "border-brand-400" : "border-line")}>
              <div className="flex items-center gap-2.5">
                <span className="relative h-10 w-10 shrink-0 overflow-hidden rounded-lg" style={{ backgroundColor: r.boja ?? "var(--color-sand)" }}>
                  <SmartImage src={r.imageUrl ?? undefined} alt={r.naziv} sizes="40px" />
                </span>
                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm font-medium">
                    {r.naziv}
                    {r.izmijenjeno && <span className="ml-1.5 rounded bg-brand-100 px-1 text-[10px] font-semibold text-brand-700">izmjena</span>}
                    {izbjegnut && <span className="ml-1.5 rounded bg-red-100 px-1 text-[10px] font-semibold text-red-700">kupac ne želi</span>}
                  </p>
                  <p className="text-xs text-muted">
                    {formatPrice(r.cijena)}
                    {unitSuffix(r.jedinica)} · {formatPrice(Math.round(r.kolicina * r.cijena * 100) / 100)}
                  </p>
                </div>
                {samoPregled ? (
                  <span className="shrink-0 text-sm font-semibold tabular-nums">
                    {r.kolicina.toLocaleString("sr-Latn-BA")} {r.jedinica}
                  </span>
                ) : (
                  <>
                    <Stepper red={r} onPromijeni={(k) => promijeni(r.productId, k)} />
                    <button
                      type="button"
                      onClick={() => setZamjenaZa(otvoren ? null : r.productId)}
                      aria-label={`Zamijeni ${r.naziv}`}
                      title="Zamijeni artiklom iste vrijednosti"
                      className={cn("grid h-8 w-8 shrink-0 place-items-center rounded-full transition", otvoren ? "bg-ink text-white" : "text-muted hover:bg-sand hover:text-ink")}
                    >
                      <ArrowLeftRight className="h-4 w-4" />
                    </button>
                    <button
                      type="button"
                      onClick={() => onChange(redovi.filter((x) => x.productId !== r.productId))}
                      aria-label={`Ukloni ${r.naziv}`}
                      className="grid h-8 w-8 shrink-0 place-items-center rounded-full text-muted transition hover:bg-red-50 hover:text-red-600"
                    >
                      <X className="h-4 w-4" />
                    </button>
                  </>
                )}
              </div>
              {otvoren && (
                <div className="mt-2 border-t border-line pt-2">
                  <p className="mb-1.5 text-xs text-muted">Umjesto {r.naziv} — ista vrijednost ({formatPrice(r.kolicina * r.cijena)}):</p>
                  <Plocice
                    ponuda={slobodno}
                    opis={(p) => {
                      const n = zamijeni(r, p);
                      return `${n.kolicina.toLocaleString("sr-Latn-BA")} ${n.jedinica}`;
                    }}
                    onIzaberi={(p) => {
                      onChange(redovi.map((x) => (x.productId === r.productId ? zamijeni(r, p) : x)));
                      setZamjenaZa(null);
                    }}
                  />
                </div>
              )}
            </li>
          );
        })}
      </ul>

      {!samoPregled && (
        <div>
          <p className="mb-1.5 text-sm font-semibold">Dodaj iz sezonske ponude</p>
          <Plocice
            ponuda={slobodno}
            pretraga
            opis={(p) => `+${kolicinaZaDodavanje(cilj, z.vrijednost, p.price, p.unit).toLocaleString("sr-Latn-BA")} ${p.unit}`}
            onIzaberi={dodaj}
          />
        </div>
      )}
    </div>
  );
}

function Stepper({ red, onPromijeni }: { red: KutijaRed; onPromijeni: (k: number) => void }) {
  const k = korak(red.jedinica);
  return (
    <div className="flex shrink-0 items-center rounded-full bg-sand">
      <button
        type="button"
        aria-label={`Manje ${red.naziv}`}
        disabled={red.kolicina <= k}
        onClick={() => onPromijeni(naKorak(red.kolicina - k, red.jedinica))}
        className="grid h-8 w-8 place-items-center rounded-full transition hover:bg-white disabled:opacity-30"
      >
        <Minus className="h-3.5 w-3.5" />
      </button>
      <input
        type="number"
        inputMode="decimal"
        step={k}
        min={k}
        value={red.kolicina}
        aria-label={`Količina ${red.naziv}`}
        onChange={(e) => {
          const n = Number(e.target.value.replace(",", "."));
          if (Number.isFinite(n) && n > 0) onPromijeni(naKorak(n, red.jedinica));
        }}
        className="w-12 bg-transparent text-center text-sm font-semibold tabular-nums outline-none [appearance:textfield] [&::-webkit-inner-spin-button]:appearance-none"
      />
      <span className="-ml-1 mr-1 text-xs text-muted">{red.jedinica}</span>
      <button
        type="button"
        aria-label={`Više ${red.naziv}`}
        onClick={() => onPromijeni(naKorak(red.kolicina + k, red.jedinica))}
        className="grid h-8 w-8 place-items-center rounded-full transition hover:bg-white"
      >
        <Plus className="h-3.5 w-3.5" />
      </button>
    </div>
  );
}

/** Pločice artikala za klik (dodavanje, zamjena, predložene zamjene), sa opcionom pretragom. */
export function Plocice({
  ponuda,
  opis,
  onIzaberi,
  pretraga = false,
  najvise = 18,
}: {
  ponuda: Product[];
  opis?: (p: Product) => string;
  onIzaberi: (p: Product) => void;
  pretraga?: boolean;
  najvise?: number;
}) {
  const [upit, setUpit] = useState("");
  const [sve, setSve] = useState(false);
  const minimalnaMarza = usePodesavanja().data?.minimalnaMarza;
  const q = bezKvacica(upit.trim());
  const filtrirano = q ? ponuda.filter((p) => bezKvacica(`${p.name} ${p.categoryName}`).includes(q)) : ponuda;
  const prikaz = sve || q ? filtrirano : filtrirano.slice(0, najvise);

  return (
    <div className="flex flex-col gap-2">
      {pretraga && (
        <label className="relative">
          <Search className="pointer-events-none absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-muted" />
          <input
            type="search"
            value={upit}
            onChange={(e) => setUpit(e.target.value)}
            placeholder="Traži artikal…"
            aria-label="Traži artikal za dodavanje"
            className="h-9 w-full rounded-full border border-line bg-white pl-8 pr-3 text-sm outline-none focus:border-brand-500"
          />
        </label>
      )}
      {prikaz.length === 0 ? (
        <p className="text-sm text-muted">{q ? `Ništa za „${upit}“.` : "Nema slobodnih artikala u sezoni."}</p>
      ) : (
        <div className="grid grid-cols-2 gap-1.5 sm:grid-cols-3">
          {prikaz.map((p) => (
            <button
              key={p.id}
              type="button"
              onClick={() => onIzaberi(p)}
              className="flex items-center gap-2 rounded-lg border border-line bg-white p-1.5 text-left transition hover:border-brand-500 hover:shadow-sm"
            >
              <span className="relative h-8 w-8 shrink-0 overflow-hidden rounded-md" style={{ backgroundColor: p.boja ?? "var(--color-sand)" }}>
                <SmartImage src={p.imageUrl} alt={p.name} sizes="32px" />
              </span>
              <span className="min-w-0">
                <span className="block truncate text-xs font-medium">{p.name}</span>
                <span className="block truncate text-[11px] text-muted">
                  {opis ? opis(p) : `${formatPrice(p.price)}${unitSuffix(p.unit)}`}
                  {p.marza != null && <span className={cn("ml-1", TON[tonMarze(p.marza, minimalnaMarza)])}>{formatMarza(p.marza)}</span>}
                </span>
              </span>
            </button>
          ))}
        </div>
      )}
      {!q && !sve && filtrirano.length > prikaz.length && (
        <button type="button" onClick={() => setSve(true)} className="self-start text-xs font-medium text-brand-700 hover:underline">
          Prikaži sve ({filtrirano.length})
        </button>
      )}
    </div>
  );
}
