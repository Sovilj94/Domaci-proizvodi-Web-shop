"use client";

import { cn } from "@/lib/utils";

/**
 * Traka tabova dispečerskog ekrana. Namjerno bez biblioteke — projekat nema tab komponentu,
 * a jedna traka dugmadi ne opravdava novu zavisnost.
 *
 * Skroluje horizontalno kad vozača ima previše; `no-scrollbar` je postojeći helper iz
 * globals.css, isti koji koriste trake kategorija u shopu.
 */

export interface DispatchTab {
  id: string;
  label: string;
  /** Inicijali za avatar; izostavljeno na tabu „Nedodijeljeno". */
  initials?: string;
  count: number;
  /** Neaktivan vozač se i dalje prikazuje ako ima narudžbe, ali prigušeno. */
  muted?: boolean;
}

export function DispatchTabs({
  tabs,
  activeId,
  onSelect,
}: {
  tabs: DispatchTab[];
  activeId: string;
  onSelect: (id: string) => void;
}) {
  return (
    <div
      role="tablist"
      aria-label="Vozači"
      className="no-scrollbar -mb-px flex gap-1 overflow-x-auto border-b border-line"
    >
      {tabs.map((tab) => {
        const active = tab.id === activeId;
        return (
          <button
            key={tab.id}
            role="tab"
            type="button"
            aria-selected={active}
            onClick={() => onSelect(tab.id)}
            className={cn(
              "flex shrink-0 items-center gap-2 border-b-2 px-4 py-3 text-sm transition",
              active
                ? "border-brand-600 font-bold text-ink"
                : "border-transparent font-medium text-muted hover:text-ink",
            )}
          >
            {tab.initials && (
              <span
                className={cn(
                  "grid h-6 w-6 shrink-0 place-items-center rounded-full text-[10px] font-bold",
                  active ? "bg-brand-50 text-brand-700" : "bg-sand text-muted",
                )}
              >
                {tab.initials}
              </span>
            )}
            <span className={cn(tab.muted && "line-through")}>{tab.label}</span>
            <span
              className={cn(
                "rounded-full px-2 py-0.5 text-xs font-semibold tabular-nums",
                active ? "bg-brand-600 text-white" : "bg-sand text-muted",
              )}
            >
              {tab.count}
            </span>
          </button>
        );
      })}
    </div>
  );
}

/**
 * Inicijali iz imena: "Mladen Vozač" → "MV", jedna riječ daje jedno slovo.
 *
 * Uzimaju se samo riječi koje POČINJU slovom — imena vozača u bazi nose dodatak u zagradi
 * ("Mladen (kombi 1)"), pa bi slijepo uzimanje prva dva znaka dalo "M(".
 */
export function inicijali(ime: string): string {
  const slova = ime
    .trim()
    .split(/\s+/)
    .map((d) => d[0] ?? "")
    .filter((z) => /\p{L}/u.test(z));

  return slova.slice(0, 2).join("").toUpperCase() || "?";
}
