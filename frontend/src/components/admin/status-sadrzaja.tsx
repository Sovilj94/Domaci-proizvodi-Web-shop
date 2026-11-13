import type { StatusPregleda } from "@/lib/api/contracts";
import { cn } from "@/lib/utils";

const STATUS_META: Record<StatusPregleda, { label: string; badge: string; dot: string }> = {
  Nema: { label: "Nema sadržaja", badge: "bg-zinc-200 text-zinc-600", dot: "bg-zinc-400" },
  Nacrt: { label: "Nacrt", badge: "bg-honey-100 text-honey-600", dot: "bg-honey-500" },
  Objavljen: { label: "Objavljeno", badge: "bg-brand-100 text-brand-700", dot: "bg-brand-500" },
  Zakljucan: { label: "Zaključano", badge: "bg-indigo-100 text-indigo-700", dot: "bg-indigo-500" },
};

/** Oznaka statusa sedmičnog sadržaja — ista na ekranu Korpice i u Postavkama korpica. */
export function StatusPregledaBadge({ status }: { status: StatusPregleda }) {
  const meta = STATUS_META[status];
  return (
    <span
      className={cn(
        "inline-flex shrink-0 items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-semibold",
        meta.badge,
      )}
    >
      <span className={cn("h-1.5 w-1.5 rounded-full", meta.dot)} />
      {meta.label}
    </span>
  );
}
