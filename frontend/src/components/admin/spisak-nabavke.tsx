"use client";

import { useState } from "react";
import { FileDown } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useToast } from "@/components/ui/toast";
import type { RedNabavke } from "@/lib/admin/nabavka";
import type { PakerskaLista } from "@/lib/api/contracts";
import { formatPrice } from "@/lib/utils";

const broj = (x: number) => Number(x.toFixed(2)).toLocaleString("sr-Latn");

/** Tabela robe za nabavku — jedan red po artiklu, bez gazdinstava. */
export function SpisakNabavke({ redovi }: { redovi: RedNabavke[] }) {
  return (
    <div className="overflow-x-auto rounded-card bg-white shadow-card print:shadow-none">
      <table className="w-full min-w-[32rem] text-sm">
        <thead className="text-left text-xs text-muted">
          <tr className="border-b border-line">
            <th className="p-3 font-medium">Artikal</th>
            <th className="p-3 text-right font-medium">Nabaviti</th>
            <th className="p-3 text-right font-medium print:hidden">Nabavno</th>
          </tr>
        </thead>
        <tbody className="tabular-nums">
          {redovi.map((r) => (
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
              </td>
              <td className="p-3 text-right font-display text-lg font-bold text-ink">
                {broj(r.kolicina)} <span className="text-sm font-semibold text-muted">{r.jedinica}</span>
              </td>
              <td className="p-3 text-right text-muted print:hidden">{r.trosak ? formatPrice(r.trosak) : "—"}</td>
            </tr>
          ))}
        </tbody>
      </table>
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
