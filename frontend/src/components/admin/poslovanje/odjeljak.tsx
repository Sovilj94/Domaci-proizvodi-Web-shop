/** Sklopivi odjeljak analitike; pri štampi je uvijek otvoren. */
export function Odjeljak({
  naslov,
  opis,
  children,
  otvoren = true,
}: {
  naslov: string;
  opis?: string;
  children: React.ReactNode;
  otvoren?: boolean;
}) {
  return (
    <details open={otvoren} className="group rounded-card bg-white shadow-card print:shadow-none">
      <summary className="flex cursor-pointer list-none items-baseline justify-between gap-3 px-5 py-4">
        <h2 className="font-display text-lg font-bold text-ink">{naslov}</h2>
        <span className="text-muted transition-transform group-open:rotate-90 print:hidden">›</span>
      </summary>
      <div className="space-y-4 border-t border-line px-5 py-4">
        {opis && <p className="text-sm text-muted">{opis}</p>}
        {children}
      </div>
    </details>
  );
}
