"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { AlertTriangle, FileDown, FilePlus2, Phone, RefreshCw } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useToast } from "@/components/ui/toast";
import { OtkupniListUnos } from "@/components/admin/otkupni-list";
import { karticeGazdinstava, poGazdinstvu, zaStampu, type GrupaGazdinstva } from "@/lib/admin/nabavka";
import { adminApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import { useSadrzajSajta } from "@/lib/api/hooks";
import type { OtkupniList, PakerskaLista } from "@/lib/api/contracts";
import { formatPrice } from "@/lib/utils";

const broj = (x: number) => Number(x.toFixed(2)).toLocaleString("sr-Latn");

/**
 * Nabavka po gazdinstvu: za svako gazdinstvo šta se kod njega uzima. Kad je sedmica zaključana,
 * uz svako stoji otkupni list (PDF za štampu, vozač ga nosi i gazdinstvo potpisuje) i unos
 * stvarno primljenih količina. Prije roka je ovo predračun i otkupnih listova još nema.
 */
export function NabavkaGazdinstva({ lista }: { lista: PakerskaLista }) {
  const qc = useQueryClient();
  const { show } = useToast();
  const sadrzaj = useSadrzajSajta();
  const kljuc = ["admin", "otkupni-listovi", lista.godina, lista.sedmica] as const;

  const listovi = useQuery({
    queryKey: kljuc,
    queryFn: () => adminApi.otkupniListovi(lista.godina, lista.sedmica),
    enabled: lista.zakljucena,
  });
  const grupe = karticeGazdinstava(poGazdinstvu(lista.stavke), listovi.data ?? []);
  const poProdavcu = new Map((listovi.data ?? []).map((l) => [l.prodavac.id, l]));

  const napravi = useMutation({
    mutationFn: () => adminApi.napraviOtkupneListove(lista.godina, lista.sedmica),
    onSuccess: ({ napravljeno }) => {
      qc.invalidateQueries({ queryKey: kljuc });
      show(napravljeno ? `Napravljeno otkupnih listova: ${napravljeno}` : "Svi otkupni listovi već postoje");
    },
    onError: (e) => show(e instanceof ApiRequestError ? e.message : "Otkupni listovi nisu napravljeni", "error"),
  });

  const [pdfRadi, setPdfRadi] = useState<string | null>(null);
  async function pdf(l: OtkupniList[], ime: string, kljucDugmeta: string) {
    setPdfRadi(kljucDugmeta);
    try {
      const { preuzmiOtkupneListove } = await import("@/lib/admin/otkupni-list-pdf");
      await preuzmiOtkupneListove(
        l,
        { adresa: sadrzaj.data?.adresa ?? undefined, telefon: sadrzaj.data?.telefon ?? undefined },
        ime,
      );
    } catch {
      show("PDF nije napravljen — pokušaj ponovo.", "error");
    } finally {
      setPdfRadi(null);
    }
  }

  const svi = zaStampu(listovi.data ?? []);
  const nedostaje =
    lista.zakljucena && listovi.isSuccess && grupe.some((g) => g.proizvodjacId && g.redovi.length > 0 && !poProdavcu.has(g.proizvodjacId));

  return (
    <div className="space-y-4">
      {lista.zakljucena && (
        <div className="flex flex-wrap items-center gap-2 print:hidden">
          {svi.length > 0 && (
            <Button
              size="sm"
              onClick={() => pdf(svi, `otkupni-listovi-${lista.godina}-sedmica-${lista.sedmica}.pdf`, "svi")}
              disabled={pdfRadi !== null}
            >
              <FileDown className="h-4 w-4" /> {pdfRadi === "svi" ? "Pravim PDF…" : `Svi otkupni listovi (${svi.length})`}
            </Button>
          )}
          {nedostaje && (
            <Button size="sm" variant="secondary" onClick={() => napravi.mutate()} disabled={napravi.isPending}>
              <FilePlus2 className="h-4 w-4" /> {napravi.isPending ? "Pravim…" : "Napravi otkupne listove"}
            </Button>
          )}
        </div>
      )}

      {grupe.map((g) => (
        <KarticaGazdinstva
          key={g.proizvodjacId || "bez"}
          g={g}
          list={g.proizvodjacId ? poProdavcu.get(g.proizvodjacId) : undefined}
          pdfRadi={pdfRadi === g.proizvodjacId}
          onPdf={(l) => pdf([l], `otkupni-list-${l.broj}.pdf`, g.proizvodjacId)}
          kljuc={kljuc}
        />
      ))}
    </div>
  );
}

function KarticaGazdinstva({
  g,
  list,
  pdfRadi,
  onPdf,
  kljuc,
}: {
  g: GrupaGazdinstva;
  list?: OtkupniList;
  pdfRadi: boolean;
  onPdf: (l: OtkupniList) => void;
  kljuc: readonly unknown[];
}) {
  const qc = useQueryClient();
  const { show } = useToast();
  const [unos, setUnos] = useState(false);
  const osvjezi = useMutation({
    mutationFn: (id: string) => adminApi.osvjeziOtkupniList(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: kljuc });
      show("Količine na otkupnom listu su osvježene");
    },
    onError: (e) => show(e instanceof ApiRequestError ? e.message : "Osvježavanje nije uspjelo", "error"),
  });

  return (
    <section className="overflow-hidden rounded-card bg-white shadow-card print:break-inside-avoid print:shadow-none">
      <header className="flex flex-wrap items-center justify-between gap-2 border-b border-line bg-brand-50 px-3 py-2">
        <div className="min-w-0">
          <h2 className="font-display text-lg font-bold text-ink">{g.naziv}</h2>
          <p className="flex flex-wrap items-center gap-x-3 text-sm text-muted">
            {g.selo && <span>{g.selo}</span>}
            {g.telefon && (
              <a href={`tel:${g.telefon.replace(/[^\d+]/g, "")}`} className="inline-flex items-center gap-1 hover:text-ink">
                <Phone className="h-3.5 w-3.5" /> {g.telefon}
              </a>
            )}
            {list && <span className="font-medium text-ink">Otkupni list {list.broj}</span>}
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2 print:hidden">
          <span className="text-sm font-semibold text-ink">{g.trosak !== null ? formatPrice(g.trosak) : "—"}</span>
          {list && (
            <>
              <Button size="sm" variant="secondary" onClick={() => setUnos((u) => !u)}>
                {unos ? "Zatvori unos" : "Unesi primljeno"}
              </Button>
              {list.stavke.length > 0 && (
                <Button size="sm" onClick={() => onPdf(list)} disabled={pdfRadi}>
                  <FileDown className="h-4 w-4" /> {pdfRadi ? "Pravim PDF…" : "Otkupni list (PDF)"}
                </Button>
              )}
            </>
          )}
        </div>
      </header>

      {list?.zastario && (
        <div className="flex flex-wrap items-center gap-2 border-b border-line bg-honey-100 px-3 py-2 text-sm text-honey-700 print:hidden">
          <AlertTriangle className="h-4 w-4" />
          Narudžbe su mijenjane poslije zaključavanja — količine na otkupnom listu možda više ne važe.
          {list.stavke.every((s) => s.primljeno == null) && (
            <Button size="sm" variant="secondary" onClick={() => osvjezi.mutate(list.id)} disabled={osvjezi.isPending}>
              <RefreshCw className="h-4 w-4" /> Osvježi količine
            </Button>
          )}
        </div>
      )}

      {g.redovi.length === 0 && list && (
        <p className="border-b border-line px-3 py-2 text-sm text-muted">
          Gazdinstvo više nema robe u nabavci ove sedmice
          {list.stavke.length > 0 ? " — osvježi količine prije štampe." : " — otkupni list se ne nosi u nabavku."}
        </p>
      )}

      {unos && list ? (
        <OtkupniListUnos list={list} kljuc={kljuc} onGotovo={() => setUnos(false)} />
      ) : (
        <table className="w-full text-sm">
          <thead className="text-left text-xs text-muted">
            <tr className="border-b border-line">
              <th className="px-3 py-2.5 font-medium">Artikal</th>
              <th className="px-3 py-2.5 text-right font-medium">Nabaviti</th>
              <th className="px-3 py-2.5 text-right font-medium print:hidden">Nabavno</th>
            </tr>
          </thead>
          <tbody className="tabular-nums">
            {g.redovi.map((r) => (
              <tr key={`${r.artikal}|${r.jedinica}`} className="border-b border-line last:border-0">
                <td className="px-3 py-2.5 font-medium text-ink">{r.artikal}</td>
                <td className="whitespace-nowrap px-3 py-2.5 text-right font-display text-base font-bold text-ink">
                  {broj(r.kolicina)} <span className="text-sm font-semibold text-muted">{r.jedinica}</span>
                </td>
                <td className="whitespace-nowrap px-3 py-2.5 text-right text-muted print:hidden">
                  {r.trosak !== null ? formatPrice(r.trosak) : "—"}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}
