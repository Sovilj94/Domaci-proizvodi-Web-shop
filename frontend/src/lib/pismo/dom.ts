import { uCirilicu } from "./preslovi";

/*
 * Preslovljava kupčev dio sajta nad DOM-om, POSLIJE hidratacije: React zato nikad ne
 * vidi razliku između serverskog i klijentskog HTML-a. Čvorovi se nikad ne zamjenjuju —
 * mijenja se samo `nodeValue`/atribut — pa React zadržava svoje reference i kasnije upisuje
 * u iste čvorove; posmatrač tada preslovi i novi tekst. Pošto je `uCirilicu` idempotentna
 * i piše se samo kad se vrijednost mijenja, posmatrač ne ulazi u petlju.
 */

const ATRIBUTI = ["placeholder", "title", "aria-label", "alt"];

/*
 * Ne dira se ono što je kupac sam otkucao (textarea, contenteditable) ni ono što je
 * izričito označeno `translate="no"` (brojevi narudžbi, kodovi, e-mail, telefon).
 * `<select>` se NE preskače: tekst opcija je natpis, ne unos. Atribucija Leaflet mape
 * („© OpenStreetMap contributors") je pravni tekst na engleskom i ostaje kakva jeste.
 */
const PRESKOCI =
  "script, style, noscript, textarea, [contenteditable], [translate='no'], .leaflet-control-attribution";

function tekst(cvor: Text) {
  const roditelj = cvor.parentElement;
  if (!roditelj || roditelj.closest(PRESKOCI)) return;
  const v = cvor.nodeValue;
  if (!v) return;
  const c = uCirilicu(v);
  if (c !== v) cvor.nodeValue = c;
}

function atributi(el: Element) {
  if (el.closest("[translate='no']")) return;
  for (const a of ATRIBUTI) {
    const v = el.getAttribute(a);
    if (!v) continue;
    const c = uCirilicu(v);
    if (c !== v) el.setAttribute(a, c);
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
  const c = uCirilicu(document.title);
  if (c !== document.title) document.title = c;
}

/** Prvi prolaz nad `koren` i posmatranje svih kasnijih izmjena. Vraća funkciju za gašenje. */
export function pokreniPreslovljavanje(koren: HTMLElement): () => void {
  obidji(koren);
  naslov();

  /*
   * Osigurač: kad bi se nešto na stranici prepiralo sa preslovljavanjem (upiše latinicu
   * čim je mi preslovimo), posmatrač bi se vrtio dok ne zamrzne pregledač. Previše
   * poziva u jednoj sekundi = gašenje i trag u konzoli; stranica ostaje upotrebljiva.
   */
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
      console.error("[pismo] preslovljavanje zaustavljeno — petlja", izmjene.slice(0, 5).map((m) => ({
        tip: m.type,
        cvor: m.target.nodeName,
        tekst: (m.target.textContent ?? "").slice(0, 80),
        roditelj: (m.target.parentElement ?? (m.target as Element)).outerHTML?.slice(0, 200),
      })));
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

  // Next mijenja <title> pri navigaciji; to je van <body>.
  const posmatracNaslova = new MutationObserver(naslov);
  posmatracNaslova.observe(document.head, { subtree: true, childList: true, characterData: true });

  return () => {
    posmatrac.disconnect();
    posmatracNaslova.disconnect();
  };
}
