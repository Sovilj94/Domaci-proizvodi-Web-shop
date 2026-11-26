/** Koliko je ostalo do roka, kratko („4 d 3 h", „5 h 7 min", „42 min"); null kad je rok prošao. */
export function preostalo(rokIso: string, sadaMs: number): string | null {
  const min = Math.floor((Date.parse(rokIso) - sadaMs) / 60_000);
  if (min < 0) return null;
  const d = Math.floor(min / 1440);
  const h = Math.floor((min % 1440) / 60);
  const m = min % 60;
  if (d > 0) return `${d} d ${h} h`;
  if (h > 0) return `${h} h ${m} min`;
  return `${m} min`;
}
