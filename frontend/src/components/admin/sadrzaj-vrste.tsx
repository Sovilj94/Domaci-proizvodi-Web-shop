"use client";

import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ChevronLeft, ChevronRight, Pencil, RefreshCw, Sparkles, TriangleAlert } from "lucide-react";
import { isoSedmica } from "@/components/admin/izbor-sedmice";
import { SadrzajNacrtEditor } from "@/components/admin/sadrzaj-nacrt-editor";
import { StatusPregledaBadge } from "@/components/admin/status-sadrzaja";
import { Button } from "@/components/ui/button";
import { SmartImage } from "@/components/ui/smart-image";
import { LoadingBlock } from "@/components/ui/spinner";
import { useToast } from "@/components/ui/toast";
import { ApiRequestError } from "@/lib/api/client";
import type { RezultatGenerisanja, SadrzajPregled } from "@/lib/api/contracts";
import { adminKorpiceApi, korpiceApi } from "@/lib/api/endpoints";
import { queryKeys } from "@/lib/api/hooks";
import { formatMarza, tonMarze } from "@/lib/marza";
import { cn, formatPrice } from "@/lib/utils";
import type { Podesavanja, TipKorpice, VelicinaKorpice } from "@/types/domain";

const NAZIV_VELICINE: Record<VelicinaKorpice, string> = { mala: "Mala", srednja: "Srednja", velika: "Velika" };
const TON = { nema: "text-muted", losa: "text-red-700", tijesna: "text-honey-700", dobra: "text-brand-700" } as const;

function poruka(err: unknown, rezerva: string): string {
  return err instanceof ApiRequestError && err.message.trim() ? err.message : rezerva;
}

/**
 * Postavke korpica → „Sadržaj korpice": šta je u svakoj veličini ove vrste za sedmicu koja se
 * prodaje, i ulaz u editor. Sistem sastavlja sadržaj sam („Sastavi automatski" — sezona,
 * prioritet, marža), a admin ga ovdje mijenja ručno: zamjena artikla (⇄), količine, dodavanje.
 * Editor (`SadrzajNacrtEditor`) uživo javlja kad korpica prestane biti ekonomična; ovdje se isto
 * vidi na kartici, bez otvaranja.
 *
 * Sedmica se bira lokalno (bez `?datum=` u URL-u — stranica postavki nema Suspense oko
 * `useSearchParams`), počev od one koju checkout upravo prodaje.
 */
export function SadrzajVrste({ tip, podesavanja }: { tip: TipKorpice; podesavanja: Podesavanja }) {
  const qc = useQueryClient();
  const { show } = useToast();
  const [pomak, setPomak] = useState(0);
  const [editor, setEditor] = useState<{ sadrzajId: string; korpicaNaziv: string } | null>(null);
  const [upozorenja, setUpozorenja] = useState<RezultatGenerisanja[]>([]);

  const krug = useQuery({ queryKey: queryKeys.sedmica(), queryFn: () => korpiceApi.sedmica() });
  // Poslije roka checkout prodaje narednu sedmicu (SedmicaZaNarudzbu) — od nje se kreće.
  const sidro = useMemo(() => {
    if (!krug.data) return null;
    const d = new Date(krug.data.rokIstekao ? krug.data.sljedecaIsporuka : krug.data.datumIsporuke);
    d.setDate(d.getDate() + pomak * 7);
    return d;
  }, [krug.data, pomak]);
  const izbor = sidro ? isoSedmica(sidro) : null;

  const pregledKljuc = ["admin", "korpice", "pregled", izbor?.godina, izbor?.sedmica] as const;
  const pregled = useQuery({
    queryKey: pregledKljuc,
    queryFn: () => adminKorpiceApi.pregled(izbor!.godina, izbor!.sedmica),
    enabled: izbor !== null,
  });
  const redovi = (pregled.data ?? []).filter((r) => r.tip === tip);

  function osvjezi() {
    qc.invalidateQueries({ queryKey: ["admin", "korpice", "pregled"] });
    qc.invalidateQueries({ queryKey: ["admin", "korpice", "sadrzaj"] });
    qc.invalidateQueries({ queryKey: ["admin", "zadaci"] });
  }

  const generisi = useMutation({
    mutationFn: (v: { korpicaId: string; ponovo?: boolean }) =>
      adminKorpiceApi.generisi({ godina: izbor!.godina, sedmica: izbor!.sedmica, korpicaId: v.korpicaId, ponovo: v.ponovo }),
    onSuccess: (rezultati) => {
      osvjezi();
      const idevi = new Set(rezultati.map((r) => r.korpicaId));
      setUpozorenja((prije) => [
        ...prije.filter((u) => !idevi.has(u.korpicaId)),
        ...rezultati.filter((r) => r.upozorenje || r.bezNabavneCijene.length > 0),
      ]);
      show(rezultati.length === 0 ? "Nema šta da se sastavi — korpica nije u ponudi ili već ima sadržaj." : "Sadržaj sastavljen — pregledaj ga i objavi.");
    },
    onError: (err) => show(poruka(err, "Sastavljanje nije uspjelo"), "error"),
  });

  const objavi = useMutation({
    mutationFn: (id: string) => adminKorpiceApi.objavi(id),
    onSuccess: () => {
      osvjezi();
      qc.invalidateQueries({ queryKey: ["korpice"] });
      show("Sadržaj objavljen");
    },
    onError: (err) => show(poruka(err, "Objavljivanje nije uspjelo"), "error"),
  });

  const min: Record<VelicinaKorpice, number> = {
    mala: podesavanja.minArtikalaMala,
    srednja: podesavanja.minArtikalaSrednja,
    velika: podesavanja.minArtikalaVelika,
  };

  return (
    <section className="rounded-card bg-white p-4 shadow-card sm:p-5">
      <div className="mb-3 flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="font-display text-lg font-semibold">Sadržaj korpice</h2>
          <p className="max-w-2xl text-sm text-muted">
            Sistem sam sastavlja korpicu iz sezonske ponude. Klikni „Izmijeni sadržaj“ da zamijeniš artikal, promijeniš
            količinu ili dodaš novi — editor odmah javlja ako korpica prestane biti ekonomična.
          </p>
        </div>
        {izbor && (
          <div className="flex items-center gap-1.5">
            <button
              type="button"
              onClick={() => setPomak((p) => p - 1)}
              disabled={pomak <= 0}
              aria-label="Prethodna sedmica"
              className="grid h-8 w-8 place-items-center rounded-full border border-line text-muted transition hover:bg-sand hover:text-ink disabled:opacity-30"
            >
              <ChevronLeft className="h-4 w-4" />
            </button>
            <div className="w-32 text-center">
              <p className="text-sm font-semibold">
                Sedmica {izbor.sedmica} · {izbor.godina}
              </p>
              <p className="text-xs text-muted">{pomak === 0 ? "prodaje se sada" : pomak === 1 ? "naredna" : `za ${pomak} sedmice`}</p>
            </div>
            <button
              type="button"
              onClick={() => setPomak((p) => p + 1)}
              disabled={pomak >= 4}
              aria-label="Sljedeća sedmica"
              className="grid h-8 w-8 place-items-center rounded-full border border-line text-muted transition hover:bg-sand hover:text-ink disabled:opacity-30"
            >
              <ChevronRight className="h-4 w-4" />
            </button>
          </div>
        )}
      </div>

      {upozorenja.length > 0 && (
        <div className="mb-3 flex gap-2 rounded-xl border border-honey-300 bg-honey-100 p-3 text-sm text-honey-700">
          <TriangleAlert className="mt-0.5 h-4 w-4 shrink-0" />
          <ul className="space-y-1">
            {upozorenja.map((u) => (
              <li key={u.korpicaId}>
                <b>{u.korpicaNaziv}:</b> {u.upozorenje}
                {u.bezNabavneCijene.length > 0 && (
                  <span className="block">Bez nabavne cijene (marža nije provjerena): {u.bezNabavneCijene.join(", ")}.</span>
                )}
              </li>
            ))}
          </ul>
        </div>
      )}

      {krug.isLoading || pregled.isLoading || !izbor ? (
        krug.isError ? (
          <p className="text-sm text-red-700">Sedmica nije učitana — osvježi stranicu.</p>
        ) : (
          <LoadingBlock />
        )
      ) : pregled.isError ? (
        <p className="text-sm text-red-700">Sadržaj nije učitan — osvježi stranicu.</p>
      ) : redovi.length === 0 ? (
        <p className="text-sm text-muted">Nijedna veličina ove vrste nije u ponudi — uključi je iznad pod „Veličine i cijene“.</p>
      ) : (
        <ul className="grid gap-3 lg:grid-cols-3">
          {redovi.map((r) => (
            <Kartica
              key={r.korpicaId}
              red={r}
              minimalnaMarza={podesavanja.minimalnaMarza}
              minimalnoArtikala={min[r.velicina]}
              generise={generisi.isPending && generisi.variables?.korpicaId === r.korpicaId}
              objavljuje={objavi.isPending && objavi.variables === r.sadrzajId}
              zauzeto={generisi.isPending || objavi.isPending}
              onGenerisi={(ponovo) => generisi.mutate({ korpicaId: r.korpicaId, ponovo })}
              onObjavi={() => r.sadrzajId && objavi.mutate(r.sadrzajId)}
              onIzmijeni={() => r.sadrzajId && setEditor({ sadrzajId: r.sadrzajId, korpicaNaziv: r.korpicaNaziv })}
            />
          ))}
        </ul>
      )}

      {editor && (
        <SadrzajNacrtEditor sadrzajId={editor.sadrzajId} korpicaNaziv={editor.korpicaNaziv} onClose={() => setEditor(null)} />
      )}
    </section>
  );
}

function Kartica({
  red: r,
  minimalnaMarza,
  minimalnoArtikala,
  generise,
  objavljuje,
  zauzeto,
  onGenerisi,
  onObjavi,
  onIzmijeni,
}: {
  red: SadrzajPregled;
  minimalnaMarza: number;
  minimalnoArtikala: number;
  generise: boolean;
  objavljuje: boolean;
  zauzeto: boolean;
  onGenerisi: (ponovo?: boolean) => void;
  onObjavi: () => void;
  onIzmijeni: () => void;
}) {
  const sadrzaj = useQuery({
    queryKey: ["admin", "korpice", "sadrzaj", r.sadrzajId],
    queryFn: () => adminKorpiceApi.sadrzaj(r.sadrzajId!),
    enabled: !!r.sadrzajId,
  });

  const ima = r.status !== "Nema";
  const mijenjaSe = r.status === "Nacrt" || r.status === "Objavljen";
  const slabaMarza = ima && r.marza != null && r.marza < minimalnaMarza;
  const maloKultura = ima && r.brojArtikala < minimalnoArtikala;

  return (
    <li className={cn("flex flex-col rounded-xl border p-3", slabaMarza || maloKultura ? "border-red-200" : "border-line")}>
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <p className="truncate font-medium">{NAZIV_VELICINE[r.velicina]}</p>
          <p className="truncate text-xs text-muted">
            {r.korpicaNaziv} · {formatPrice(r.cijenaKorpice)}
          </p>
        </div>
        <StatusPregledaBadge status={r.status} />
      </div>

      {ima && (
        <p className="mt-2 text-xs text-muted">
          Vrijedi <b className="text-ink">{formatPrice(r.vrijednost)}</b> / {formatPrice(r.ciljGenerisanja)} · marža{" "}
          <b className={TON[tonMarze(r.marza, minimalnaMarza)]}>{formatMarza(r.marza)}</b>
          {r.bezNabavneCijene > 0 && ` · ${r.bezNabavneCijene} bez nabavne`}
        </p>
      )}

      {(slabaMarza || maloKultura) && (
        <p className="mt-2 flex gap-1.5 rounded-lg bg-red-50 p-2 text-xs font-medium text-red-800">
          <TriangleAlert className="mt-0.5 h-3.5 w-3.5 shrink-0" />
          <span>
            Nije ekonomična:
            {slabaMarza && ` marža ${formatMarza(r.marza)} je ispod najmanje ${formatMarza(minimalnaMarza)}`}
            {slabaMarza && maloKultura && ";"}
            {maloKultura && ` ${r.brojArtikala} kultura, a najmanje je ${minimalnoArtikala}`}.
          </span>
        </p>
      )}

      <div className="mt-3 flex-1">
        {!ima ? (
          <p className="text-sm text-muted">Za ovu sedmicu još nema sadržaja.</p>
        ) : sadrzaj.isLoading ? (
          <p className="text-sm text-muted">Učitavam sadržaj…</p>
        ) : sadrzaj.isError || !sadrzaj.data ? (
          <p className="text-sm text-red-700">Sadržaj nije učitan.</p>
        ) : (
          <ul className="flex flex-col gap-1">
            {sadrzaj.data.stavke.map((s) => (
              <li key={s.productId} className="flex items-center gap-2 text-sm">
                <span className="relative h-7 w-7 shrink-0 overflow-hidden rounded-md" style={{ backgroundColor: s.boja ?? "var(--color-sand)" }}>
                  <SmartImage src={s.imageUrl} alt={s.naziv} sizes="28px" />
                </span>
                <span className="min-w-0 flex-1 truncate">{s.naziv}</span>
                <span className="shrink-0 text-xs tabular-nums text-muted">
                  {s.kolicina.toLocaleString("sr-Latn-BA")} {s.jedinica}
                </span>
              </li>
            ))}
          </ul>
        )}
      </div>

      <div className="mt-3 flex flex-wrap gap-2">
        {!ima && (
          <Button size="sm" onClick={() => onGenerisi()} disabled={zauzeto}>
            <Sparkles className="h-4 w-4" /> {generise ? "Sastavljam…" : "Sastavi automatski"}
          </Button>
        )}
        {ima && (
          <Button size="sm" variant={mijenjaSe ? "primary" : "secondary"} onClick={onIzmijeni}>
            {mijenjaSe && <Pencil className="h-4 w-4" />} {mijenjaSe ? "Izmijeni sadržaj" : "Pregled"}
          </Button>
        )}
        {r.status === "Nacrt" && (
          <>
            <Button
              size="sm"
              variant="ghost"
              title="Obriši ovaj nacrt i sastavi ga ponovo po trenutnim cijenama, nabavnim cijenama i sezoni"
              onClick={() => onGenerisi(true)}
              disabled={zauzeto}
            >
              <RefreshCw className={cn("h-4 w-4", generise && "animate-spin")} /> Sastavi ponovo
            </Button>
            <Button size="sm" variant="secondary" onClick={onObjavi} disabled={zauzeto}>
              {objavljuje ? "Objavljujem…" : "Objavi"}
            </Button>
          </>
        )}
      </div>
    </li>
  );
}
