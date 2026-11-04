import { MJESECI, skalaSaMinusom } from "@/lib/admin/poslovanje";
import type { MjesecGodine } from "@/lib/api/contracts";

const V = 220; // visina crteža
const L = 48; // prostor za oznake ose
const SIRINA = 720;

const kmTekst = (x: number) => Math.round(x).toLocaleString("sr-Latn");

/**
 * Neto (svijetlo), zarada na robi (srednje) i dobit (tamno; crveno ispod nule) po mjesecima,
 * ručno u SVG-u. Kad neki mjesec ima gubitak, osa se spušta ispod nule.
 */
export function GrafikonGodine({ mjeseci }: { mjeseci: MjesecGodine[] }) {
  const { max, min } = skalaSaMinusom(mjeseci.flatMap((m) => [m.neto, m.zarada, m.dobit]));
  const raspon = max + min;
  const korak = (SIRINA - L) / 12;
  const y = (x: number) => ((max - x) / raspon) * V;
  const nula = y(0);
  const podjele = [0, 1, 2, 3, 4].map((i) => (max * i) / 4).concat(min > 0 ? [-min] : []);
  const stub = (x: number, s: number, vrijednost: number, klasa: string) => (
    <rect x={x} y={Math.min(y(vrijednost), nula)} width={s} height={Math.abs(y(vrijednost) - nula)} rx={2} className={klasa} />
  );
  return (
    <figure className="space-y-2">
      <svg
        viewBox={`0 -12 ${SIRINA} ${V + 36}`}
        className="w-full"
        role="img"
        aria-label="Neto prihod, zarada i dobit po mjesecima"
      >
        {podjele.map((p) => (
          <g key={p}>
            <line x1={L} x2={SIRINA} y1={y(p)} y2={y(p)} className={p === 0 ? "stroke-muted" : "stroke-line"} strokeWidth={1} />
            <text x={L - 6} y={y(p) + 4} textAnchor="end" className="fill-muted text-[10px]">
              {p.toLocaleString("sr-Latn")}
            </text>
          </g>
        ))}
        {mjeseci.map((m, i) => {
          const x = L + i * korak + korak * 0.12;
          const s = korak * 0.24;
          return (
            <g key={m.mjesec}>
              <title>{`${MJESECI[i]}: neto ${kmTekst(m.neto)} KM, zarada ${kmTekst(m.zarada)} KM, troškovi ${kmTekst(m.troskovi)} KM, dobit ${kmTekst(m.dobit)} KM`}</title>
              {stub(x, s, m.neto, "fill-brand-200")}
              {stub(x + s + 1, s, m.zarada, "fill-brand-400")}
              {stub(x + 2 * (s + 1), s, m.dobit, m.dobit < 0 ? "fill-red-500" : "fill-brand-800")}
              <text x={x + 1.5 * s} y={V + 16} textAnchor="middle" className="fill-muted text-[10px]">
                {MJESECI[i].slice(0, 3)}
              </text>
            </g>
          );
        })}
      </svg>
      <figcaption className="flex flex-wrap gap-4 text-xs text-muted">
        <span>
          <span className="mr-1 inline-block h-2.5 w-2.5 rounded-sm bg-brand-200" />
          Neto prihod (KM)
        </span>
        <span>
          <span className="mr-1 inline-block h-2.5 w-2.5 rounded-sm bg-brand-400" />
          Zarada na robi (KM)
        </span>
        <span>
          <span className="mr-1 inline-block h-2.5 w-2.5 rounded-sm bg-brand-800" />
          Dobit poslije troškova (KM; crveno = gubitak)
        </span>
      </figcaption>
    </figure>
  );
}
