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
    <div className="rounded-card bg-white p-5 shadow-card">
      <div className="flex items-center justify-between">
        <span className="text-sm font-medium text-muted">{label}</span>
        <span className={cn("grid h-9 w-9 place-items-center rounded-full", accents[accent])}>
          {icon}
        </span>
      </div>
      <p className="mt-3 font-display text-3xl font-bold">{value}</p>
      {hint && <p className="mt-1 text-xs text-muted">{hint}</p>}
    </div>
  );
}
