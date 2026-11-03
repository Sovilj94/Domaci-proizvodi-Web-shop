import { MJESECI, skalaGrafikona } from "@/lib/admin/poslovanje";
import type { MjesecGodine } from "@/lib/api/contracts";

const V = 220; // visina crteža
const L = 48; // prostor za oznake ose
const SIRINA = 720;

/** Neto (svijetlo) i zarada (tamno) po mjesecima, ručno u SVG-u. */
export function GrafikonGodine({ mjeseci }: { mjeseci: MjesecGodine[] }) {
  const { max, podjele } = skalaGrafikona(mjeseci.flatMap((m) => [m.neto, m.zarada]));
  const korak = (SIRINA - L) / 12;
  const y = (x: number) => V - (Math.max(0, x) / max) * V;
  return (
    <figure className="space-y-2">
      <svg viewBox={`0 -12 ${SIRINA} ${V + 36}`} className="w-full" role="img" aria-label="Neto prihod i zarada po mjesecima">
        {podjele.map((p) => (
          <g key={p}>
            <line x1={L} x2={SIRINA} y1={y(p)} y2={y(p)} className="stroke-line" strokeWidth={1} />
            <text x={L - 6} y={y(p) + 4} textAnchor="end" className="fill-muted text-[10px]">
              {p.toLocaleString("sr-Latn")}
            </text>
          </g>
        ))}
        {mjeseci.map((m, i) => {
          const x = L + i * korak + korak * 0.15;
          const s = korak * 0.32;
          return (
            <g key={m.mjesec}>
              <title>{`${MJESECI[i]}: neto ${m.neto.toLocaleString("sr-Latn")} KM, zarada ${m.zarada.toLocaleString("sr-Latn")} KM`}</title>
              <rect x={x} y={y(m.neto)} width={s} height={V - y(m.neto)} rx={2} className="fill-brand-300" />
              <rect x={x + s + 2} y={y(m.zarada)} width={s} height={V - y(m.zarada)} rx={2} className="fill-brand-700" />
              <text x={x + s} y={V + 16} textAnchor="middle" className="fill-muted text-[10px]">
                {MJESECI[i].slice(0, 3)}
              </text>
            </g>
          );
        })}
      </svg>
      <figcaption className="flex gap-4 text-xs text-muted">
        <span>
          <span className="mr-1 inline-block h-2.5 w-2.5 rounded-sm bg-brand-300" />
          Neto prihod (KM)
        </span>
        <span>
          <span className="mr-1 inline-block h-2.5 w-2.5 rounded-sm bg-brand-700" />
          Zarada (KM)
        </span>
      </figcaption>
    </figure>
  );
}
