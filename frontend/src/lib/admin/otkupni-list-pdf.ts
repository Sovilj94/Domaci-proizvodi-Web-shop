import type { Content, TableCell, TDocumentDefinitions } from "pdfmake/interfaces";
import type { OtkupniList } from "@/lib/api/contracts";

const broj = (x: number) => Number(x.toFixed(3)).toLocaleString("sr-Latn");
const novac = (x: number) => `${x.toLocaleString("sr-Latn", { minimumFractionDigits: 2, maximumFractionDigits: 2 })} KM`;
const datum = (iso: string) =>
  new Date(iso).toLocaleDateString("sr-Latn", { day: "numeric", month: "numeric", year: "numeric", timeZone: "Europe/Sarajevo" });
/** Polje koje nije uneseno štampa se kao linija za upis rukom. */
const iliLinija = (s?: string) => (s && s.trim() ? s : "______________________________");
const zaglavlje = (text: string, alignment: "left" | "right" | "center" = "left"): TableCell => ({
  text,
  alignment,
  bold: true,
  fontSize: 8,
  color: "#555555",
});

/** Podaci otkupljivača — iz kontakta sajta (`SadrzajSajta`); prazno ostaje linija. */
export interface Otkupljivac {
  adresa?: string;
  telefon?: string;
}

function podatak(oznaka: string, vrijednost?: string): Content {
  return { text: [{ text: `${oznaka}: `, color: "#555555" }, iliLinija(vrijednost)], fontSize: 9, margin: [0, 1, 0, 1] };
}

function strana(l: OtkupniList, kupac: Otkupljivac, prva: boolean): Content[] {
  const p = l.prodavac;
  const iznos = (s: OtkupniList["stavke"][number]) => (s.cijena != null ? s.naruceno * s.cijena : null);
  return [
    {
      columns: [
        { text: "E piljara", fontSize: 10, color: "#4b7a3a", bold: true },
        { text: `Sedmica ${l.sedmica}/${l.godina}`, fontSize: 9, color: "#888888", alignment: "right" },
      ],
      ...(prva ? {} : { pageBreak: "before" as const }),
    },
    { text: `OTKUPNI LIST br. ${l.broj}`, fontSize: 18, bold: true, margin: [0, 4, 0, 2] },
    { text: `Datum otkupa: ${datum(l.datum)}`, fontSize: 10, color: "#555555", margin: [0, 0, 0, 10] },
    {
      columns: [
        [
          { text: "OTKUPLJIVAČ", bold: true, fontSize: 9, margin: [0, 0, 0, 3] },
          podatak("Naziv", "E piljara"),
          podatak("Adresa", kupac.adresa),
          podatak("JIB", undefined),
          podatak("Telefon", kupac.telefon),
        ],
        [
          { text: "PRODAVAC (poljoprivredno gazdinstvo)", bold: true, fontSize: 9, margin: [0, 0, 0, 3] },
          podatak("Gazdinstvo", `${p.naziv}${p.selo ? `, ${p.selo}` : ""}`),
          podatak("Nosilac", p.nosilacIme),
          podatak("BPG", p.bpg),
          podatak("JMBG", p.jmbg),
          podatak("Adresa", p.adresa),
          podatak("Žiro račun", p.ziroRacun ? `${p.ziroRacun}${p.banka ? ` (${p.banka})` : ""}` : undefined),
          podatak("Telefon", p.telefon),
        ],
      ],
      columnGap: 20,
      margin: [0, 0, 0, 12],
    },
    {
      table: {
        headerRows: 1,
        dontBreakRows: true,
        widths: [20, "*", 30, 52, 56, 56, 62],
        body: [
          [
            zaglavlje("R.br."),
            zaglavlje("Artikal"),
            zaglavlje("Jed."),
            zaglavlje("Naručeno", "right"),
            zaglavlje("Primljeno", "right"),
            zaglavlje("Cijena", "right"),
            zaglavlje("Iznos", "right"),
          ],
          ...l.stavke.map((s, i) => {
            const iz = s.primljeno != null && s.cijena != null ? s.primljeno * s.cijena : iznos(s);
            return [
              { text: `${i + 1}.`, color: "#888888" },
              s.naziv,
              { text: s.jedinica, color: "#555555" },
              { text: broj(s.naruceno), alignment: "right" as const },
              // Prazno dok se ne unese — upisuje se rukom pri preuzimanju.
              { text: s.primljeno != null ? broj(s.primljeno) : "", alignment: "right" as const, bold: true },
              { text: s.cijena != null ? novac(s.cijena) : "", alignment: "right" as const },
              { text: iz != null && s.primljeno != null ? novac(iz) : "", alignment: "right" as const },
            ];
          }),
        ],
      },
      layout: {
        hLineWidth: (i: number) => (i === 0 ? 0 : i === 1 ? 1 : 0.5),
        vLineWidth: () => 0,
        hLineColor: (i: number) => (i === 1 ? "#333333" : "#cccccc"),
        paddingTop: () => 7,
        paddingBottom: () => 7,
      },
    },
    {
      columns: [
        { text: `Stavki: ${l.stavke.length}`, fontSize: 9, color: "#555555" },
        {
          text: [
            { text: "UKUPNO ZA ISPLATU: ", bold: true },
            l.iznosPrimljeno != null && l.stavke.every((s) => s.primljeno != null) ? novac(l.iznosPrimljeno) : "____________ KM",
          ],
          alignment: "right",
          fontSize: 11,
        },
      ],
      margin: [0, 10, 0, 0],
    },
    l.iznos != null
      ? { text: `Po naručenim količinama: ${novac(l.iznos)}`, fontSize: 8, color: "#888888", alignment: "right", margin: [0, 2, 0, 0] }
      : { text: "" },
    {
      text: "Prodavac svojim potpisom potvrđuje da je predao, a otkupljivač da je primio robu u navedenim količinama.",
      fontSize: 8,
      color: "#555555",
      margin: [0, 18, 0, 0],
    },
    {
      columns: [
        [
          { text: "Prodavac (predao)", fontSize: 9, color: "#555555" },
          { text: "\n\n______________________________", fontSize: 9 },
          { text: "Datum: _______________", fontSize: 9, margin: [0, 6, 0, 0] },
        ],
        [
          { text: "Otkupljivač (primio)", fontSize: 9, color: "#555555", alignment: "right" },
          { text: "\n\n______________________________", fontSize: 9, alignment: "right" },
          { text: "Datum: _______________", fontSize: 9, alignment: "right", margin: [0, 6, 0, 0] },
        ],
      ],
      margin: [0, 16, 0, 0],
    },
  ];
}

/**
 * Otkupni listovi za štampu: svaki list na svojoj strani. Vozač ih nosi u nabavku, gazdinstvo
 * upiše primljene količine i potpiše. Polja koja admin nije unio štampaju se kao linije.
 */
export function dokumentOtkupnogLista(listovi: OtkupniList[], kupac: Otkupljivac): TDocumentDefinitions {
  return {
    pageSize: "A4",
    pageMargins: [40, 40, 40, 48],
    info: { title: listovi.length === 1 ? `Otkupni list ${listovi[0].broj}` : "Otkupni listovi" },
    defaultStyle: { font: "Roboto", fontSize: 10 },
    content: listovi.flatMap((l, i) => strana(l, kupac, i === 0)),
    footer: (s: number, ukupno: number) => ({
      text: `${s} / ${ukupno}`,
      alignment: "right",
      margin: [40, 16, 40, 0],
      fontSize: 8,
      color: "#888888",
    }),
  };
}

/** Pravi PDF i preuzima ga. pdfmake (s fontom koji ima č, ć, đ) se učitava tek na klik. */
export async function preuzmiOtkupneListove(listovi: OtkupniList[], kupac: Otkupljivac, ime: string): Promise<void> {
  const [pdfMod, vfsMod] = await Promise.all([import("pdfmake/build/pdfmake"), import("pdfmake/build/vfs_fonts")]);
  const pdfMake = ((pdfMod as { default?: unknown }).default ?? pdfMod) as typeof import("pdfmake/build/pdfmake");
  const vfs = ((vfsMod as { default?: unknown }).default ?? vfsMod) as Parameters<typeof pdfMake.addVirtualFileSystem>[0];
  pdfMake.addVirtualFileSystem(vfs);
  await pdfMake.createPdf(dokumentOtkupnogLista(listovi, kupac)).download(ime);
}
