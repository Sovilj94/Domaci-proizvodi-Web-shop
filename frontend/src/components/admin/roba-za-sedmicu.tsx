"use client";

import { useQuery } from "@tanstack/react-query";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { LoadingBlock } from "@/components/ui/spinner";
import { adminKorpiceApi } from "@/lib/api/endpoints";
import { spisakRobe } from "@/lib/admin/nabavka";
import { PdfNabavkeDugme, SpisakNabavke } from "@/components/admin/spisak-nabavke";
import { narudzbeRijec } from "@/lib/orders";
import { cn, formatDateTime, formatPrice } from "@/lib/utils";


/**
 * Koliko robe treba nabaviti za sedmicu: sadržaj svih korpica (onakav kakav su ih kupci
 * ostavili) plus Špajz, u jednom broju po artiklu — roba se ne skladišti, nego se nabavlja kad se
 * sedmica zaključi. Dok rok nije prošao kupci još mijenjaju korpice, pa se brojevi mijenjaju.
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
  const redovi = spisakRobe(d.stavke);

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
              {redovi.length} {redovi.length === 1 ? "artikal" : "artikala"} · {d.brojNarudzbi} {narudzbeRijec(d.brojNarudzbi)}
              {d.bezNabavneCijene > 0 && ` · ${d.bezNabavneCijene} bez nabavne cijene (nisu u zbiru)`}
            </p>
            <div className="ml-auto">
              <PdfNabavkeDugme lista={d} redovi={redovi} variant="secondary" />
            </div>
          </div>

          <SpisakNabavke redovi={redovi} />
        </>
      )}
    </div>
  );
}
