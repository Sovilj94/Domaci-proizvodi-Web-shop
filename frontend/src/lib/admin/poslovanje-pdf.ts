import type { Content, TableCell, TDocumentDefinitions } from "pdfmake/interfaces";
import type { PoslovanjeMjeseca } from "@/lib/api/contracts";
import { MJESECI, procenat } from "./poslovanje";

const km = (x: number) => `${x.toLocaleString("sr-Latn", { minimumFractionDigits: 2, maximumFractionDigits: 2 })} KM`;
const br = (x: number) => Number(x.toFixed(2)).toLocaleString("sr-Latn");
const z = (t: string, desno = false): TableCell => ({
  text: t,
  bold: true,
  fontSize: 9,
  color: "#555555",
  alignment: desno ? "right" : "left",
});
const d2 = (t: string): TableCell => ({ text: t, alignment: "right" });

/** Naslov i tabela u jednom nedjeljivom bloku — naslov ne ostaje sam na dnu strane (tabele su kratke). */
function tabela(naslov: string, zaglavlje: TableCell[], redovi: TableCell[][], sirine: (string | number)[]): Content[] {
  return [
    {
      unbreakable: true,
      stack: [
        { text: naslov, fontSize: 13, bold: true, color: "#4b7a3a", margin: [0, 12, 0, 4] },
        redovi.length === 0
          ? { text: "Nema podataka za ovaj mjesec.", color: "#888888", fontSize: 10 }
          : {
              table: { headerRows: 1, dontBreakRows: true, widths: sirine, body: [zaglavlje, ...redovi] },
              layout: "lightHorizontalLines",
              fontSize: 10,
            },
      ],
    },
  ];
}

/** Mjesečni pregled poslovanja kao PDF — interni dokument (ima nabavne cijene). */
export function dokumentPoslovanja(d: PoslovanjeMjeseca): TDocumentDefinitions {
  const p = d.prihod;
  const naslov = `${MJESECI[d.mjesec - 1]} ${d.godina}`;
  return {
    pageSize: "A4",
    pageMargins: [40, 48, 40, 48],
    info: { title: `Poslovanje — ${naslov}` },
    defaultStyle: { font: "Roboto", fontSize: 11 },
    content: [
      { text: "E piljara", fontSize: 10, color: "#4b7a3a", bold: true },
      { text: `Poslovanje — ${naslov}.`, fontSize: 20, bold: true, margin: [0, 2, 0, 2] },
      {
        text: "Po datumu isporuke; prihod samo od isporučenih narudžbi. Interno — sadrži nabavne cijene.",
        fontSize: 9,
        color: "#555555",
      },
      ...(p.procjena
        ? [{ text: "Procjena: dio zarade računat po današnjim nabavnim cijenama.", fontSize: 9, color: "#a15c00" } as Content]
        : []),
      ...tabela(
        "Prihod i zarada",
        [z("Stavka"), z("Iznos", true)],
        [
          ["Promet robe", d2(km(p.prometRobe))],
          ["Dostava naplaćena", d2(km(p.dostava))],
          ["Promo popusti", d2(`− ${km(p.promoPopust)}`)],
          ["Popust od poena", d2(`− ${km(p.poeniPopust)}`)],
          [{ text: "Neto prihod", bold: true }, { text: km(p.neto), bold: true, alignment: "right" }],
          ["Nabavni trošak robe", d2(km(p.nabavno))],
          [
            { text: "Zarada na robi", bold: true },
            { text: `${km(p.zarada)} (${procenat(p.marza)})`, bold: true, alignment: "right" },
          ],
          ["Narudžbi / prosječna", d2(`${p.brojNarudzbi} / ${km(p.prosjecnaNarudzba)}`)],
        ],
        ["*", 160],
      ),
      ...tabela(
        "Korpice",
        [z("Korpica"), z("Kom.", true), z("Prihod", true), z("Marža", true)],
        d.prodaja.korpice.map((k) => [k.naziv, d2(br(k.komada)), d2(km(k.prihod)), d2(procenat(k.marza))]),
        ["*", 40, 80, 50],
      ),
      ...tabela(
        "Špajz po kategoriji",
        [z("Kategorija"), z("Prihod", true), z("Marža", true)],
        d.prodaja.spajz.map((k) => [k.kategorija, d2(km(k.prihod)), d2(procenat(k.marza))]),
        ["*", 80, 50],
      ),
      ...tabela(
        "Top 10 artikala",
        [z("Artikal"), z("Količina", true), z("Prihod", true), z("Zarada", true)],
        d.prodaja.topArtikli.map((a) => [a.naziv, d2(`${br(a.kolicina)} ${a.jedinica}`), d2(km(a.prihod)), d2(km(a.zarada))]),
        ["*", 70, 70, 70],
      ),
      ...tabela(
        "Kupci",
        [z("Pokazatelj"), z("Vrijednost", true)],
        [
          ["Aktivni (novi / povratni)", d2(`${d.kupci.aktivni} (${d.kupci.novi} / ${d.kupci.povratni})`)],
          ["Zadržavanje", d2(procenat(d.kupci.zadrzavanje))],
          ["Potrošnja po kupcu", d2(km(d.kupci.potrosnjaPoKupcu))],
          ["Registrovani / gosti", d2(`${d.kupci.registrovani} / ${d.kupci.gosti}`)],
          ["Novi preko preporuke", d2(String(d.kupci.noviPrekoPreporuke))],
        ],
        ["*", 120],
      ),
      ...tabela(
        "Pretplate",
        [z("Pokazatelj"), z("Vrijednost", true)],
        [
          ["Aktivne na kraju mjeseca", d2(String(d.pretplate.aktivneNaKraju))],
          ["Nove / otkazane", d2(`${d.pretplate.nove} / ${d.pretplate.otkazane}`)],
          ["Stopa otkazivanja", d2(procenat(d.pretplate.stopaOtkazivanja))],
          ["Udio prihoda iz pretplata", d2(procenat(d.pretplate.udioPrihoda))],
        ],
        ["*", 120],
      ),
      ...tabela(
        "Dostava po zoni",
        [z("Zona"), z("Narudžbi", true), z("Neto", true)],
        d.dostava.poZoni.map((x) => [x.zona, d2(String(x.narudzbi)), d2(km(x.neto))]),
        ["*", 60, 80],
      ),
      {
        text: `Uspješnost isporuke ${procenat(d.dostava.uspjesnost)} · otkazano ${d.dostava.otkazane} (${km(d.dostava.otkazaneVrijednost)}) · ocjena ${d.dostava.prosjecnaOcjena == null ? "—" : br(d.dostava.prosjecnaOcjena)} (${d.dostava.brojOcjena})`,
        fontSize: 9,
        color: "#555555",
        margin: [0, 4, 0, 0],
      },
      ...tabela(
        "Gazdinstva",
        [z("Gazdinstvo"), z("Selo"), z("Nabavno", true)],
        d.gazdinstva.map((g) => [g.naziv, g.selo, d2(km(g.nabavno))]),
        ["*", 100, 80],
      ),
    ],
    footer: (strana: number, ukupno: number) => ({
      text: `${strana} / ${ukupno}`,
      alignment: "right",
      margin: [40, 16, 40, 0],
      fontSize: 8,
      color: "#888888",
    }),
  };
}

/** Pravi PDF i preuzima ga. pdfmake (s fontom koji ima č, ć, đ) se učitava tek na klik. */
export async function preuzmiPdfPoslovanja(d: PoslovanjeMjeseca): Promise<void> {
  const [pdfMod, vfsMod] = await Promise.all([import("pdfmake/build/pdfmake"), import("pdfmake/build/vfs_fonts")]);
  const pdfMake = ((pdfMod as { default?: unknown }).default ?? pdfMod) as typeof import("pdfmake/build/pdfmake");
  const vfs = ((vfsMod as { default?: unknown }).default ?? vfsMod) as Parameters<typeof pdfMake.addVirtualFileSystem>[0];
  pdfMake.addVirtualFileSystem(vfs);
  await pdfMake.createPdf(dokumentPoslovanja(d)).download(`poslovanje-${d.godina}-${String(d.mjesec).padStart(2, "0")}.pdf`);
}
