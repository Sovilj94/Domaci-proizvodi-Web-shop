"use client";

import { useQuery } from "@tanstack/react-query";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { LoadingBlock } from "@/components/ui/spinner";
import { adminKorpiceApi } from "@/lib/api/endpoints";
import { narudzbeRijec } from "@/lib/orders";
import { cn, formatDateTime, formatPrice } from "@/lib/utils";

const broj = (x: number) => Number(x.toFixed(2)).toLocaleString("sr-Latn");

/**
 * Koliko robe treba za sedmicu: sadržaj svih korpica (onakav kakav su ih kupci ostavili) plus
 * Špajz, i koliko korpicama fali preko trenutnog stanja. Dok rok nije prošao kupci još
 * mijenjaju korpice, pa se brojevi mijenjaju; poslije roka su konačni.
 *
 * Isti ključ kao pakerska lista na ekranu Korpice, pa oba ekrana dijele upit.
 */
export function RobaZaSedmicu({ godina, sedmica }: { godina: number; sedmica: number }) {
  const q = useQuery({
    queryKey: ["admin", "korpice", "pakerska", godina, sedmica],
    queryFn: () => adminKorpiceApi.pakerskaLista(godina, sedmica),
    refetchInterval: 60_000,
  });

  if (q.isLoading) return <LoadingBlock />;
  if (q.isError || !q.data)
    return (
      <EmptyState
        title="Ne mogu da učitam robu za sedmicu"
        description="Provjeri konekciju pa pokušaj ponovo."
        action={<Button onClick={() => q.refetch()}>Pokušaj ponovo</Button>}
      />
    );

  const d = q.data;
  const fali = d.stavke.filter((s) => s.fali > 0);
  const stavke = [...d.stavke].sort((a, b) => b.fali - a.fali || a.artikal.localeCompare(b.artikal, "sr-Latn"));

  return (
    <div className="space-y-3">
      <p className={cn("rounded-lg px-3 py-2 text-sm", d.zakljucena ? "bg-brand-50 text-brand-800" : "bg-honey-100 text-honey-700")}>
        {d.zakljucena
          ? `Sedmica je zaključena (rok ${formatDateTime(d.rok)}) — količine su konačne.`
          : `Narudžbe su otvorene do ${formatDateTime(d.rok)} — kupci još mijenjaju korpice, pa se količine mijenjaju.`}
      </p>

      {d.stavke.length === 0 ? (
        <p className="text-sm text-muted">
          {d.brojNarudzbi === 0 ? "Za ovu sedmicu još nema nijedne narudžbe." : "Narudžbe ove sedmice nemaju robe za nabavku."}
        </p>
      ) : (
        <>
          <p className="text-sm text-muted">
            {d.brojNarudzbi} {narudzbeRijec(d.brojNarudzbi)}. Trošak nabavke:{" "}
            <strong className="text-ink">{formatPrice(d.nabavnaVrijednost)}</strong>
            {d.bezNabavneCijene > 0 && ` (${d.bezNabavneCijene} bez unesene nabavne cijene)`}.{" "}
            {fali.length > 0 ? (
              <strong className="text-red-700">Nabaviti treba još {fali.length} {fali.length === 1 ? "artikal" : "artikala"}.</strong>
            ) : (
              <span className="text-brand-700">Stanje pokriva sve korpice.</span>
            )}
          </p>
          <div className="overflow-x-auto rounded-card bg-white shadow-card">
            <table className="w-full min-w-[36rem] text-sm">
              <thead className="text-left text-xs text-muted">
                <tr className="border-b border-line">
                  <th className="p-3 font-medium">Artikal</th>
                  <th className="p-3 text-right font-medium">Korpice</th>
                  <th className="p-3 text-right font-medium">Špajz</th>
                  <th className="p-3 text-right font-medium">Ukupno</th>
                  <th className="p-3 text-right font-medium">Na stanju</th>
                  <th className="p-3 text-right font-medium">Fali</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-line tabular-nums">
                {stavke.map((r) => (
                  <tr key={`${r.artikal}-${r.proizvodjac}-${r.selo}`}>
                    <td className="p-3">
                      <p className="font-medium text-ink">
                        {r.artikal}
                        {r.artikalObrisan && (
                          <span className="ml-2 rounded-full bg-red-100 px-2 py-0.5 text-xs font-semibold text-red-700">
                            Obrisan artikal — provjeriti
                          </span>
                        )}
                      </p>
                      {(r.proizvodjac || r.selo) && (
                        <p className="text-xs text-muted">{[r.proizvodjac, r.selo].filter(Boolean).join(" · ")}</p>
                      )}
                    </td>
                    <td className="p-3 text-right">{r.uKorpicama > 0 ? broj(r.uKorpicama) : "—"}</td>
                    <td className="p-3 text-right">{r.uSpajzu > 0 ? broj(r.uSpajzu) : "—"}</td>
                    <td className="p-3 text-right font-semibold text-ink">
                      {broj(r.kolicina)} {r.jedinica}
                    </td>
                    <td className="p-3 text-right">{broj(r.naStanju)}</td>
                    <td className={cn("p-3 text-right font-semibold", r.fali > 0 ? "text-red-700" : "text-muted")}>
                      {r.fali > 0 ? `${broj(r.fali)} ${r.jedinica}` : "—"}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <p className="text-xs text-muted">
            Špajz je već skinut sa stanja pri narudžbi, pa „Na stanju“ pokazuje ono što ostaje za korpice, a „Fali“ koliko
            korpice traže preko toga.
          </p>
        </>
      )}
    </div>
  );
}
