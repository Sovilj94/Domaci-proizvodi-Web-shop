"use client";

import { Fragment, useEffect, useState } from "react";
import Link from "next/link";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ChevronDown, TriangleAlert } from "lucide-react";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { Field, Input, Select } from "@/components/ui/field";
import { LoadingBlock } from "@/components/ui/spinner";
import { useToast } from "@/components/ui/toast";
import { ApiRequestError } from "@/lib/api/client";
import { adminApi, adminKorpiceApi, type ObracunKorpice } from "@/lib/api/endpoints";
import { cn, formatPrice } from "@/lib/utils";

const MJESECI = ["januar", "februar", "mart", "april", "maj", "jun", "jul", "avgust", "septembar", "oktobar", "novembar", "decembar"];
const KRATKO = ["Jan", "Feb", "Mar", "Apr", "Maj", "Jun", "Jul", "Avg", "Sep", "Okt", "Nov", "Dec"];
const TIPOVI: Record<string, string> = {
  povrce: "Povrće",
  voce: "Voće",
  mlijecna: "Mliječna",
  suhomesnata: "Suhomesnata",
  domacinska: "Domaćinska",
};
const pct = (x?: number | null) => (x == null ? "—" : `${Math.round(x * 1000) / 10} %`);
const broj = (v: string) => {
  const t = v.trim().replace(",", ".");
  return /^\d+(\.\d+)?$/.test(t) ? Number(t) : null;
};

/**
 * Obračun cijene korpice: koliko korpica smije koštati da imamo zaradu, a da kupac i dalje prođe
 * povoljnije nego da isto kupi u piljari. Tipičan sadržaj mjeseca = glavne kulture + prosjek osnovnih
 * artikala do minimuma, u zadatim količinama.
 */
export default function ObracunCijenaPage() {
  const qc = useQueryClient();
  const { show } = useToast();
  const podesavanja = useQuery({ queryKey: ["admin", "podesavanja"], queryFn: adminApi.podesavanja });
  const [marza, setMarza] = useState("");
  const [povoljnije, setPovoljnije] = useState("10");
  const [mjesec, setMjesec] = useState(new Date().getMonth() + 1);
  const [otvorena, setOtvorena] = useState<string | null>(null);

  useEffect(() => {
    if (podesavanja.data && marza === "") setMarza(String(Math.round(podesavanja.data.minimalnaMarza * 100)));
  }, [podesavanja.data, marza]);

  const m = broj(marza);
  const p = broj(povoljnije);
  const ispravno = m != null && m < 90 && p != null && p < 90;
  const q = useQuery({
    queryKey: ["admin", "korpice", "obracun", m, p, mjesec],
    queryFn: () => adminKorpiceApi.obracun(m! / 100, p! / 100, mjesec),
    enabled: ispravno,
  });

  const primijeni = useMutation({
    mutationFn: ({ id, cijena }: { id: string; cijena: number }) => adminKorpiceApi.postaviCijenu(id, cijena),
    onSuccess: (c) => {
      qc.invalidateQueries({ queryKey: ["admin", "korpice"] });
      show(`Nova cijena korpice: ${formatPrice(c)} — važi za nove narudžbe i sljedeće generisanje`);
    },
    onError: (e) => show(e instanceof ApiRequestError ? e.message : "Cijena nije sačuvana.", "error"),
  });

  const d = q.data;
  const poTipu = d ? Object.entries(Object.groupBy(d.korpice, (k) => k.tip)) : [];

  return (
    <div className="space-y-6">
      <div>
        <h1 className="font-display text-2xl font-bold">Obračun cijena korpica</h1>
        <p className="max-w-3xl text-muted">
          Za svaku korpicu: šta tipično ide u nju tog mjeseca (glavne kulture + prosjek osnovnih artikala do minimuma, u
          zadatim količinama), koliko to košta u nabavci sa kutijom i koliko bi isto platio kupac u piljari. Iz toga
          slijede <b className="text-ink">najniža cijena</b> uz koju imamo zaradu i <b className="text-ink">najviša</b> uz koju
          smo povoljniji od piljare.
        </p>
      </div>

      <div className="flex flex-wrap items-end gap-3 rounded-card bg-white p-4 shadow-card">
        <Field label="Ciljna marža (%)">
          <Input inputMode="decimal" value={marza} onChange={(e) => setMarza(e.target.value)} className="w-24" />
        </Field>
        <Field label="Jeftinije od piljare za (%)">
          <Input inputMode="decimal" value={povoljnije} onChange={(e) => setPovoljnije(e.target.value)} className="w-24" />
        </Field>
        <Field label="Mjesec">
          <Select value={mjesec} onChange={(e) => setMjesec(Number(e.target.value))}>
            {MJESECI.map((n, i) => (
              <option key={n} value={i + 1}>
                {n}
              </option>
            ))}
          </Select>
        </Field>
        <p className="max-w-md text-xs text-muted">
          Marža ovdje je samo za obračun. Generator korpica koristi „Minimalnu maržu“ iz{" "}
          <Link href="/admin/korpice/postavke" className="underline">
            Postavki korpica
          </Link>
          {podesavanja.data && <> (sada {Math.round(podesavanja.data.minimalnaMarza * 100)} %)</>}.
        </p>
      </div>

      {d && (d.bezPiljare > 0 || d.bezNabavne > 0) && (
        <div className="flex gap-3 rounded-card border border-honey-300 bg-honey-100 p-4 text-sm text-honey-700">
          <TriangleAlert className="h-5 w-5 shrink-0" />
          <div className="space-y-1">
            {d.bezPiljare > 0 && (
              <p>
                <b>{d.bezPiljare}</b> artikala u korpicama nema cijenu u piljari — za njih se računa naša cijena, pa je
                „najviša cijena“ preniska. Unesi ih u{" "}
                <Link href="/admin/korpice/postavke" className="underline">
                  Postavke korpica → Artikli, kolona „Piljara“
                </Link>
                .
              </p>
            )}
            {d.bezNabavne > 0 && <p><b>{d.bezNabavne}</b> artikala nema nabavnu cijenu i ne ulaze u obračun.</p>}
          </div>
        </div>
      )}

      {!ispravno ? (
        <p className="text-sm text-red-700">Upiši maržu i popust kao brojeve ispod 90.</p>
      ) : q.isLoading ? (
        <LoadingBlock />
      ) : q.isError || !d ? (
        <EmptyState title="Obračun nije uspio" description={q.error instanceof ApiRequestError ? q.error.message : ""} />
      ) : (
        poTipu.map(([tip, korpice]) => (
          <section key={tip} className="overflow-x-auto rounded-card bg-white p-4 shadow-card">
            <h2 className="mb-2 font-display text-lg font-semibold">{TIPOVI[tip] ?? tip}</h2>
            <table className="w-full min-w-[60rem] text-sm">
              <thead className="text-left text-xs text-muted">
                <tr className="border-b border-line">
                  <th className="p-2 font-medium">Korpica</th>
                  <th className="p-2 font-medium" title="Broj artikala tipičnog sadržaja / minimum (★ glavnih)">Sadržaj</th>
                  <th className="p-2 text-right font-medium">Nabavno + kutija</th>
                  <th className="p-2 text-right font-medium" title="Isti sadržaj po našim cijenama u Špajzu">Naša vrijednost</th>
                  <th className="p-2 text-right font-medium">U piljari</th>
                  <th className="p-2 text-right font-medium" title="Najniža cijena za ciljnu maržu">Najniža</th>
                  <th className="p-2 text-right font-medium" title="Najviša cijena da budemo povoljniji od piljare">Najviša</th>
                  <th className="p-2 text-right font-medium">Sada</th>
                  <th className="p-2 text-right font-medium">Preporuka</th>
                  <th className="p-2" />
                </tr>
              </thead>
              <tbody className="tabular-nums">
                {(korpice ?? []).map((k) => (
                  <Fragment key={k.id}>
                    <Red k={k} otvoren={otvorena === k.id} onOtvori={() => setOtvorena(otvorena === k.id ? null : k.id)}
                      onPrimijeni={(cijena) => primijeni.mutate({ id: k.id, cijena })} zauzet={primijeni.isPending} />
                    {otvorena === k.id && <PoMjesecima k={k} />}
                  </Fragment>
                ))}
              </tbody>
            </table>
          </section>
        ))
      )}
    </div>
  );
}

function Red({ k, otvoren, onOtvori, onPrimijeni, zauzet }: {
  k: ObracunKorpice;
  otvoren: boolean;
  onOtvori: () => void;
  onPrimijeni: (cijena: number) => void;
  zauzet: boolean;
}) {
  const m = k.izabrani;
  const [cijena, setCijena] = useState(String(k.preporuka));
  useEffect(() => setCijena(String(k.preporuka)), [k.preporuka]);
  const unos = broj(cijena);
  const nemaSadrzaja = m.artikala === 0;
  return (
    <tr className={cn("border-b border-line align-top last:border-0", !k.uProdaji && "text-muted")}>
      <td className="p-2">
        <button type="button" onClick={onOtvori} className="flex items-center gap-1 text-left font-medium text-ink hover:text-brand-700">
          <ChevronDown className={cn("h-4 w-4 shrink-0 transition", otvoren && "rotate-180")} />
          {k.naziv}
        </button>
        {!k.uProdaji && <span className="ml-5 text-xs">nije u prodaji</span>}
      </td>
      <td className="p-2">
        <span className={cn(m.artikala < m.minimum && "font-semibold text-red-700")}>
          {m.artikala}/{m.minimum}
        </span>
        {m.glavnih > 0 && <span className="text-honey-600"> · {m.glavnih}★</span>}
      </td>
      <td className="p-2 text-right">{formatPrice(m.nabavno + k.ambalaza)}</td>
      <td className="p-2 text-right">{formatPrice(m.nasaVrijednost)}</td>
      <td className="p-2 text-right">
        {formatPrice(m.piljaraVrijednost)}
        {m.bezPiljare > 0 && <span className="block text-[11px] text-honey-700">{m.bezPiljare} bez cijene</span>}
      </td>
      <td className="p-2 text-right font-semibold text-ink">{nemaSadrzaja ? "—" : formatPrice(m.minCijena)}</td>
      <td className="p-2 text-right">{nemaSadrzaja ? "—" : formatPrice(m.maxCijena)}</td>
      <td className="p-2 text-right">
        {formatPrice(k.cijena)}
        <span className={cn("block text-[11px]", m.marzaSada != null && m.marzaSada < 0 ? "text-red-700" : "text-muted")}>
          marža {pct(m.marzaSada)}
        </span>
        <span className={cn("block text-[11px]", m.ustedaSada != null && m.ustedaSada < 0 ? "text-red-700" : "text-muted")}>
          {m.ustedaSada != null && m.ustedaSada < 0 ? "skuplje od piljare " : "jeftinije od piljare "}
          {pct(m.ustedaSada == null ? null : Math.abs(m.ustedaSada))}
        </span>
      </td>
      <td className="p-2 text-right">
        {nemaSadrzaja ? (
          <span className="text-xs text-muted">nema artikala u sezoni</span>
        ) : (
          <>
            <span className={cn("font-semibold", m.moguce ? "text-brand-700" : "text-red-700")}>{formatPrice(k.preporuka)}</span>
            <span className="block text-[11px] text-muted">
              marža {pct(k.marzaPriPreporuci)}, kupac štedi {pct(k.ustedaPriPreporuci)}
            </span>
            {!m.moguce && (
              <span className="block max-w-[14rem] text-[11px] text-red-700">
                Za ovu maržu korpica bi bila skuplja od piljare — spusti maržu, smanji količine ili nađi jeftiniju nabavku.
              </span>
            )}
            <span className="block text-[11px] text-muted">za svaki mjesec: {formatPrice(k.preporukaGodina)}</span>
          </>
        )}
      </td>
      <td className="p-2">
        <div className="flex items-center justify-end gap-1">
          <Input inputMode="decimal" value={cijena} onChange={(e) => setCijena(e.target.value)} className="w-20" aria-label={`Nova cijena — ${k.naziv}`} />
          <Button size="sm" variant="secondary" disabled={zauzet || unos == null || unos === k.cijena} onClick={() => onPrimijeni(unos!)}>
            Primijeni
          </Button>
        </div>
      </td>
    </tr>
  );
}

function PoMjesecima({ k }: { k: ObracunKorpice }) {
  return (
    <tr className="border-b border-line bg-sand/40">
      <td colSpan={10} className="p-3">
        <p className="mb-2 text-xs text-muted">
          Najniža / najviša cijena po mjesecima. Crveno: tog mjeseca nema cijene koja daje i zaradu i povoljnost.
        </p>
        <div className="grid grid-cols-6 gap-1 text-xs sm:grid-cols-12">
          {k.mjeseci.map((m) => (
            <div
              key={m.mjesec}
              className={cn(
                "rounded-md p-1.5 text-center",
                m.artikala === 0 ? "bg-white text-muted" : m.moguce ? "bg-brand-50 text-brand-800" : "bg-red-50 text-red-700",
              )}
              title={`${m.artikala}/${m.minimum} artikala, nabavno ${formatPrice(m.nabavno)}, piljara ${formatPrice(m.piljaraVrijednost)}`}
            >
              <div className="font-semibold">{KRATKO[m.mjesec - 1]}</div>
              {m.artikala === 0 ? (
                <div>—</div>
              ) : (
                <>
                  <div>{formatPrice(m.minCijena)}</div>
                  <div className="opacity-70">{formatPrice(m.maxCijena)}</div>
                </>
              )}
            </div>
          ))}
        </div>
      </td>
    </tr>
  );
}
