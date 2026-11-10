import { cn } from "@/lib/utils";

export function StatCard({
  label,
  value,
  hint,
  icon,
  accent = "brand",
}: {
  label: string;
  value: string | number;
  hint?: string;
  icon: React.ReactNode;
  accent?: "brand" | "honey" | "purple" | "blue";
}) {
  const accents: Record<string, string> = {
    brand: "bg-brand-100 text-brand-700",
    honey: "bg-honey-100 text-honey-600",
    purple: "bg-purple-100 text-purple-700",
    blue: "bg-blue-100 text-blue-700",
  };
  return (
    <div className="min-w-0 rounded-card bg-white p-4 shadow-card sm:p-5">
      <div className="flex items-start justify-between gap-2">
        <span className="text-sm font-medium text-muted">{label}</span>
        <span className={cn("grid h-8 w-8 shrink-0 place-items-center rounded-full sm:h-9 sm:w-9", accents[accent])}>
          {icon}
        </span>
      </div>
      <p className="mt-2 truncate font-display text-2xl font-bold sm:mt-3 sm:text-3xl">{value}</p>
      {hint && <p className="mt-1 text-xs text-muted">{hint}</p>}
    </div>
  );
}
