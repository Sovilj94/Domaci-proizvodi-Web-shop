"use client";

import type { Category } from "@/types/domain";
import { cn } from "@/lib/utils";

export function CategoryChips({
  categories,
  active,
  onSelect,
}: {
  categories: Category[];
  active: string | null;
  onSelect: (slug: string | null) => void;
}) {
  return (
    <div className="no-scrollbar -mx-4 flex gap-2 overflow-x-auto px-4 pb-1">
      <Chip label="Sve" selected={active === null} onClick={() => onSelect(null)} />
      {categories.map((c) => (
        <Chip
          key={c.id}
          label={c.name}
          selected={active === c.slug}
          onClick={() => onSelect(c.slug)}
        />
      ))}
    </div>
  );
}

function Chip({
  label,
  selected,
  onClick,
}: {
  label: string;
  selected: boolean;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={cn(
        "whitespace-nowrap rounded-full px-4 py-2 text-sm font-medium transition",
        selected
          ? "bg-brand-600 text-white"
          : "bg-surface text-ink ring-1 ring-line hover:bg-sand",
      )}
    >
      {label}
    </button>
  );
}
