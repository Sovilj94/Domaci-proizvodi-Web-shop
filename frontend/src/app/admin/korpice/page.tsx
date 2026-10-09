"use client";

import { Suspense, useEffect, useMemo, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ChevronLeft, ChevronRight, Printer, RefreshCw, TriangleAlert } from "lucide-react";
import { Button } from "@/components/ui/button";
import { LoadingBlock } from "@/components/ui/spinner";
import { EmptyState } from "@/components/ui/empty-state";
import { useToast } from "@/components/ui/toast";
import { SadrzajNacrtEditor } from "@/components/admin/sadrzaj-nacrt-editor";
import { NAZIVI as NAZIVI_TIPA } from "@/components/korpice/tipovi-tabovi";
import { adminKorpiceApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import type { RezultatGenerisanja, StatusPregleda } from "@/lib/api/contracts";
import type { VelicinaKorpice } from "@/types/domain";
import { narudzbeRijec } from "@/lib/orders";
import { cn, formatDate, formatPrice } from "@/lib/utils";

/** ISO broj sedmice za dati datum — isti račun kao na backendu (ISO-8601, četvrtak određuje sedmicu). */
function isoSedmica(d: Date): { godina: number; sedmica: number } {
  const t = new Date(Date.UTC(d.getFullYear(), d.getMonth(), d.getDate()));
  t.setUTCDate(t.getUTCDate() + 4 - (t.getUTCDay() || 7));
  const prvi = new Date(Date.UTC(t.getUTCFullYear(), 0, 1));
  const sedmica = Math.ceil(((t.getTime() - prvi.getTime()) / 86400000 + 1) / 7);
  return { godina: t.getUTCFullYear(), sedmica };
}

/** Ponedjeljak–nedjelja te ISO sedmice, samo za prikaz raspona datuma u zaglavlju. */
function raspon(d: Date): { od: Date; do: Date } {
  const cetvrtak = new Date(Date.UTC(d.getFullYear(), d.getMonth(), d.getDate()));
  cetvrtak.setUTCDate(cetvrtak.getUTCDate() + 4 - (cetvrtak.getUTCDay() || 7));
  const od = new Date(cetvrtak);
  od.setUTCDate(cetvrtak.getUTCDate() - 3);
  const doDatum = new Date(cetvrtak);
  doDatum.setUTCDate(cetvrtak.getUTCDate() + 3);
  return { od, do: doDatum };
}

const VELICINA_LABEL: Record<VelicinaKorpice, string> = {
  mala: "Mala",
  srednja: "Srednja",
  velika: "Velika",
};

const STATUS_META: Record<StatusPregleda, { label: string; badge: string; dot: string }> = {
  Nema: { label: "Nema sadržaja", badge: "bg-zinc-200 text-zinc-600", dot: "bg-zinc-400" },
  Nacrt: { label: "Nacrt", badge: "bg-honey-100 text-honey-600", dot: "bg-honey-500" },
  Objavljen: { label: "Objavljeno", badge: "bg-brand-100 text-brand-700", dot: "bg-brand-500" },
  Zakljucan: { label: "Zaključano", badge: "bg-indigo-100 text-indigo-700", dot: "bg-indigo-500" },
};

function StatusPregledaBadge({ status }: { status: StatusPregleda }) {
  const meta = STATUS_META[status];
  return (
    <span
      className={cn(
        "inline-flex shrink-0 items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-semibold",
        meta.badge,
      )}
    >
      <span className={cn("h-1.5 w-1.5 rounded-full", meta.dot)} />
      {meta.label}
    </span>
  );
}

/**
 * Srpska deklinacija uz broj artikala: 1 artikal, 2–4 artikla, 5+ artikala.
 * Isto pravilo kao `narudzbeRijec` u `lib/orders.ts`.
 */
function artikalaRijec(n: number): string {
  const jedinice = n % 10;
  const desetice = n % 100;
  if (jedinice === 1 && desetice !== 11) return "artikal";
  if (jedinice >= 2 && jedinice <= 4 && (desetice < 12 || desetice > 14)) return "artikla";
  return "artikala";
}

/** Poruka iz ApiRequestError ako je ima; inače rezerva. */
function poruka(err: unknown, rezerva: string): string {
  return err instanceof ApiRequestError && err.message.trim() ? err.message : rezerva;
}

// Pre-existing gap, not introduced by this screen: useSearchParams() opts the page out of
// static generation without a Suspense boundary below it — isti obrazac kao /admin/dostava.
export default function AdminKorpicePage() {
  return (
    <Suspense fallback={<LoadingBlock />}>
      <AdminKorpiceSadrzaj />
    </Suspense>
  );
}

function AdminKorpiceSadrzaj() {
  const qc = useQueryClient();
  const { show } = useToast();
  const router = useRouter();
  const params = useSearchParams();

  // Izabrana sedmica živi u URL-u (?datum=) kao sidro — bilo koji dan te ISO sedmice radi,
  // "Sljedeća/Prethodna" samo pomjeraju sidro za 7 dana, bez ručnog računanja prelaska
  // godine/broja sedmice (to već radi `isoSedmica`, isto kao backend).
  const anchor = useMemo(() => {
    const raw = params.get("datum");
    const d = raw ? new Date(`${raw}T00:00:00Z`) : null;
    return d && !Number.isNaN(d.getTime()) ? d : new Date();
  }, [params]);

  const { godina, sedmica } = useMemo(() => isoSedmica(anchor), [anchor]);
  const { od, do: doDatum } = useMemo(() => raspon(anchor), [anchor]);

  const [editor, setEditor] = useState<{ sadrzajId: string; korpicaNaziv: string; cilj: number } | null>(
    null,
  );
  const [upozorenja, setUpozorenja] = useState<RezultatGenerisanja[]>([]);

  // Upozorenja iz generisanja su samo odgovor POST-a (SadrzajPregled ih ne nosi), pa nemaju
  // smisla poslije promjene sedmice — gube se čim operater ode na drugu sedmicu.
  useEffect(() => {
    setUpozorenja([]);
  }, [godina, sedmica]);

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

  const pregledKljuc = ["admin", "korpice", "pregled", godina, sedmica] as const;
  const pakerskaKljuc = ["admin", "korpice", "pakerska", godina, sedmica] as const;

  const pregled = useQuery({
    queryKey: pregledKljuc,
    queryFn: () => adminKorpiceApi.pregled(godina, sedmica),
  });

  const pakerska = useQuery({
    queryKey: pakerskaKljuc,
    queryFn: () => adminKorpiceApi.pakerskaLista(godina, sedmica),
  });

  /** Zamjenjuje upozorenja za korpice koje su upravo (re)generisane; ostala ostaju. */
  function primijeniRezultate(rezultati: RezultatGenerisanja[]) {
    const idevi = new Set(rezultati.map((r) => r.korpicaId));
    setUpozorenja((prije) => [
      ...prije.filter((u) => !idevi.has(u.korpicaId)),
      ...rezultati.filter((r) => r.upozorenje),
    ]);
  }

  // Jedna mutacija za "generiši cijelu sedmicu" (korpicaId izostavljen) i "generiši samo
  // ovu korpicu" (korpicaId postavljen) — isti endpoint, `undefined` se ne serijalizuje
  // u JSON-u, pa tijelo ispadne identično ručno pisanom zahtjevu bez tog polja.
  const generisi = useMutation({
    mutationFn: (korpicaId?: string) => adminKorpiceApi.generisi({ godina, sedmica, korpicaId }),
    onSuccess: (rezultati) => {
      qc.invalidateQueries({ queryKey: pregledKljuc });
      primijeniRezultate(rezultati);
      // Backend vrati [] kad nema šta da generiše (korpica nedostupna, ili već ima
      // sadržaj za ovu sedmicu) — bez ove poruke dugme naizgled ne radi ništa, operater
      // ne zna da li je zahtjev uopšte otišao (vidi I4, finalni review).
      if (rezultati.length === 0) {
        show("Nema šta da se generiše — korpica je nedostupna ili već ima sadržaj za ovu sedmicu.");
      }
    },
    onError: (err) => show(poruka(err, "Generisanje nije uspjelo"), "error"),
  });

  const objavi = useMutation({
    mutationFn: (id: string) => adminKorpiceApi.objavi(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: pregledKljuc });
      qc.invalidateQueries({ queryKey: pakerskaKljuc });
      show("Sadržaj objavljen");
    },
    onError: (err) => show(poruka(err, "Objavljivanje nije uspjelo"), "error"),
  });

  const ucitava = pregled.isLoading;

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="font-display text-2xl font-bold text-ink">Korpice</h1>
          <p className="mt-1 text-sm text-muted">
            Generiši, uredi i objavi sadržaj korpica za sedmicu, pa odštampaj pakersku listu.
          </p>
        </div>

        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={() => pomjeri(-7)}
            aria-label="Prethodna sedmica"
            className="grid h-9 w-9 shrink-0 place-items-center rounded-full border border-line text-muted transition hover:bg-sand hover:text-ink"
          >
            <ChevronLeft className="h-4 w-4" />
          </button>
          <div className="w-36 text-center">
            <p className="text-sm font-semibold text-ink">
              Sedmica {sedmica} · {godina}
            </p>
            <p className="text-xs text-muted">
              {formatDate(od.toISOString())} – {formatDate(doDatum.toISOString())}
            </p>
          </div>
          <button
            type="button"
            onClick={() => pomjeri(7)}
            aria-label="Sljedeća sedmica"
            className="grid h-9 w-9 shrink-0 place-items-center rounded-full border border-line text-muted transition hover:bg-sand hover:text-ink"
          >
            <ChevronRight className="h-4 w-4" />
          </button>
          <Button variant="secondary" size="sm" onClick={idiNaDanas}>
            Danas
          </Button>
        </div>
      </div>

      {upozorenja.length > 0 && (
        <div className="flex gap-3 rounded-card border border-honey-300 bg-honey-100 p-4 text-sm text-honey-700">
          <TriangleAlert className="h-5 w-5 shrink-0 text-honey-600" />
          <ul className="space-y-1">
            {upozorenja.map((u) => (
              <li key={u.korpicaId}>
                <strong>{u.korpicaNaziv}:</strong> {u.upozorenje}
              </li>
            ))}
          </ul>
        </div>
      )}

      <section className="space-y-3">
        <div className="flex items-center justify-between">
          <h2 className="font-display text-lg font-bold text-ink">Sadržaj po korpici</h2>
          <Button
            size="sm"
            onClick={() => generisi.mutate(undefined)}
            disabled={generisi.isPending}
          >
            <RefreshCw className={cn("h-4 w-4", generisi.isPending && "animate-spin")} />
            {generisi.isPending && generisi.variables === undefined
              ? "Generišem…"
              : "Generiši nacrte za sedmicu"}
          </Button>
        </div>

        {ucitava ? (
          <LoadingBlock />
        ) : !pregled.data || pregled.data.length === 0 ? (
          <EmptyState
            title="Nema korpica"
            description="Dodajte korpice u katalog da bi se ovdje prikazao sadržaj po sedmici."
          />
        ) : (
          <div className="overflow-hidden rounded-card bg-white shadow-card">
            <ul className="divide-y divide-line">
              {pregled.data.map((r) => (
                <li key={r.korpicaId} className="flex flex-wrap items-center gap-4 p-3">
                  <div className="min-w-0 flex-1">
                    <p className="truncate font-medium text-ink">{r.korpicaNaziv}</p>
                    <p className="truncate text-xs text-muted">
                      {NAZIVI_TIPA[r.tip].label} · {VELICINA_LABEL[r.velicina]}
                    </p>
                  </div>

                  <StatusPregledaBadge status={r.status} />

                  <div className="w-32 shrink-0 text-right">
                    <p className="text-sm font-semibold text-ink">
                      {r.brojArtikala} {artikalaRijec(r.brojArtikala)}
                    </p>
                    <p
                      className={cn(
                        "text-xs",
                        r.vrijednost > r.ciljGenerisanja * 1.05 ? "font-medium text-red-600" : "text-muted",
                      )}
                    >
                      {formatPrice(r.vrijednost)} / {formatPrice(r.ciljGenerisanja)}
                    </p>
                  </div>

                  <div className="flex shrink-0 items-center gap-2">
                    {r.status === "Nema" && (
                      <Button
                        size="sm"
                        variant="secondary"
                        onClick={() => generisi.mutate(r.korpicaId)}
                        disabled={generisi.isPending}
                      >
                        {generisi.isPending && generisi.variables === r.korpicaId
                          ? "Generišem…"
                          : "Generiši"}
                      </Button>
                    )}

                    {r.status === "Nacrt" && r.sadrzajId && (
                      <>
                        <Button
                          size="sm"
                          variant="secondary"
                          onClick={() =>
                            setEditor({
                              sadrzajId: r.sadrzajId!,
                              korpicaNaziv: r.korpicaNaziv,
                              cilj: r.ciljGenerisanja,
                            })
                          }
                        >
                          Izmijeni
                        </Button>
                        <Button
                          size="sm"
                          onClick={() => objavi.mutate(r.sadrzajId!)}
                          disabled={objavi.isPending}
                        >
                          {objavi.isPending && objavi.variables === r.sadrzajId
                            ? "Objavljujem…"
                            : "Objavi"}
                        </Button>
                      </>
                    )}

                    {(r.status === "Objavljen" || r.status === "Zakljucan") && (
                      <span className="px-2 text-xs text-muted">—</span>
                    )}
                  </div>
                </li>
              ))}
            </ul>
          </div>
        )}
      </section>

      <section className="space-y-3">
        <div className="flex items-center justify-between">
          <h2 className="font-display text-lg font-bold text-ink">Pakerska lista</h2>
          {pakerska.data && pakerska.data.stavke.length > 0 && (
            <Button
              variant="secondary"
              size="sm"
              className="print:hidden"
              onClick={() => window.print()}
            >
              <Printer className="h-4 w-4" /> Štampaj
            </Button>
          )}
        </div>

        {pakerska.isLoading ? (
          <LoadingBlock />
        ) : pakerska.isError ? (
          <EmptyState
            title="Ne mogu da učitam pakersku listu"
            description="Provjeri konekciju pa pokušaj ponovo."
            action={<Button onClick={() => pakerska.refetch()}>Pokušaj ponovo</Button>}
          />
        ) : !pakerska.data || pakerska.data.stavke.length === 0 ? (
          <>
            <p className="text-sm text-muted">
              {pakerska.data?.brojNarudzbi === 0
                ? "Za ovu sedmicu još nema nijedne narudžbe."
                : `${pakerska.data?.brojNarudzbi} ${narudzbeRijec(pakerska.data?.brojNarudzbi ?? 0)} za ovu sedmicu.`}
            </p>
            <EmptyState
              title="Pakerska lista je prazna"
              description="Nema narudžbi za ovu sedmicu, pa nema ni šta da se pakuje."
            />
          </>
        ) : (
          <>
            <p className="text-sm text-muted">
              {pakerska.data.brojNarudzbi} {narudzbeRijec(pakerska.data.brojNarudzbi)} za ovu sedmicu.
            </p>
            <div className="overflow-hidden rounded-card bg-white shadow-card">
              <ul className="divide-y divide-line">
                {[...pakerska.data.stavke]
                  .sort((a, b) => a.artikal.localeCompare(b.artikal, "sr"))
                  .map((r) => (
                    <li
                      key={`${r.artikal}-${r.proizvodjac}-${r.selo}`}
                      className="flex items-center gap-4 p-3"
                    >
                      <div className="min-w-0 flex-1">
                        <p className="truncate font-medium text-ink">
                          {r.artikal}
                          {r.artikalObrisan && (
                            <span className="ml-2 rounded-full bg-red-100 px-2 py-0.5 text-xs font-semibold text-red-700">
                              Obrisan artikal — provjeriti
                            </span>
                          )}
                        </p>
                        <p className="truncate text-xs text-muted">
                          {r.proizvodjac} · {r.selo}
                        </p>
                      </div>
                      <p className="shrink-0 text-sm font-semibold text-ink">
                        {r.kolicina} {r.jedinica}
                      </p>
                    </li>
                  ))}
              </ul>
            </div>
          </>
        )}
      </section>

      {editor && (
        <SadrzajNacrtEditor
          sadrzajId={editor.sadrzajId}
          korpicaNaziv={editor.korpicaNaziv}
          cilj={editor.cilj}
          onClose={() => setEditor(null)}
        />
      )}
    </div>
  );
}
