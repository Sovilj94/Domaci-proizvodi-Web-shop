"use client";

import { Suspense, useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Printer, RefreshCw, TriangleAlert } from "lucide-react";
import { Button } from "@/components/ui/button";
import { LoadingBlock } from "@/components/ui/spinner";
import { EmptyState } from "@/components/ui/empty-state";
import { useToast } from "@/components/ui/toast";
import { SadrzajNacrtEditor } from "@/components/admin/sadrzaj-nacrt-editor";
import { IzborSedmice, useIzabranaSedmica } from "@/components/admin/izbor-sedmice";
import { NAZIVI as NAZIVI_TIPA } from "@/components/korpice/tipovi-tabovi";
import { adminKorpiceApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import type { RezultatGenerisanja, StatusPregleda } from "@/lib/api/contracts";
import type { VelicinaKorpice } from "@/types/domain";
import { narudzbeRijec } from "@/lib/orders";
import { cn, formatPrice } from "@/lib/utils";

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
  const izbor = useIzabranaSedmica();
  const { godina, sedmica } = izbor;


  const [editor, setEditor] = useState<{ sadrzajId: string; korpicaNaziv: string; cilj: number } | null>(
    null,
  );
  const [upozorenja, setUpozorenja] = useState<RezultatGenerisanja[]>([]);

  // Upozorenja iz generisanja su samo odgovor POST-a (SadrzajPregled ih ne nosi), pa nemaju
  // smisla poslije promjene sedmice — gube se čim operater ode na drugu sedmicu.
  useEffect(() => {
    setUpozorenja([]);
  }, [godina, sedmica]);


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

        <IzborSedmice s={izbor} />
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
