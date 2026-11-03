import { promjena } from "@/lib/admin/poslovanje";
import { cn } from "@/lib/utils";

/** Broj mjeseca sa ▲/▼ % prema prethodnom. `obrnuto`: rast je loš (npr. otkazane). */
export function KarticaPoslovanja({
  label,
  vrijednost,
  sada,
  prije,
  obrnuto = false,
  hint,
}: {
  label: string;
  vrijednost: string;
  sada?: number;
  prije?: number;
  obrnuto?: boolean;
  hint?: string;
}) {
  const p = sada === undefined || prije === undefined ? null : promjena(sada, prije);
  const dobro = p !== null && (obrnuto ? p < 0 : p > 0);
  return (
    <div className="rounded-card bg-white p-5 shadow-card print:border print:border-line print:shadow-none">
      <p className="text-sm font-medium text-muted">{label}</p>
      <p className="mt-2 font-display text-2xl font-bold tabular-nums">{vrijednost}</p>
      {p !== null ? (
        <p
          className={cn(
            "mt-1 text-xs font-semibold tabular-nums",
            p === 0 ? "text-muted" : dobro ? "text-brand-700" : "text-red-700",
          )}
        >
          {p > 0 ? "▲" : p < 0 ? "▼" : "="} {Math.abs(p).toLocaleString("sr-Latn")}% od prošlog mjeseca
        </p>
      ) : (
        hint && <p className="mt-1 text-xs text-muted">{hint}</p>
      )}
    </div>
  );
}
