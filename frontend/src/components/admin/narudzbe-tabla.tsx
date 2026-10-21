"use client";

import { useState } from "react";
import { AlertTriangle, ArrowRight, CheckCheck, GripVertical, XCircle } from "lucide-react";
import { AdminLadica } from "@/components/admin/admin-ladica";
import { OrderCard } from "@/components/admin/order-card";
import { KOLONE, NAJVISE_U_ZAVRSENOJ, mozePreci, rasporedi, sljedeci } from "@/lib/admin/kanban";
import { STATUS_META, narudzbeRijec } from "@/lib/orders";
import type { Order, OrderStatus } from "@/types/domain";
import { cn, formatPrice } from "@/lib/utils";

/**
 * Narudžbe kao tabla: kolona po statusu, kartica se prevlači u sljedeću kolonu (samo tamo
 * gdje backend dozvoljava prelaz — dozvoljene kolone svijetle, ostale se priguše). Za tastaturu
 * i telefon isto radi dugme „→" na kartici. Otkazivanje je nepovratno, pa traži potvrdu.
 */
export function NarudzbeTabla({
  narudzbe,
  pending,
  onPromijeni,
  onPromijeniVise,
}: {
  narudzbe: Order[];
  pending: boolean;
  onPromijeni: (o: Order, status: OrderStatus) => void;
  /** Više narudžbi odjednom (Potvrdi sve) — jedan zahtjev po narudžbi, jedno obavještenje. */
  onPromijeniVise: (narudzbe: Order[], status: OrderStatus) => void;
}) {
  const t = rasporedi(narudzbe);
  const [vuce, setVuce] = useState<Order | null>(null);
  const [iznad, setIznad] = useState<OrderStatus | "Cancelled" | null>(null);
  const [otkazati, setOtkazati] = useState<Order | null>(null);
  const [otvorena, setOtvorena] = useState<Order | null>(null);
  // Panel prati svježu verziju narudžbe poslije promjene statusa.
  const uPanelu = otvorena ? (narudzbe.find((o) => o.id === otvorena.id) ?? otvorena) : null;

  function spusti(u: OrderStatus) {
    const o = vuce;
    setVuce(null);
    setIznad(null);
    if (!o || !mozePreci(o.status, u)) return;
    if (u === "Cancelled") setOtkazati(o);
    else onPromijeni(o, u);
  }

  function zona(u: OrderStatus) {
    const dozvoljena = !!vuce && mozePreci(vuce.status, u);
    return {
      onDragOver: (e: React.DragEvent) => {
        if (!dozvoljena) return;
        e.preventDefault();
        e.dataTransfer.dropEffect = "move";
        setIznad(u);
      },
      onDragLeave: () => setIznad((x) => (x === u ? null : x)),
      onDrop: (e: React.DragEvent) => {
        e.preventDefault();
        spusti(u);
      },
      dozvoljena,
    };
  }

  const otkaziZona = zona("Cancelled");

  return (
    <div className="space-y-3">
      {t.problemi.length > 0 && (
        <div className="rounded-card border border-red-200 bg-red-50 p-3">
          <p className="mb-2 flex items-center gap-2 text-sm font-semibold text-red-700">
            <AlertTriangle className="h-4 w-4" />
            Neuspješna dostava — vrati u dostavu ili otkaži
          </p>
          <div className="flex flex-wrap gap-2">
            {t.problemi.map((o) => (
              <Kartica
                key={o.id}
                o={o}
                pending={pending}
                onOtvori={() => setOtvorena(o)}
                onNaprijed={(s) => onPromijeni(o, s)}
                onVuci={setVuce}
                kompaktna
              />
            ))}
          </div>
        </div>
      )}

      {otkazati && (
        <div role="alertdialog" className="flex flex-wrap items-center gap-3 rounded-card bg-ink p-3 text-sm text-white">
          <XCircle className="h-4 w-4 text-red-300" />
          <span className="flex-1">
            Otkazati <b>{otkazati.code}</b> ({otkazati.customerName}, {formatPrice(otkazati.totalAmount)})? Ovo se ne može vratiti.
          </span>
          <button
            type="button"
            onClick={() => {
              onPromijeni(otkazati, "Cancelled");
              setOtkazati(null);
            }}
            className="rounded-full bg-red-500 px-3 py-1 font-semibold transition hover:bg-red-600"
          >
            Otkaži narudžbu
          </button>
          <button type="button" onClick={() => setOtkazati(null)} className="rounded-full px-3 py-1 transition hover:bg-white/10">
            Ne
          </button>
        </div>
      )}

      <div className="no-scrollbar -mx-4 flex gap-3 overflow-x-auto px-4 pb-2 lg:mx-0 lg:grid lg:grid-cols-5 lg:overflow-visible lg:px-0">
        {KOLONE.map((k) => {
          const z = zona(k);
          const sve = t.kolone[k];
          const prikaz = k === "Delivered" ? sve.slice(0, NAJVISE_U_ZAVRSENOJ) : sve;
          const zaPotvrdu = k === "Pending" ? sve : [];
          return (
            <section
              key={k}
              aria-label={STATUS_META[k].label}
              onDragOver={z.onDragOver}
              onDragLeave={z.onDragLeave}
              onDrop={z.onDrop}
              className={cn(
                "flex w-72 shrink-0 flex-col rounded-card bg-sand/70 p-2 transition lg:w-auto",
                vuce && !z.dozvoljena && vuce.status !== k && "opacity-40",
                z.dozvoljena && "ring-2 ring-brand-300",
                iznad === k && "bg-brand-50 ring-brand-500",
              )}
            >
              <header className="mb-2 flex items-center gap-2 px-1.5 pt-1">
                <span className={cn("h-2 w-2 rounded-full", STATUS_META[k].dot)} />
                <h2 className="whitespace-nowrap text-sm font-semibold">{STATUS_META[k].label}</h2>
                <span className="rounded-full bg-white px-1.5 text-xs font-semibold text-muted">{sve.length}</span>
                {zaPotvrdu.length > 1 && (
                  <button
                    type="button"
                    disabled={pending}
                    onClick={() => onPromijeniVise(zaPotvrdu, "Confirmed")}
                    title={`Potvrdi ${zaPotvrdu.length} ${narudzbeRijec(zaPotvrdu.length)}`}
                    aria-label={`Potvrdi sve: ${zaPotvrdu.length} ${narudzbeRijec(zaPotvrdu.length)}`}
                    className="ml-auto inline-flex items-center gap-1 rounded-full bg-white px-2 py-0.5 text-xs font-semibold text-brand-700 transition hover:bg-brand-50 disabled:opacity-50"
                  >
                    <CheckCheck className="h-3.5 w-3.5" /> Sve
                  </button>
                )}
              </header>
              <div className="flex min-h-24 flex-col gap-2">
                {prikaz.map((o) => (
                  <Kartica
                    key={o.id}
                    o={o}
                    pending={pending}
                    vuceSe={vuce?.id === o.id}
                    onOtvori={() => setOtvorena(o)}
                    onNaprijed={(s) => onPromijeni(o, s)}
                    onVuci={setVuce}
                  />
                ))}
                {sve.length === 0 && (
                  <p className="rounded-lg border border-dashed border-line px-3 py-6 text-center text-xs text-muted">
                    {z.dozvoljena ? "Spusti ovdje" : "Prazno"}
                  </p>
                )}
                {sve.length > prikaz.length && (
                  <p className="px-1.5 text-xs text-muted">i još {sve.length - prikaz.length} starijih — vidi listu</p>
                )}
              </div>
            </section>
          );
        })}
      </div>

      {/* Zona za otkazivanje se pojavi tek dok se vuče narudžba koja se smije otkazati. */}
      {vuce && otkaziZona.dozvoljena && (
        <div
          onDragOver={otkaziZona.onDragOver}
          onDragLeave={otkaziZona.onDragLeave}
          onDrop={otkaziZona.onDrop}
          className={cn(
            "flex items-center justify-center gap-2 rounded-card border-2 border-dashed border-red-300 p-4 text-sm font-semibold text-red-600 transition",
            iznad === "Cancelled" && "border-red-500 bg-red-50",
          )}
        >
          <XCircle className="h-4 w-4" /> Spusti ovdje da otkažeš
        </div>
      )}

      {t.otkazane > 0 && <p className="text-xs text-muted">Otkazanih: {t.otkazane} — vidi listu sa filterom „Otkazano“.</p>}

      {uPanelu && (
        <AdminLadica naslov={uPanelu.code} podnaslov={uPanelu.customerName} onClose={() => setOtvorena(null)}>
          <OrderCard
            order={uPanelu}
            expanded
            pending={pending}
            onToggle={() => setOtvorena(null)}
            onAdvance={(s) => onPromijeni(uPanelu, s)}
          />
        </AdminLadica>
      )}
    </div>
  );
}

function Kartica({
  o,
  pending,
  vuceSe,
  kompaktna,
  onOtvori,
  onNaprijed,
  onVuci,
}: {
  o: Order;
  pending: boolean;
  vuceSe?: boolean;
  kompaktna?: boolean;
  onOtvori: () => void;
  onNaprijed: (s: OrderStatus) => void;
  onVuci: (o: Order | null) => void;
}) {
  const dalje = sljedeci(o.status);
  const komada = o.items?.length ?? 0;
  return (
    <article
      draggable={!pending}
      onDragStart={(e) => {
        e.dataTransfer.effectAllowed = "move";
        e.dataTransfer.setData("text/plain", o.id);
        onVuci(o);
      }}
      onDragEnd={() => onVuci(null)}
      className={cn(
        "group cursor-grab rounded-xl bg-white p-2.5 shadow-sm ring-1 ring-black/5 transition hover:shadow-md active:cursor-grabbing",
        vuceSe && "opacity-40",
        kompaktna && "w-64",
      )}
    >
      <button type="button" onClick={onOtvori} className="block w-full text-left">
        <div className="flex items-start gap-1.5">
          <GripVertical className="mt-0.5 h-4 w-4 shrink-0 text-zinc-300 group-hover:text-zinc-400" />
          <div className="min-w-0 flex-1">
            <div className="flex items-baseline justify-between gap-2">
              <span className="font-display text-sm font-semibold">{o.code}</span>
              <span className="text-sm font-bold">{formatPrice(o.totalAmount)}</span>
            </div>
            <p className="truncate text-xs text-muted">{o.customerName}</p>
            <p className="truncate text-xs text-muted">
              {o.deliveryZoneName} · {komada} {komada === 1 ? "stavka" : komada >= 2 && komada <= 4 ? "stavke" : "stavki"}
              {o.deliverySlot?.label ? ` · ${o.deliverySlot.label}` : ""}
            </p>
            {o.note && <p className="mt-1 line-clamp-2 rounded bg-honey-100/60 px-1.5 py-0.5 text-[11px] text-honey-700">{o.note}</p>}
          </div>
        </div>
      </button>
      {dalje && (
        <button
          type="button"
          disabled={pending}
          onClick={() => onNaprijed(dalje)}
          className="mt-2 flex w-full items-center justify-center gap-1 rounded-lg bg-sand/70 py-1 text-xs font-semibold text-ink transition hover:bg-brand-600 hover:text-white disabled:opacity-50"
        >
          {STATUS_META[dalje].label} <ArrowRight className="h-3.5 w-3.5" />
        </button>
      )}
    </article>
  );
}
