import { cn } from "@/lib/utils";

export interface Kolona<T> {
  naslov: string;
  vrijednost: (r: T) => React.ReactNode;
  desno?: boolean;
}

/** Jednostavna tabela; prazna lista daje red „Nema podataka za ovaj mjesec." */
export function Tabela<T>({
  kolone,
  redovi,
  kljuc,
  prazno = "Nema podataka za ovaj mjesec.",
}: {
  kolone: Kolona<T>[];
  redovi: T[];
  kljuc: (r: T, i: number) => string;
  prazno?: string;
}) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-[28rem] text-sm">
        <thead className="text-left text-xs text-muted">
          <tr className="border-b border-line">
            {kolone.map((k) => (
              <th key={k.naslov} className={cn("p-2 font-medium", k.desno && "text-right")}>
                {k.naslov}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="tabular-nums">
          {redovi.length === 0 ? (
            <tr>
              <td colSpan={kolone.length} className="p-3 text-center text-muted">
                {prazno}
              </td>
            </tr>
          ) : (
            redovi.map((r, i) => (
              <tr key={kljuc(r, i)} className="break-inside-avoid border-b border-line last:border-0">
                {kolone.map((k) => (
                  <td key={k.naslov} className={cn("p-2", k.desno && "text-right")}>
                    {k.vrijednost(r)}
                  </td>
                ))}
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  );
}
