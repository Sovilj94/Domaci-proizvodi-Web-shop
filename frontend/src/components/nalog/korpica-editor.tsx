"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ArrowLeftRight, Ban, Clock, RotateCcw, Sparkles, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { LoadingBlock } from "@/components/ui/spinner";
import { SmartImage } from "@/components/ui/smart-image";
import { useToast } from "@/components/ui/toast";
import { queryKeys } from "@/lib/api/hooks";
import { accountApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import type { MojaKorpicaDetalji } from "@/lib/api/contracts";
import type { PonudaArtikla } from "@/types/domain";
import { cn, formatDateTime, formatPrice, unitSuffix } from "@/lib/utils";

const IZBJEGNUTI_KLJUC = ["account", "izbjegnuti"] as const;

/** Poruka iz ApiRequestError ako je ima; inače rezerva. */
function poruka(err: unknown, rezerva: string): string {
  return err instanceof ApiRequestError && err.message.trim() ? err.message : rezerva;
}

/**
 * Kupčeva korpica, najjednostavnije moguće: na svakom artiklu „Zamijeni". Kupac bira šta
 * želi umjesto njega (ili pusti nas da izaberemo), a količinu uvijek računa server da
 * vrijednost kutije ostane ista — nema budžeta, nema „Sačuvaj", svaka zamjena se upiše
 * odmah. „Ne stavljaj mi ovo ni ubuduće" pamti artikal: naredne korpice ga same zamijene.
 */
export function KorpicaEditor({ orderItemId }: { orderItemId: string }) {
  const qc = useQueryClient();
  const { show } = useToast();
  const [zamjenaZa, setZamjenaZa] = useState<string | null>(null);
  const [greska, setGreska] = useState<string | null>(null);

  const { data: detalji, isLoading, isError, refetch } = useQuery({
    queryKey: queryKeys.mojaKorpica(orderItemId),
    queryFn: () => accountApi.mojaKorpica(orderItemId),
  });
  const izbjegnuti = useQuery({ queryKey: IZBJEGNUTI_KLJUC, queryFn: () => accountApi.izbjegnuti() });

  function primi(novo: MojaKorpicaDetalji) {
    qc.setQueryData(queryKeys.mojaKorpica(orderItemId), novo);
    qc.invalidateQueries({ queryKey: queryKeys.mojeKorpice() });
    qc.invalidateQueries({ queryKey: IZBJEGNUTI_KLJUC });
    setZamjenaZa(null);
    setGreska(null);
  }

  const zamjena = useMutation({
    mutationFn: (v: { izbaci: string; ubaci: string | null; zapamti: boolean; staro: string }) =>
      accountApi.zamijeniUKorpici(orderItemId, v.izbaci, v.ubaci, v.zapamti),
    onSuccess: (novo, v) => {
      const prije = new Set(detalji?.stavke.map((s) => s.productId));
      const dosao = novo.stavke.find((s) => !prije.has(s.productId));
      primi(novo);
      show(
        `${v.staro} → ${dosao ? `${dosao.naziv} ${dosao.kolicina} ${dosao.jedinica}` : "zamijenjeno"}` +
          (v.zapamti ? ` · više ti ne stavljamo: ${v.staro}` : ""),
      );
    },
    onError: (err) => setGreska(poruka(err, "Zamjena nije uspjela.")),
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
        <p className="text-sm font-medium text-red-700">Ne mogu da učitam ovu korpicu.</p>
        <Button variant="secondary" size="sm" className="mt-3" onClick={() => refetch()}>
          Pokušaj ponovo
        </Button>
      </div>
    );
  }

  const { korpica, stavke, ponuda } = detalji;
  const rokIstekao = new Date(korpica.rokIzmjene).getTime() < Date.now();
  const izmijenjena = stavke.some((s) => s.dodaoKupac);
  const zauzeto = zamjena.isPending || vrati.isPending;
  const uKutiji = new Set(stavke.map((s) => s.productId));
  const spisak = new Set(detalji.izbjegnuti ?? []);
  // Za zamjenu se nudi samo ono što nije već u kutiji i nije na spisku „ne stavljaj mi".
  const ponudaZaZamjenu = ponuda.filter((p) => !uKutiji.has(p.productId) && !spisak.has(p.productId));
  const slike = new Map(ponuda.map((p) => [p.productId, p.imageUrl]));

  return (
    <div className="flex flex-col gap-4 rounded-card border border-line bg-surface p-5 shadow-card">
      <div className="flex items-start gap-3">
        <span className="relative h-14 w-14 shrink-0 overflow-hidden rounded-xl bg-sand">
          <SmartImage src={korpica.imageUrl} alt={korpica.naziv} sizes="56px" />
        </span>
        <div className="min-w-0 flex-1">
          <p className="truncate font-display font-semibold text-ink">{korpica.naziv}</p>
          <p className="truncate text-sm text-muted">
            {korpica.kodNarudzbe} · {stavke.length} artikala · vrijednost {formatPrice(korpica.trenutnaVrijednost)}
          </p>
          <p className={cn("mt-1 flex items-center gap-1.5 text-sm", rokIstekao ? "font-medium text-red-700" : "text-muted")}>
            <Clock className="h-3.5 w-3.5 shrink-0" />
            {rokIstekao ? "Izmjene su zatvorene od " : "Možeš mijenjati do "}
            {formatDateTime(korpica.rokIzmjene)}
          </p>
        </div>
      </div>

      {!rokIstekao && (
        <p className="rounded-lg bg-brand-50 px-3 py-2 text-sm text-brand-800">
          Nešto ti ne odgovara? Klikni <b>Zamijeni</b> — količinu računamo mi, da dobiješ istu vrijednost.
        </p>
      )}

      <ul className="flex flex-col gap-2">
        {stavke.map((s) => {
          const otvoren = zamjenaZa === s.productId;
          return (
            <li key={s.productId} className={cn("rounded-card border bg-cream p-3 transition", otvoren ? "border-brand-400" : "border-line")}>
              <div className="flex items-center gap-3">
                <span className="relative h-11 w-11 shrink-0 overflow-hidden rounded-lg bg-sand">
                  <SmartImage src={slike.get(s.productId)} alt={s.naziv} sizes="44px" />
                </span>
                <div className="min-w-0 flex-1">
                  <p className="truncate font-medium text-ink">
                    {s.naziv}
                    {s.dodaoKupac && (
                      <span className="ml-2 rounded-full bg-brand-100 px-2 py-0.5 align-middle text-[10px] font-semibold text-brand-700">
                        tvoj izbor
                      </span>
                    )}
                  </p>
                  <p className="text-sm text-muted">
                    {s.kolicina.toLocaleString("sr-Latn-BA")} {s.jedinica}
                  </p>
                </div>
                {!rokIstekao && (
                  <button
                    type="button"
                    onClick={() => {
                      setGreska(null);
                      setZamjenaZa(otvoren ? null : s.productId);
                    }}
                    aria-expanded={otvoren}
                    disabled={zauzeto}
                    className={cn(
                      "inline-flex shrink-0 items-center gap-1.5 rounded-full px-3 py-1.5 text-sm font-semibold transition disabled:opacity-50",
                      otvoren ? "bg-ink text-white" : "bg-white text-ink ring-1 ring-line hover:ring-brand-400",
                    )}
                  >
                    {otvoren ? <X className="h-4 w-4" /> : <ArrowLeftRight className="h-4 w-4" />}
                    {otvoren ? "Zatvori" : "Zamijeni"}
                  </button>
                )}
              </div>
              {otvoren && (
                <BiracZamjene
                  naziv={s.naziv}
                  ponuda={ponudaZaZamjenu}
                  zauzeto={zauzeto}
                  onIzaberi={(ubaci, zapamti) =>
                    zamjena.mutate({ izbaci: s.productId, ubaci, zapamti, staro: s.naziv })
                  }
                />
              )}
            </li>
          );
        })}
      </ul>

      {greska && <p className="rounded-lg bg-red-50 px-3 py-2 text-sm font-medium text-red-700">{greska}</p>}

      {!rokIstekao && izmijenjena && (
        <button
          type="button"
          onClick={() => vrati.mutate()}
          disabled={zauzeto}
          className="inline-flex items-center gap-1.5 self-start text-sm font-medium text-muted transition hover:text-ink disabled:opacity-50"
        >
          <RotateCcw className="h-4 w-4" /> {vrati.isPending ? "Vraćam…" : "Vrati kako je bilo"}
        </button>
      )}

      {(izbjegnuti.data?.length ?? 0) > 0 && (
        <div className="border-t border-line pt-3">
          <p className="mb-2 flex items-center gap-1.5 text-sm font-medium text-ink">
            <Ban className="h-4 w-4 text-muted" /> Ne stavljamo ti
          </p>
          <div className="flex flex-wrap gap-1.5">
            {izbjegnuti.data!.map((a) => (
              <span key={a.productId} className="inline-flex items-center gap-1 rounded-full bg-sand px-2.5 py-1 text-sm">
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
          <p className="mt-1.5 text-xs text-muted">Ove artikle u svakoj narednoj korpici same zamijenimo nečim drugim.</p>
        </div>
      )}
    </div>
  );
}

/**
 * Šta umjesto `naziv`: „Izaberi umjesto mene" (server uzme prvu zamjenu koja staje), pa
 * preporučene zamjene ove sedmice, pa — na klik — sve ostalo iz sezonske ponude.
 */
function BiracZamjene({
  naziv,
  ponuda,
  zauzeto,
  onIzaberi,
}: {
  naziv: string;
  ponuda: PonudaArtikla[];
  zauzeto: boolean;
  onIzaberi: (productId: string | null, zapamti: boolean) => void;
}) {
  const [zapamti, setZapamti] = useState(false);
  const [sve, setSve] = useState(false);
  const preporucene = ponuda.filter((p) => p.preporucenaZamjena);
  const ostale = ponuda.filter((p) => !p.preporucenaZamjena);
  const prikaz = sve ? [...preporucene, ...ostale] : preporucene.length > 0 ? preporucene : ostale.slice(0, 6);
  const skriveno = preporucene.length + ostale.length - prikaz.length;

  return (
    <div className="mt-3 flex flex-col gap-2 border-t border-line pt-3">
      <button
        type="button"
        onClick={() => onIzaberi(null, zapamti)}
        disabled={zauzeto}
        className="flex items-center justify-center gap-2 rounded-lg bg-brand-600 px-3 py-2.5 text-sm font-semibold text-white transition hover:bg-brand-700 disabled:opacity-60"
      >
        <Sparkles className="h-4 w-4" /> {zauzeto ? "Mijenjam…" : "Izaberi umjesto mene"}
      </button>

      {prikaz.length > 0 && <p className="pt-1 text-xs font-semibold uppercase tracking-wide text-muted">ili izaberi sam</p>}
      <div className="grid gap-1.5 sm:grid-cols-2">
        {prikaz.map((p) => (
          <button
            key={p.productId}
            type="button"
            onClick={() => onIzaberi(p.productId, zapamti)}
            disabled={zauzeto}
            className="flex items-center gap-2 rounded-lg border border-line bg-surface px-2.5 py-2 text-left text-sm transition hover:border-brand-500 hover:bg-sand/60 disabled:opacity-50"
          >
            <span className="relative h-8 w-8 shrink-0 overflow-hidden rounded-lg bg-sand">
              <SmartImage src={p.imageUrl} alt={p.naziv} sizes="32px" />
            </span>
            <span className="min-w-0 flex-1">
              <span className="block truncate text-ink">{p.naziv}</span>
              <span className="block text-xs text-muted">
                {formatPrice(p.cijena)}
                {unitSuffix(p.jedinica)}
                {p.preporucenaZamjena && <span className="ml-1.5 font-semibold text-brand-700">preporučeno</span>}
              </span>
            </span>
          </button>
        ))}
      </div>
      {skriveno > 0 && (
        <button type="button" onClick={() => setSve(true)} className="self-start text-sm font-medium text-brand-700 hover:underline">
          Prikaži sve iz ponude ({skriveno} još)
        </button>
      )}

      <label className="mt-1 flex cursor-pointer items-center gap-2 text-sm text-ink">
        <input
          type="checkbox"
          checked={zapamti}
          onChange={(e) => setZapamti(e.target.checked)}
          className="h-4 w-4 accent-brand-600"
        />
        Ubuduće mi ne stavljaj: {naziv}
      </label>
    </div>
  );
}
