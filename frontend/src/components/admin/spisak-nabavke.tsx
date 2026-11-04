"use client";

import { useState } from "react";
import Link from "next/link";
import { FileDown } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useToast } from "@/components/ui/toast";
import { grupeNabavke, type RedNabavke } from "@/lib/admin/nabavka";
import type { PakerskaLista } from "@/lib/api/contracts";
import { formatPrice } from "@/lib/utils";

const broj = (x: number) => Number(x.toFixed(2)).toLocaleString("sr-Latn");

/** Nabavka broji samo potvrđene narudžbe — ovo javlja koliko ih još čeka potvrdu. */
export function NaCekanjuNapomena({ broj: n }: { broj: number }) {
  if (!n) return null;
  return (
    <p className="rounded-card bg-blue-50 px-4 py-3 text-sm font-medium text-blue-800 print:hidden">
      {n} {n === 1 ? "narudžba čeka" : "narudžbi čeka"} potvrdu i {n === 1 ? "nije" : "nisu"} u spisku.{" "}
      <Link href="/admin/orders" className="underline underline-offset-2">
        Potvrdi narudžbe
      </Link>{" "}
      — količine i nabavna vrijednost se tada ažuriraju. Nepotvrđene do roka naručivanja sistem sam odbija.
    </p>
  );
}
const artikalaRijec = (n: number) => (n % 10 === 1 && n % 100 !== 11 ? "artikal" : "artikala");

/**
 * Tabela robe za nabavku po vrsti robe (Povrće, Voće, … Špajz) — jedan red po artiklu, bez
 * gazdinstava. U Špajzu stoji i kategorija, jer je to grupa različite robe.
 */
export function SpisakNabavke({ redovi }: { redovi: RedNabavke[] }) {
  return (
    <div className="space-y-4">
      {grupeNabavke(redovi).map((g) => (
        <section key={g.naziv} className="overflow-x-auto rounded-card bg-white shadow-card print:shadow-none">
          <div className="flex flex-wrap items-baseline justify-between gap-2 border-b border-line bg-brand-50 px-3 py-2">
            <h2 className="font-display text-lg font-bold text-ink">{g.naziv}</h2>
            <p className="text-sm text-muted">
              {g.redovi.length} {artikalaRijec(g.redovi.length)}
              <span className="print:hidden">{g.trosak ? ` · ${formatPrice(g.trosak)}` : ""}</span>
            </p>
          </div>
          <table className="w-full min-w-[32rem] text-sm">
            <thead className="text-left text-xs text-muted">
              <tr className="border-b border-line">
                <th className="p-3 font-medium">Artikal</th>
                <th className="p-3 text-right font-medium">Nabaviti</th>
                <th className="p-3 text-right font-medium print:hidden">Nabavno</th>
              </tr>
            </thead>
            <tbody className="tabular-nums">
              {g.redovi.map((r) => (
                <tr key={`${r.artikal}|${r.jedinica}`} className="break-inside-avoid border-b border-line last:border-0">
                  <td className="p-3">
                    <p className="font-medium text-ink">
                      {r.artikal}
                      {r.artikalObrisan && (
                        <span className="ml-2 rounded-full bg-red-100 px-2 py-0.5 text-xs font-semibold text-red-700">
                          Obrisan artikal — provjeriti
                        </span>
                      )}
                    </p>
                    {r.kategorija && r.kategorija !== g.naziv && <p className="text-xs text-muted">{r.kategorija}</p>}
                  </td>
                  <td className="p-3 text-right font-display text-lg font-bold text-ink">
                    {broj(r.kolicina)} <span className="text-sm font-semibold text-muted">{r.jedinica}</span>
                  </td>
                  <td className="p-3 text-right text-muted print:hidden">{r.trosak ? formatPrice(r.trosak) : "—"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </section>
      ))}
    </div>
  );
}

/** Preuzima spisak kao PDF (bez cijena — može ići i dobavljaču). */
export function PdfNabavkeDugme({
  lista,
  redovi,
  size = "sm",
  variant = "primary",
}: {
  lista: PakerskaLista;
  redovi: RedNabavke[];
  size?: "sm" | "md";
  variant?: "primary" | "secondary";
}) {
  const [radi, setRadi] = useState(false);
  const { show } = useToast();
  async function preuzmi() {
    setRadi(true);
    try {
      const { preuzmiPdfNabavke } = await import("@/lib/admin/nabavka-pdf");
      await preuzmiPdfNabavke(lista, redovi);
    } catch {
      show("PDF nije napravljen — pokušaj ponovo.", "error");
    } finally {
      setRadi(false);
    }
  }
  return (
    <Button variant={variant} size={size} onClick={preuzmi} disabled={radi}>
      <FileDown className="h-4 w-4" /> {radi ? "Pravim PDF…" : "Preuzmi PDF"}
    </Button>
  );
}
