import type { Alignment, TableCell, TDocumentDefinitions } from "pdfmake/interfaces";
import type { PakerskaLista } from "@/lib/api/contracts";
import type { RedNabavke } from "./nabavka";

const broj = (x: number) => Number(x.toFixed(2)).toLocaleString("sr-Latn");
const datum = (iso: string, opcije: Intl.DateTimeFormatOptions) =>
  new Date(iso).toLocaleString("sr-Latn", { timeZone: "Europe/Sarajevo", ...opcije });
const zaglavlje = (text: string, alignment: Alignment = "left"): TableCell => ({ text, alignment, bold: true, fontSize: 9, color: "#555555" });

/**
 * Spisak za nabavku kao PDF — za štampu ili da se pošalje dobavljaču. Zato bez cijena i bez
 * gazdinstava: samo šta i koliko, uz praznu kolonu za štikliranje pri preuzimanju.
 */
export function dokumentNabavke(d: PakerskaLista, redovi: RedNabavke[]): TDocumentDefinitions {
  const napomena = d.zakljucena
    ? "Konačan spisak — narudžbe su zaključene."
    : `Predračun — narudžbe su otvorene do ${datum(d.rok, { day: "numeric", month: "numeric", hour: "2-digit", minute: "2-digit" })}, količine se još mijenjaju.`;
  return {
    pageSize: "A4",
    pageMargins: [40, 48, 40, 48],
    info: { title: `Nabavka — sedmica ${d.sedmica}/${d.godina}` },
    defaultStyle: { font: "Roboto", fontSize: 11 },
    content: [
      { text: "E piljara", fontSize: 10, color: "#4b7a3a", bold: true },
      { text: `Nabavka za sedmicu ${d.sedmica}.`, fontSize: 20, bold: true, margin: [0, 2, 0, 2] },
      {
        text: `Isporuka: ${datum(d.isporuka, { weekday: "long", day: "numeric", month: "numeric", year: "numeric" })}`,
        color: "#555555",
      },
      { text: napomena, fontSize: 9, color: d.zakljucena ? "#555555" : "#a15c00", margin: [0, 2, 0, 14] },
      {
        table: {
          headerRows: 1,
          widths: [22, "*", 70, 34, 60],
          body: [
            [
              zaglavlje("#"),
              zaglavlje("Artikal"),
              zaglavlje("Količina", "right"),
              zaglavlje(""),
              zaglavlje("Preuzeto", "center"),
            ],
            ...redovi.map((r, i) => [
              { text: `${i + 1}.`, color: "#888888" },
              { text: r.artikalObrisan ? `${r.artikal} (provjeriti)` : r.artikal },
              { text: broj(r.kolicina), alignment: "right" as const, bold: true },
              { text: r.jedinica, color: "#555555" },
              // Kućica za štikliranje — nacrtana, jer Roboto nema znak ☐.
              { canvas: [{ type: "rect" as const, x: 23, y: 1, w: 11, h: 11, lineWidth: 0.8, lineColor: "#777777" }] },
            ]),
          ],
        },
        layout: {
          hLineWidth: (i: number) => (i === 0 ? 0 : i === 1 ? 1 : 0.5),
          vLineWidth: () => 0,
          hLineColor: (i: number) => (i === 1 ? "#333333" : "#dddddd"),
          paddingTop: () => 5,
          paddingBottom: () => 5,
        },
      },
      { text: `Ukupno artikala: ${redovi.length}`, fontSize: 9, color: "#555555", margin: [0, 10, 0, 0] },
    ],
    footer: (strana: number, ukupno: number) => ({
      columns: [
        { text: `Napravljeno ${datum(new Date().toISOString(), { day: "numeric", month: "numeric", year: "numeric", hour: "2-digit", minute: "2-digit" })}` },
        { text: `${strana} / ${ukupno}`, alignment: "right" },
      ],
      margin: [40, 16, 40, 0],
      fontSize: 8,
      color: "#888888",
    }),
  };
}

/** Pravi PDF i preuzima ga. pdfmake (s fontom koji ima č, ć, đ) se učitava tek na klik. */
export async function preuzmiPdfNabavke(d: PakerskaLista, redovi: RedNabavke[]): Promise<void> {
  const [pdfMod, vfsMod] = await Promise.all([import("pdfmake/build/pdfmake"), import("pdfmake/build/vfs_fonts")]);
  const pdfMake = ((pdfMod as { default?: unknown }).default ?? pdfMod) as typeof import("pdfmake/build/pdfmake");
  const vfs = ((vfsMod as { default?: unknown }).default ?? vfsMod) as Parameters<typeof pdfMake.addVirtualFileSystem>[0];
  pdfMake.addVirtualFileSystem(vfs);
  await pdfMake.createPdf(dokumentNabavke(d, redovi)).download(`nabavka-${d.godina}-sedmica-${d.sedmica}.pdf`);
}
