"use client";

import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  Ban,
  Clock,
  Minus,
  Plus,
  RotateCcw,
  Sparkles,
  Trash2,
  X,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { LoadingBlock } from "@/components/ui/spinner";
import { SmartImage } from "@/components/ui/smart-image";
import { useToast } from "@/components/ui/toast";
import { queryKeys } from "@/lib/api/hooks";
import { accountApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import type { MojaKorpicaDetalji } from "@/lib/api/contracts";
import { cn, formatDateTime } from "@/lib/utils";
import {
  povecaj,
  procenat,
  smanji,
  staje,
  stanjeBudzeta,
  vrijednostNacrta,
  type StavkaNacrta,
} from "@/lib/korpica-budzet";

const IZBJEGNUTI_KLJUC = ["account", "izbjegnuti"] as const;

/** „Porodica Kovačević, Han Kola" — domaćin od kojeg namirnica dolazi; null ako ga nema. */
function domacin(naziv?: string, selo?: string): string | null {
  const dijelovi = [naziv, selo].filter(
    (x): x is string => !!x && x.trim().length > 0,
  );
  return dijelovi.length > 0 ? dijelovi.join(", ") : null;
}

/** Poruka iz ApiRequestError ako je ima; inače rezerva. */
function poruka(err: unknown, rezerva: string): string {
  return err instanceof ApiRequestError && err.message.trim()
    ? err.message
    : rezerva;
}

const kol = (x: number) => x.toLocaleString("sr-Latn-BA");

/** Sve što ekran zna o jednoj namirnici — iz stavki korpice ili iz ponude. */
interface Namirnica {
  productId: string;
  naziv: string;
  jedinica: string;
  cijena: number;
  najmanje: number;
  korak: number;
  imageUrl?: string;
  domacin: string | null;
  dodaoKupac: boolean;
}

/**
 * Kupčeva korpica kao budžet: − / + po namirnici (ispod najmanje količine „−" postaje „Izbaci"),
 * „Dodaj namirnicu" iz ponude te korpice, traka „Iskorišteno N % korpice" i jedno „Sačuvaj" kad
 * je korpica u 95–100 %. „Popuni umjesto mene" dopuni ostatak (server predlaže, kupac sačuva).
 * Pojedinačne cijene se ne prikazuju — kupac plaća korpicu, ne stavke.
 */
export function KorpicaEditor({ orderItemId }: { orderItemId: string }) {
  const {
    data: detalji,
    isLoading,
    isError,
    refetch,
  } = useQuery({
    queryKey: queryKeys.mojaKorpica(orderItemId),
    queryFn: () => accountApi.mojaKorpica(orderItemId),
  });

  if (isLoading) {
    return (
      <div className="rounded-card border border-line bg-surface p-5 shadow-card">
        <LoadingBlock label="Učitavam korpicu…" />
      </div>
    );
  }

  if (isError || !detalji) {
    return (
      <div className="rounded-card border border-line bg-surface p-5 shadow-card">
        <p className="text-sm font-medium text-red-700">
          Ne mogu da učitam ovu korpicu.
        </p>
        <Button
          variant="secondary"
          size="sm"
          className="mt-3"
          onClick={() => refetch()}
        >
          Pokušaj ponovo
        </Button>
      </div>
    );
  }

  // Nacrt se gradi iz sačuvanog stanja; novi odgovor servera (poslije čuvanja) ga resetuje.
  const kljuc = detalji.stavke
    .map((s) => `${s.productId}:${s.kolicina}`)
    .join("|");
  return <Uredjivac key={kljuc} orderItemId={orderItemId} detalji={detalji} />;
}

function Uredjivac({
  orderItemId,
  detalji,
}: {
  orderItemId: string;
  detalji: MojaKorpicaDetalji;
}) {
  const qc = useQueryClient();
  const { show } = useToast();
  const { korpica, stavke, ponuda } = detalji;
  const izbjegnuti = useQuery({
    queryKey: IZBJEGNUTI_KLJUC,
    queryFn: () => accountApi.izbjegnuti(),
  });

  const katalog = useMemo(() => {
    const m = new Map<string, Namirnica>();
    for (const p of ponuda)
      m.set(p.productId, {
        productId: p.productId,
        naziv: p.naziv,
        jedinica: p.jedinica,
        cijena: p.cijena,
        najmanje: p.najmanje,
        korak: p.korak,
        imageUrl: p.imageUrl,
        domacin: domacin(p.proizvodjacNaziv, p.proizvodjacSelo),
        dodaoKupac: true,
      });
    // Stavke korpice imaju prednost: njihova cijena je snimak iz narudžbe.
    for (const s of stavke)
      m.set(s.productId, {
        productId: s.productId,
        naziv: s.naziv,
        jedinica: s.jedinica,
        cijena: s.jedinicnaCijena,
        najmanje: s.najmanje,
        korak: s.korak,
        imageUrl: m.get(s.productId)?.imageUrl,
        domacin: domacin(s.proizvodjacNaziv, s.proizvodjacSelo),
        dodaoKupac: s.dodaoKupac,
      });
    return m;
  }, [ponuda, stavke]);

  const pocetni: StavkaNacrta[] = stavke.map((s) => ({
    productId: s.productId,
    kolicina: s.kolicina,
    cijena: s.jedinicnaCijena,
    najmanje: s.najmanje,
    korak: s.korak,
  }));
  const [nacrt, setNacrt] = useState<StavkaNacrta[]>(pocetni);
  const [dodavanje, setDodavanje] = useState(false);
  const [greska, setGreska] = useState<string | null>(null);

  const rokIstekao = new Date(korpica.rokIzmjene).getTime() < Date.now();
  const vrijednost = vrijednostNacrta(nacrt);
  const posto = procenat(vrijednost, korpica.ciljnaVrijednost);
  const stanje = stanjeBudzeta(vrijednost, korpica.ciljnaVrijednost);
  const izmijenjen =
    nacrt.length !== pocetni.length ||
    nacrt.some(
      (s) =>
        pocetni.find((p) => p.productId === s.productId)?.kolicina !==
        s.kolicina,
    );
  const premaloKultura = nacrt.length < korpica.minimalanBrojArtikala;
  const spisak = new Set(detalji.izbjegnuti ?? []);
  const zaDodati = ponuda.filter(
    (p) =>
      !nacrt.some((s) => s.productId === p.productId) &&
      !spisak.has(p.productId),
  );

  function primi(novo: MojaKorpicaDetalji) {
    qc.setQueryData(queryKeys.mojaKorpica(orderItemId), novo);
    qc.invalidateQueries({ queryKey: queryKeys.mojeKorpice() });
    qc.invalidateQueries({ queryKey: IZBJEGNUTI_KLJUC });
  }

  const zaServer = () =>
    nacrt.map((s) => ({ productId: s.productId, kolicina: s.kolicina }));

  const sacuvaj = useMutation({
    mutationFn: () => accountApi.izmijeniKorpicu(orderItemId, zaServer()),
    onSuccess: (novo) => {
      primi(novo);
      show("Korpica je sačuvana.");
    },
    onError: (err) => setGreska(poruka(err, "Čuvanje nije uspjelo.")),
  });

  const popuni = useMutation({
    mutationFn: () => accountApi.popuniKorpicu(orderItemId, zaServer()),
    onSuccess: (predlog) => {
      setGreska(null);
      setNacrt(
        predlog
          .map((p): StavkaNacrta | null => {
            const n = katalog.get(p.productId);
            return n
              ? {
                  productId: p.productId,
                  kolicina: p.kolicina,
                  cijena: n.cijena,
                  najmanje: n.najmanje,
                  korak: n.korak,
                }
              : null;
          })
          .filter((s): s is StavkaNacrta => s !== null),
      );
    },
    onError: (err) => setGreska(poruka(err, "Popunjavanje nije uspjelo.")),
  });

  const vrati = useMutation({
    mutationFn: () => accountApi.vratiKorpicu(orderItemId),
    onSuccess: (novo) => {
      primi(novo);
      show("Korpica je vraćena kako je bila.");
    },
    onError: (err) => setGreska(poruka(err, "Vraćanje nije uspjelo.")),
  });

  const ukloniSaSpiska = useMutation({
    mutationFn: (productId: string) => accountApi.ukloniIzbjegnut(productId),
    onSuccess: (lista) => {
      qc.setQueryData(IZBJEGNUTI_KLJUC, lista);
      qc.invalidateQueries({ queryKey: queryKeys.mojaKorpica(orderItemId) });
    },
    onError: (err) => setGreska(poruka(err, "Nije uspjelo.")),
  });

  const zauzeto = sacuvaj.isPending || popuni.isPending || vrati.isPending;

  function promijeni(productId: string, smjer: "plus" | "minus") {
    setGreska(null);
    setNacrt((n) =>
      n.flatMap((s) => {
        if (s.productId !== productId) return [s];
        if (smjer === "plus") return [povecaj(s)];
        const manje = smanji(s);
        return manje ? [manje] : [];
      }),
    );
  }

  function dodaj(productId: string) {
    const n = katalog.get(productId);
    if (!n) return;
    setGreska(null);
    setNacrt((x) => [
      ...x,
      {
        productId,
        kolicina: n.najmanje,
        cijena: n.cijena,
        najmanje: n.najmanje,
        korak: n.korak,
      },
    ]);
    setDodavanje(false);
  }

  const savjet =
    stanje === "malo"
      ? "Ima još mjesta — dodaj namirnicu, povećaj nešto ili klikni „Popuni umjesto mene“."
      : stanje === "previse"
        ? "Korpica je prepuna — smanji ili izbaci nešto."
        : premaloKultura
          ? `Korpica treba bar ${korpica.minimalanBrojArtikala} namirnica.`
          : izmijenjen
            ? "Sve je u redu — sačuvaj izmjene."
            : null;

  return (
    <div className="flex flex-col gap-4 rounded-card border border-line bg-surface p-5 shadow-card">
      <div className="flex items-start gap-3">
        <span className="relative h-14 w-14 shrink-0 overflow-hidden rounded-xl bg-sand">
          <SmartImage src={korpica.imageUrl} alt={korpica.naziv} sizes="56px" />
        </span>
        <div className="min-w-0 flex-1">
          <p className="line-clamp-2 font-display font-semibold leading-snug text-ink sm:truncate">
            {korpica.naziv}
          </p>
          <p className="truncate text-sm text-muted">
            {korpica.kodNarudzbe} · {nacrt.length} namirnica
          </p>
          <p
            className={cn(
              "mt-1 flex items-center gap-1.5 text-sm",
              rokIstekao ? "font-medium text-red-700" : "text-muted",
            )}
          >
            <Clock className="h-3.5 w-3.5 shrink-0" />
            {rokIstekao ? "Izmjene su zatvorene od " : "Možeš mijenjati do "}
            {formatDateTime(korpica.rokIzmjene)}
          </p>
        </div>
      </div>

      {/* Traka budžeta */}
      <div>
        <div className="flex items-baseline justify-between text-sm">
          <span className="font-medium text-ink">
            Iskorišteno {posto} % korpice
          </span>
          <span className="text-xs text-muted">cilj 95–100 %</span>
        </div>
        <div
          className="relative mt-1.5 h-2.5 overflow-hidden rounded-full bg-sand"
          aria-hidden
        >
          <div
            className={cn(
              "h-full rounded-full transition-all",
              stanje === "ok"
                ? "bg-brand-600"
                : stanje === "previse"
                  ? "bg-red-500"
                  : "bg-honey-500",
            )}
            style={{ width: `${Math.min(posto, 100)}%` }}
          />
          <div className="absolute inset-y-0 left-[95%] w-px bg-ink/30" />
        </div>
        {!rokIstekao && savjet && (
          <p
            className={cn(
              "mt-1.5 text-sm",
              stanje === "previse"
                ? "text-red-700"
                : stanje === "ok"
                  ? "text-brand-700"
                  : "text-muted",
            )}
          >
            {savjet}
          </p>
        )}
      </div>

      <ul className="flex flex-col gap-2">
        {nacrt.map((s) => {
          const n = katalog.get(s.productId);
          const izbacuje = smanji(s) === null;
          // „+" je siv kad bi sljedeći korak prepunio korpicu.
          const viseStaje = staje(
            vrijednost,
            (povecaj(s).kolicina - s.kolicina) * s.cijena,
            korpica.ciljnaVrijednost,
          );
          return (
            <li
              key={s.productId}
              className="flex items-center gap-3 rounded-card border border-line bg-cream p-3"
            >
              <span className="relative h-11 w-11 shrink-0 overflow-hidden rounded-lg bg-sand">
                <SmartImage
                  src={n?.imageUrl}
                  alt={n?.naziv ?? ""}
                  sizes="44px"
                />
              </span>
              <div className="min-w-0 flex-1">
                <p className="line-clamp-2 font-medium leading-snug text-ink sm:truncate">
                  {n?.naziv}
                  {n?.dodaoKupac && (
                    <span className="ml-2 rounded-full bg-brand-100 px-2 py-0.5 align-middle text-[10px] font-semibold text-brand-700">
                      tvoj izbor
                    </span>
                  )}
                </p>
                {n?.domacin && (
                  <p className="truncate text-xs text-muted">od: {n.domacin}</p>
                )}
              </div>
              {rokIstekao ? (
                <span className="shrink-0 text-sm font-semibold text-ink">
                  {kol(s.kolicina)} {n?.jedinica}
                </span>
              ) : (
                <div className="flex shrink-0 items-center gap-1">
                  <button
                    type="button"
                    onClick={() => promijeni(s.productId, "minus")}
                    disabled={zauzeto}
                    aria-label={
                      izbacuje ? `Izbaci ${n?.naziv}` : `Manje ${n?.naziv}`
                    }
                    title={izbacuje ? "Izbaci" : "Manje"}
                    className={cn(
                      "grid h-8 w-8 place-items-center rounded-full ring-1 transition disabled:opacity-50",
                      izbacuje
                        ? "text-red-700 ring-red-200 hover:bg-red-50"
                        : "text-ink ring-line hover:ring-brand-400",
                    )}
                  >
                    {izbacuje ? (
                      <Trash2 className="h-4 w-4" />
                    ) : (
                      <Minus className="h-4 w-4" />
                    )}
                  </button>
                  <span className="w-16 text-center text-sm font-semibold tabular-nums text-ink">
                    {kol(s.kolicina)} {n?.jedinica}
                  </span>
                  <button
                    type="button"
                    onClick={() => promijeni(s.productId, "plus")}
                    disabled={zauzeto || !viseStaje}
                    aria-label={`Više ${n?.naziv}`}
                    title="Više"
                    className="grid h-8 w-8 place-items-center rounded-full text-ink ring-1 ring-line transition hover:ring-brand-400 disabled:opacity-50"
                  >
                    <Plus className="h-4 w-4" />
                  </button>
                </div>
              )}
            </li>
          );
        })}
      </ul>

      {!rokIstekao && (
        <>
          {dodavanje ? (
            <div className="flex flex-col gap-2 rounded-card border border-brand-300 p-3">
              <div className="flex items-center justify-between">
                <p className="text-sm font-semibold text-ink">
                  Dodaj namirnicu
                </p>
                <button
                  type="button"
                  onClick={() => setDodavanje(false)}
                  aria-label="Zatvori"
                  className="rounded-full p-1 text-muted hover:bg-sand"
                >
                  <X className="h-4 w-4" />
                </button>
              </div>
              {zaDodati.length === 0 ? (
                <p className="text-sm text-muted">
                  Nema više namirnica za ovu korpicu ove sedmice.
                </p>
              ) : (
                <div className="grid gap-1.5 sm:grid-cols-2">
                  {zaDodati.map((p) => {
                    // Siva je namirnica koja ni u najmanjoj količini ne staje bez prelaska 100 %.
                    const moze = staje(
                      vrijednost,
                      p.najmanje *
                        (katalog.get(p.productId)?.cijena ?? p.cijena),
                      korpica.ciljnaVrijednost,
                    );
                    return (
                      <button
                        key={p.productId}
                        type="button"
                        onClick={() => dodaj(p.productId)}
                        disabled={!moze}
                        title={
                          moze
                            ? undefined
                            : "Ne staje u korpicu — prvo smanji ili izbaci nešto."
                        }
                        className="flex items-center gap-2 rounded-lg border border-line bg-surface px-2.5 py-2 text-left text-sm transition enabled:hover:border-brand-500 enabled:hover:bg-sand/60 disabled:cursor-not-allowed disabled:opacity-40"
                      >
                        <span className="relative h-8 w-8 shrink-0 overflow-hidden rounded-lg bg-sand">
                          <SmartImage
                            src={p.imageUrl}
                            alt={p.naziv}
                            sizes="32px"
                          />
                        </span>
                        <span className="min-w-0 flex-1">
                          <span className="block truncate text-ink">
                            {p.naziv}
                          </span>
                          <span className="block text-xs text-muted">
                            od {kol(p.najmanje)} {p.jedinica}
                            {p.preporucenaZamjena && (
                              <span className="ml-1.5 font-semibold text-brand-700">
                                preporučeno
                              </span>
                            )}
                          </span>
                          {domacin(p.proizvodjacNaziv, p.proizvodjacSelo) && (
                            <span className="block truncate text-xs text-muted">
                              od:{" "}
                              {domacin(p.proizvodjacNaziv, p.proizvodjacSelo)}
                            </span>
                          )}
                        </span>
                      </button>
                    );
                  })}
                </div>
              )}
              {zaDodati.length > 0 && (
                <p className="text-xs text-muted">
                  Posivljene namirnice ne staju dok ne smanjiš ili izbaciš
                  nešto.
                </p>
              )}
            </div>
          ) : (
            <button
              type="button"
              onClick={() => setDodavanje(true)}
              disabled={zauzeto}
              className="inline-flex items-center justify-center gap-1.5 rounded-lg border border-dashed border-line px-3 py-2.5 text-sm font-medium text-ink transition hover:border-brand-400 disabled:opacity-50"
            >
              <Plus className="h-4 w-4" /> Dodaj namirnicu
            </button>
          )}

          {greska && (
            <p className="rounded-lg bg-red-50 px-3 py-2 text-sm font-medium text-red-700">
              {greska}
            </p>
          )}

          <div className="flex flex-wrap gap-2">
            {stanje === "malo" && (
              <Button
                variant="secondary"
                onClick={() => popuni.mutate()}
                disabled={zauzeto}
              >
                <Sparkles className="h-4 w-4" />{" "}
                {popuni.isPending ? "Popunjavam…" : "Popuni umjesto mene"}
              </Button>
            )}
            <Button
              onClick={() => sacuvaj.mutate()}
              disabled={
                zauzeto || !izmijenjen || stanje !== "ok" || premaloKultura
              }
            >
              {sacuvaj.isPending ? "Čuvam…" : "Sačuvaj"}
            </Button>
            {izmijenjen && (
              <Button
                variant="secondary"
                onClick={() => {
                  setNacrt(pocetni);
                  setGreska(null);
                }}
                disabled={zauzeto}
              >
                Odustani
              </Button>
            )}
          </div>

          {stavke.some((s) => s.dodaoKupac) && (
            <button
              type="button"
              onClick={() => vrati.mutate()}
              disabled={zauzeto}
              className="inline-flex items-center gap-1.5 self-start text-sm font-medium text-muted transition hover:text-ink disabled:opacity-50"
            >
              <RotateCcw className="h-4 w-4" />{" "}
              {vrati.isPending ? "Vraćam…" : "Vrati kako je bilo"}
            </button>
          )}
        </>
      )}

      {(izbjegnuti.data?.length ?? 0) > 0 && (
        <div className="border-t border-line pt-3">
          <p className="mb-2 flex items-center gap-1.5 text-sm font-medium text-ink">
            <Ban className="h-4 w-4 text-muted" /> Ne stavljamo ti
          </p>
          <div className="flex flex-wrap gap-1.5">
            {izbjegnuti.data!.map((a) => (
              <span
                key={a.productId}
                className="inline-flex items-center gap-1 rounded-full bg-sand px-2.5 py-1 text-sm"
              >
                {a.naziv}
                <button
                  type="button"
                  aria-label={`Ponovo dozvoli ${a.naziv}`}
                  title="Ponovo dozvoli"
                  onClick={() => ukloniSaSpiska.mutate(a.productId)}
                  disabled={ukloniSaSpiska.isPending}
                  className="rounded-full p-0.5 text-muted transition hover:bg-white hover:text-ink"
                >
                  <X className="h-3.5 w-3.5" />
                </button>
              </span>
            ))}
          </div>
          <p className="mt-1.5 text-xs text-muted">
            Ove artikle u svakoj narednoj korpici same zamijenimo nečim drugim.
          </p>
        </div>
      )}
    </div>
  );
}
