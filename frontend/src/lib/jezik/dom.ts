import { uEngleski } from "./prevod";

/*
 * Prevodi kupčev dio sajta nad DOM-om, POSLIJE hidratacije — isti obrazac kao nekadašnja
 * ćirilica: React nikad ne vidi razliku između serverskog i klijentskog HTML-a, čvorovi se ne
 * zamjenjuju (mijenja se samo `nodeValue`/atribut), a posmatrač prevodi i kasnije upisan tekst.
 * Prevod je idempotentan (engleski tekst nije ključ u rječniku), pa posmatrač ne ulazi u petlju.
 */

const ATRIBUTI = ["placeholder", "title", "aria-label", "alt"];

/*
 * Ne dira se ono što je kupac sam otkucao (textarea, contenteditable) ni ono što je izričito
 * označeno `translate="no"` (brojevi narudžbi, kodovi, e-mail, telefon, imena domaćina).
 */
const PRESKOCI =
  "script, style, noscript, textarea, [contenteditable], [translate='no'], .leaflet-control-attribution";

function tekst(cvor: Text) {
  const roditelj = cvor.parentElement;
  if (!roditelj || roditelj.closest(PRESKOCI)) return;
  const v = cvor.nodeValue;
  if (!v || !v.trim()) return;
  const p = uEngleski(v);
  if (p !== v) cvor.nodeValue = p;
}

function atributi(el: Element) {
  if (el.closest("[translate='no']")) return;
  for (const a of ATRIBUTI) {
    const v = el.getAttribute(a);
    if (!v) continue;
    const p = uEngleski(v);
    if (p !== v) el.setAttribute(a, p);
  }
}

function obidji(koren: Node) {
  if (koren.nodeType === Node.TEXT_NODE) {
    tekst(koren as Text);
    return;
  }
  if (!(koren instanceof Element)) return;
  atributi(koren);
  koren.querySelectorAll(ATRIBUTI.map((a) => `[${a}]`).join(",")).forEach(atributi);
  const setac = document.createTreeWalker(koren, NodeFilter.SHOW_TEXT);
  for (let n = setac.nextNode(); n; n = setac.nextNode()) tekst(n as Text);
}

function naslov() {
  if (document.querySelector("[data-admin]")) return;
  const p = uEngleski(document.title);
  if (p !== document.title) document.title = p;
}

/** Ponovni prolaz preko cijele stranice — kad stigne rječnik naziva proizvoda sa servera. */
export function prevediPonovo(koren: HTMLElement): void {
  obidji(koren);
  naslov();
}

/** Prvi prolaz nad `koren` i posmatranje svih kasnijih izmjena. Vraća funkciju za gašenje. */
export function pokreniPrevodjenje(koren: HTMLElement): () => void {
  obidji(koren);
  naslov();

  // Osigurač protiv petlje (nešto upisuje srpski čim prevedemo): previše poziva = gašenje.
  let prozor = 0;
  let poziva = 0;
  const posmatrac = new MutationObserver((izmjene) => {
    const sad = performance.now();
    if (sad - prozor > 1000) {
      prozor = sad;
      poziva = 0;
    }
    if (++poziva > 500) {
      posmatrac.disconnect();
      console.error("[jezik] prevođenje zaustavljeno — petlja");
      return;
    }
    for (const m of izmjene) {
      if (m.type === "characterData") tekst(m.target as Text);
      else if (m.type === "attributes") atributi(m.target as Element);
      else m.addedNodes.forEach(obidji);
    }
  });
  posmatrac.observe(koren, {
    subtree: true,
    childList: true,
    characterData: true,
    attributes: true,
    attributeFilter: ATRIBUTI,
  });

  const posmatracNaslova = new MutationObserver(naslov);
  posmatracNaslova.observe(document.head, { subtree: true, childList: true, characterData: true });

  return () => {
    posmatrac.disconnect();
    posmatracNaslova.disconnect();
  };
}
