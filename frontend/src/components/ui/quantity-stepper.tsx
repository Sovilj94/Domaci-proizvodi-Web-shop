"use client";

import { Minus, Plus } from "lucide-react";
import { cn } from "@/lib/utils";

export function QuantityStepper({
  value,
  onChange,
  min = 1,
  max,
  size = "md",
}: {
  value: number;
  onChange: (next: number) => void;
  min?: number;
  max?: number;
  size?: "sm" | "md";
}) {
  const dec = () => onChange(Math.max(min, value - 1));
  const inc = () => onChange(max ? Math.min(max, value + 1) : value + 1);
  const btn =
    size === "sm" ? "h-8 w-8" : "h-10 w-10";
  const width = size === "sm" ? "w-8 text-sm" : "w-11 text-sm";

  return (
    <div className="inline-flex items-center rounded-full bg-white ring-1 ring-black/10">
      <button
        type="button"
        onClick={dec}
        disabled={value <= min}
        aria-label="Smanji količinu"
        className={cn(
          "grid place-items-center rounded-full text-ink transition hover:bg-sand disabled:opacity-40",
          btn,
        )}
      >
        <Minus className="h-4 w-4" />
      </button>
      <span className={cn("text-center font-semibold tabular-nums", width)}>{value}</span>
      <button
        type="button"
        onClick={inc}
        disabled={max !== undefined && value >= max}
        aria-label="Povećaj količinu"
        className={cn(
          "grid place-items-center rounded-full text-ink transition hover:bg-sand disabled:opacity-40",
          btn,
        )}
      >
        <Plus className="h-4 w-4" />
      </button>
    </div>
  );
}
