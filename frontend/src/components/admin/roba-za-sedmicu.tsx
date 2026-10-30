"use client";

import { useQuery } from "@tanstack/react-query";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { LoadingBlock } from "@/components/ui/spinner";
import { adminKorpiceApi } from "@/lib/api/endpoints";
import { poProizvodjacu } from "@/lib/admin/nabavka";
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

  const grupe = poProizvodjacu(d.stavke);

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
          <div className="flex flex-wrap items-baseline gap-x-6 gap-y-1">
            <p className="text-sm text-muted">
              Ukupno za nabavku:{" "}
              <strong className="font-display text-xl text-ink">{formatPrice(d.nabavnaVrijednost)}</strong>
            </p>
            <p className="text-sm text-muted">
              {d.stavke.length} {d.stavke.length === 1 ? "artikal" : "artikala"} · {grupe.length}{" "}
              {grupe.length === 1 ? "proizvođač" : "proizvođača"} · {d.brojNarudzbi} {narudzbeRijec(d.brojNarudzbi)}
              {d.bezNabavneCijene > 0 && ` · ${d.bezNabavneCijene} bez nabavne cijene (nisu u zbiru)`}
            </p>
          </div>

          <div className="overflow-x-auto rounded-card bg-white shadow-card">
            <table className="w-full min-w-[32rem] text-sm">
              <thead className="text-left text-xs text-muted">
                <tr className="border-b border-line">
                  <th className="p-3 font-medium">Artikal</th>
                  <th className="p-3 text-right font-medium">Nabaviti</th>
                  <th className="p-3 text-right font-medium">od toga korpice / Špajz</th>
                  <th className="p-3 text-right font-medium">Nabavno</th>
                </tr>
              </thead>
              {grupe.map((g) => (
                <tbody key={`${g.proizvodjac}|${g.selo}`} className="tabular-nums">
                  <tr className="border-b border-line bg-surface">
                    <th colSpan={3} className="px-3 py-2 text-left font-semibold text-ink">
                      {g.proizvodjac || "Bez proizvođača"}
                      {g.selo && <span className="ml-1.5 font-normal text-muted">· {g.selo}</span>}
                    </th>
                    <th className="px-3 py-2 text-right font-semibold text-ink">{g.trosak > 0 ? formatPrice(g.trosak) : "—"}</th>
                  </tr>
                  {g.stavke.map((r) => (
                    <tr key={r.artikal} className="border-b border-line last:border-0">
                      <td className="p-3">
                        <p className="font-medium text-ink">
                          {r.artikal}
                          {r.artikalObrisan && (
                            <span className="ml-2 rounded-full bg-red-100 px-2 py-0.5 text-xs font-semibold text-red-700">
                              Obrisan artikal — provjeriti
                            </span>
                          )}
                        </p>
                        {r.fali > 0 && (
                          <p className="text-xs text-red-700">
                            Na stanju samo {broj(r.naStanju)} {r.jedinica} — fali {broj(r.fali)} {r.jedinica}
                          </p>
                        )}
                      </td>
                      <td className="p-3 text-right text-base font-bold text-ink">
                        {broj(r.kolicina)} {r.jedinica}
                      </td>
                      <td className="p-3 text-right text-muted">
                        {r.uKorpicama > 0 ? broj(r.uKorpicama) : "—"} / {r.uSpajzu > 0 ? broj(r.uSpajzu) : "—"}
                      </td>
                      <td className="p-3 text-right text-muted">
                        {r.nabavnaCijena ? formatPrice(r.kolicina * r.nabavnaCijena) : "—"}
                      </td>
                    </tr>
                  ))}
                </tbody>
              ))}
            </table>
          </div>
          {fali.length > 0 && (
            <p className="text-xs text-muted">
              Crveno: stanje u magacinu ne pokriva korpice ({fali.length}{" "}
              {fali.length === 1 ? "artikal" : "artikala"}). Špajz je već skinut sa stanja pri narudžbi.
            </p>
          )}
        </>
      )}
    </div>
  );
}
