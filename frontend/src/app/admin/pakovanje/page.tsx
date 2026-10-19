"use client";

import { Suspense, useMemo, useState } from "react";
import Link from "next/link";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Check, Printer } from "lucide-react";
import { IzborSedmice, useIzabranaSedmica } from "@/components/admin/izbor-sedmice";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { LoadingBlock } from "@/components/ui/spinner";
import { useToast } from "@/components/ui/toast";
import { ApiRequestError } from "@/lib/api/client";
import { adminApi } from "@/lib/api/endpoints";
import { STATUS_META } from "@/lib/orders";
import { cn, formatPrice, weekdayName } from "@/lib/utils";
import type { OrderStatus, PakovanjeNarudzba, PakovanjeStavka } from "@/types/domain";

/**
 * Pakovanje sedmice: šta tačno ide u svaku kutiju. Sadržaj korpice je onaj koji je kupac
 * ostavio (artikli koje je sam dodao su označeni), plus dodaci iz Špajza. „Spakovano" prebacuje
 * narudžbu u status Pakovanje — odatle je vozač preuzima. Zbir za nabavku korpica je na ekranu
 * Korpice (pakerska lista); ovdje je i zbir Špajz dodataka, koji ta lista ne sabira.
 */
export default function AdminPakovanjePage() {
  return (
    <Suspense fallback={<LoadingBlock />}>
      <Pakovanje />
    </Suspense>
  );
}

const FILTERI: { kljuc: "sve" | "zaPakovanje" | "spakovano"; naziv: string }[] = [
  { kljuc: "zaPakovanje", naziv: "Za pakovanje" },
  { kljuc: "spakovano", naziv: "Spakovano" },
  { kljuc: "sve", naziv: "Sve" },
];

const ZA_PAKOVANJE: OrderStatus[] = ["Pending", "Confirmed"];

function kol(s: PakovanjeStavka) {
  return `${Number(s.kolicina.toFixed(2))} ${s.jedinica}`;
}

function Pakovanje() {
  const izbor = useIzabranaSedmica();
  const { godina, sedmica } = izbor;
  const qc = useQueryClient();
  const { show } = useToast();
  const [filter, setFilter] = useState<(typeof FILTERI)[number]["kljuc"]>("zaPakovanje");
  const [dan, setDan] = useState<number | "svi">("svi");

  const kljuc = ["admin", "pakovanje", godina, sedmica] as const;
  const q = useQuery({ queryKey: kljuc, queryFn: () => adminApi.pakovanje(godina, sedmica) });

  const status = useMutation({
    mutationFn: async ({ n, cilj }: { n: PakovanjeNarudzba; cilj: OrderStatus }) => {
      // Narudžba na čekanju se prvo potvrdi — prelaz Pending → Packing ne postoji.
      if (cilj === "Packing" && n.status === "Pending") await adminApi.updateOrderStatus(n.id, "Confirmed");
      return adminApi.updateOrderStatus(n.id, cilj);
    },
    onSuccess: (_d, { n, cilj }) => {
      qc.invalidateQueries({ queryKey: kljuc });
      qc.invalidateQueries({ queryKey: ["admin", "orders"] });
      qc.invalidateQueries({ queryKey: ["admin", "dashboard"] });
      show(cilj === "Packing" ? `${n.code} je spakovana` : `${n.code}: ${STATUS_META[cilj].label}`);
    },
    onError: (err) => {
      qc.invalidateQueries({ queryKey: kljuc });
      show(err instanceof ApiRequestError ? err.message : "Status nije promijenjen", "error");
    },
  });

  const dani = useMemo(
    () => [...new Set((q.data?.narudzbe ?? []).map((n) => n.danIsporuke).filter((d): d is number => d != null))],
    [q.data],
  );

  const prikazane = (q.data?.narudzbe ?? []).filter(
    (n) =>
      (dan === "svi" || n.danIsporuke === dan) &&
      (filter === "sve" ||
        (filter === "zaPakovanje" ? ZA_PAKOVANJE.includes(n.status) : !ZA_PAKOVANJE.includes(n.status))),
  );
  const brojZaPakovanje = (q.data?.narudzbe ?? []).filter((n) => ZA_PAKOVANJE.includes(n.status)).length;

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="font-display text-2xl font-bold text-ink">Pakovanje</h1>
          <p className="text-sm text-muted">
            Šta ide u svaku kutiju ove sedmice.{" "}
            <Link
              href={`/admin/korpice${izbor.datumParam ? `?datum=${izbor.datumParam}` : ""}`}
              className="font-medium text-brand-700 hover:underline print:hidden"
            >
              Zbir za nabavku (pakerska lista) →
            </Link>
          </p>
        </div>
        <div className="flex items-center gap-2 print:hidden">
          <IzborSedmice s={izbor} />
          <Button variant="secondary" size="sm" onClick={() => window.print()}>
            <Printer className="h-4 w-4" /> Štampaj
          </Button>
        </div>
      </div>

      {q.data && (
        <div className="flex flex-wrap items-center gap-2 print:hidden">
          {FILTERI.map((f) => (
            <button
              key={f.kljuc}
              type="button"
              onClick={() => setFilter(f.kljuc)}
              className={cn(
                "rounded-full px-4 py-1.5 text-sm font-medium",
                filter === f.kljuc ? "bg-brand-600 text-white" : "bg-surface text-ink ring-1 ring-line",
              )}
            >
              {f.naziv}
              {f.kljuc === "zaPakovanje" && brojZaPakovanje > 0 && ` (${brojZaPakovanje})`}
            </button>
          ))}
          {dani.length > 1 && (
            <span className="ml-2 flex gap-1">
              {(["svi", ...dani] as const).map((d) => (
                <button
                  key={d}
                  type="button"
                  onClick={() => setDan(d)}
                  className={cn(
                    "rounded-full px-3 py-1.5 text-sm",
                    dan === d ? "bg-ink text-white" : "text-ink ring-1 ring-line",
                  )}
                >
                  {d === "svi" ? "Svi dani" : weekdayName(d)}
                </button>
              ))}
            </span>
          )}
        </div>
      )}

      {q.isLoading ? (
        <LoadingBlock />
      ) : q.isError ? (
        <EmptyState title="Pakovanje nije učitano" description="Pokušaj ponovo malo kasnije." />
      ) : !prikazane.length ? (
        <EmptyState
          title={q.data?.narudzbe.length ? "Nema narudžbi za ovaj filter" : "Nema narudžbi za ovu sedmicu"}
          description={q.data?.narudzbe.length ? "Promijeni filter iznad." : "Narudžbe za izabranu sedmicu pojaviće se ovdje."}
        />
      ) : (
        <div className="grid gap-4 lg:grid-cols-2 print:block">
          {prikazane.map((n) => (
            <Kutija
              key={n.id}
              n={n}
              zauzeto={status.isPending && status.variables?.n.id === n.id}
              onStatus={(cilj) => status.mutate({ n, cilj })}
            />
          ))}
        </div>
      )}

      {!!q.data?.zbirDodataka.length && (
        <section className="rounded-card border border-line bg-surface p-5 print:break-before-page">
          <h2 className="font-display text-lg font-bold text-ink">Dodaci iz Špajza — ukupno za sedmicu</h2>
          <p className="text-sm text-muted">Nisu dio sadržaja korpica, pa ih pakerska lista ne sabira.</p>
          <ul className="mt-3 divide-y divide-line text-sm">
            {q.data.zbirDodataka.map((s) => (
              <li key={s.naziv} className="flex justify-between py-1.5">
                <span>{s.naziv}</span>
                <span className="font-semibold tabular-nums">{kol(s)}</span>
              </li>
            ))}
          </ul>
        </section>
      )}
    </div>
  );
}

function Kutija({
  n,
  zauzeto,
  onStatus,
}: {
  n: PakovanjeNarudzba;
  zauzeto: boolean;
  onStatus: (cilj: OrderStatus) => void;
}) {
  const meta = STATUS_META[n.status];
  const zaPakovanje = ZA_PAKOVANJE.includes(n.status);

  return (
    <article className="break-inside-avoid rounded-card border border-line bg-surface p-5 print:mb-4 print:border-black">
      <header className="flex flex-wrap items-start justify-between gap-2">
        <div>
          <p className="font-display text-lg font-bold text-ink">
            {n.code} {n.pretplatna && <span className="text-xs font-medium text-brand-700">· pretplata</span>}
          </p>
          <p className="text-sm font-medium text-ink">{n.kupac}</p>
          <p className="text-sm text-muted">
            {n.adresa}
            {n.zona && ` · ${n.zona}`}
          </p>
          <p className="text-sm text-muted">
            {n.danIsporuke != null && weekdayName(n.danIsporuke)} {n.termin}
            {n.telefon && ` · ${n.telefon}`} · {formatPrice(n.ukupno)} pouzećem
          </p>
        </div>
        <span className={cn("rounded-full px-2.5 py-1 text-xs font-semibold", meta.badge)}>
          {n.status === "Packing" ? "Spakovano" : meta.label}
        </span>
      </header>

      {n.napomena && (
        <p className="mt-3 rounded-lg bg-honey-100 px-3 py-2 text-sm text-honey-700">Napomena: {n.napomena}</p>
      )}

      {n.korpice.map((k, i) => (
        <div key={i} className="mt-4">
          <p className="text-sm font-semibold text-ink">
            {k.naziv}
            {k.komada > 1 && ` × ${k.komada}`}
          </p>
          {k.sadrzaj.length === 0 ? (
            <p className="text-sm text-red-700">Sadržaj korpice još nije upisan — objavi sadržaj sedmice.</p>
          ) : (
            <ul className="mt-1 space-y-1 text-sm">
              {k.sadrzaj.map((s) => (
                <Red key={s.naziv} s={s} />
              ))}
            </ul>
          )}
        </div>
      ))}

      {n.dodaci.length > 0 && (
        <div className="mt-4">
          <p className="text-sm font-semibold text-ink">Dodaci iz Špajza</p>
          <ul className="mt-1 space-y-1 text-sm">
            {n.dodaci.map((s) => (
              <Red key={s.naziv} s={s} />
            ))}
          </ul>
        </div>
      )}

      <footer className="mt-4 flex flex-wrap gap-2 print:hidden">
        {zaPakovanje && (
          <Button size="sm" disabled={zauzeto} onClick={() => onStatus("Packing")}>
            <Check className="h-4 w-4" /> {zauzeto ? "Čuvam…" : "Spakovano"}
          </Button>
        )}
        {n.status === "Packing" && (
          <Button size="sm" variant="secondary" disabled={zauzeto} onClick={() => onStatus("OutForDelivery")}>
            Predato vozaču
          </Button>
        )}
      </footer>
    </article>
  );
}

function Red({ s }: { s: PakovanjeStavka }) {
  return (
    <li className="flex items-center justify-between gap-3">
      <label className="flex items-center gap-2">
        <input type="checkbox" className="h-4 w-4 accent-brand-600 print:hidden" />
        <span className="hidden h-3.5 w-3.5 border border-black print:inline-block" aria-hidden />
        <span>
          {s.naziv}
          {s.dodaoKupac && <span className="ml-1 text-xs font-medium text-honey-600">(izbor kupca)</span>}
        </span>
      </label>
      <span className="shrink-0 font-semibold tabular-nums">{kol(s)}</span>
    </li>
  );
}
