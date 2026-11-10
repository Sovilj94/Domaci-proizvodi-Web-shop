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
    <div className="min-w-0 rounded-card bg-white p-4 shadow-card sm:p-5 print:border print:border-line print:shadow-none">
      <p className="text-xs font-medium text-muted sm:text-sm">{label}</p>
      <p className="mt-1.5 break-words font-display text-xl font-bold tabular-nums sm:mt-2 sm:text-2xl">{vrijednost}</p>
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
