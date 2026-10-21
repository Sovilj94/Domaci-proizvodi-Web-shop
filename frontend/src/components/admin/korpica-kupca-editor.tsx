"use client";

import { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Ban } from "lucide-react";
import { AdminLadica } from "@/components/admin/admin-ladica";
import { KutijaEditor } from "@/components/admin/kutija-editor";
import { Button } from "@/components/ui/button";
import { LoadingBlock } from "@/components/ui/spinner";
import { useToast } from "@/components/ui/toast";
import { adminApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import { type KutijaRed, mjesecSedmice, sezonskaPonuda } from "@/lib/admin/kutija";
import { STATUS_META } from "@/lib/orders";
import type { OrderStatus, Product } from "@/types/domain";

/**
 * Admin uređuje korpicu jednog kupca (kupac je zvao: „bez luka, više jaja"). Isti alat kao
 * sedmični sadržaj, ali količine su slobodne — admin nije vezan budžetom, samo vidi
 * vrijednost naspram cilja i maržu. Kupčev spisak „ne stavljaj mi" je istaknut.
 */
export function KorpicaKupcaEditor({ orderItemId, onClose }: { orderItemId: string; onClose: () => void }) {
  const qc = useQueryClient();
  const { show } = useToast();
  const [redovi, setRedovi] = useState<KutijaRed[] | null>(null);

  const kljuc = ["admin", "korpica-narudzbe", orderItemId] as const;
  const { data, isLoading, isError } = useQuery({ queryKey: kljuc, queryFn: () => adminApi.korpicaNarudzbe(orderItemId) });
  const { data: sviArtikli } = useQuery({ queryKey: ["admin", "products"], queryFn: () => adminApi.products() });
  const poId = useMemo(() => new Map<string, Product>((sviArtikli ?? []).map((a) => [a.id, a])), [sviArtikli]);

  useEffect(() => {
    if (!data || !sviArtikli || redovi !== null) return;
    setRedovi(
      data.stavke.map((s) => ({
        productId: s.productId,
        naziv: s.naziv,
        jedinica: s.jedinica,
        kolicina: s.kolicina,
        cijena: s.jedinicnaCijena,
        nabavna: s.nabavnaCijena,
        imageUrl: poId.get(s.productId)?.imageUrl,
        boja: poId.get(s.productId)?.boja,
        izmijenjeno: s.izmijenjeno,
      })),
    );
  }, [data, sviArtikli, redovi, poId]);

  const sacuvaj = useMutation({
    mutationFn: () => adminApi.izmijeniKorpicuNarudzbe(orderItemId, (redovi ?? []).map((r) => ({ productId: r.productId, kolicina: r.kolicina }))),
    onSuccess: (novo) => {
      qc.setQueryData(kljuc, novo);
      qc.invalidateQueries({ queryKey: ["admin", "korpice", "pakerska"] });
      qc.invalidateQueries({ queryKey: ["admin", "pakovanje"] });
      show(`Korpica za ${novo.kupac} sačuvana`);
      onClose();
    },
    onError: (err) => show(err instanceof ApiRequestError ? err.message : "Čuvanje nije uspjelo", "error"),
  });

  const ponuda = useMemo(
    () => (data && sviArtikli ? sezonskaPonuda(sviArtikli, mjesecSedmice(data.godina, data.sedmica)) : []),
    [data, sviArtikli],
  );
  const izbjegnuti = useMemo(() => new Set(data?.izbjegnuti.map((a) => a.productId)), [data]);
  const samoPregled = !!data && !data.mozeSeMijenjati;

  return (
    <AdminLadica
      naslov={data ? `${data.korpica} — ${data.kupac}` : "Korpica kupca"}
      podnaslov={
        data
          ? `${data.kodNarudzbe} · ${STATUS_META[data.statusNarudzbe as OrderStatus]?.label ?? data.statusNarudzbe}` +
            (data.brojKutija > 1 ? ` · ${data.brojKutija} kutije (količine su po kutiji)` : "")
          : undefined
      }
      onClose={onClose}
      sirina="max-w-2xl"
    >
      <div className="flex-1 overflow-y-auto p-5">
        {isError ? (
          <p className="text-sm text-red-700">Korpica nije učitana.</p>
        ) : isLoading || redovi === null ? (
          <LoadingBlock label="Učitavanje…" />
        ) : (
          <div className="flex flex-col gap-4">
            {samoPregled && (
              <p className="rounded-lg bg-sand px-3 py-2 text-sm text-muted">Narudžba je krenula u dostavu — sadržaj je samo za pregled.</p>
            )}
            {data!.izbjegnuti.length > 0 && (
              <p className="flex flex-wrap items-center gap-1.5 rounded-lg bg-red-50 px-3 py-2 text-sm text-red-800">
                <Ban className="h-4 w-4" /> Kupac ne želi:
                {data!.izbjegnuti.map((a) => (
                  <span key={a.productId} className="rounded-full bg-white px-2 py-0.5 text-xs font-medium">
                    {a.naziv}
                  </span>
                ))}
              </p>
            )}
            <KutijaEditor
              redovi={redovi}
              onChange={setRedovi}
              cilj={data!.cilj}
              cijenaKorpice={data!.cijenaKorpice}
              ponuda={ponuda}
              samoPregled={samoPregled}
              izbjegnuti={izbjegnuti}
            />
          </div>
        )}
      </div>
      <div className="flex gap-3 border-t border-line p-4">
        <Button variant="secondary" className="flex-1" onClick={onClose} type="button">
          {samoPregled ? "Zatvori" : "Odustani"}
        </Button>
        {!samoPregled && (
          <Button
            className="flex-1"
            onClick={() => sacuvaj.mutate()}
            disabled={redovi === null || redovi.length === 0 || sacuvaj.isPending}
          >
            {sacuvaj.isPending ? "Čuvam…" : "Sačuvaj"}
          </Button>
        )}
      </div>
    </AdminLadica>
  );
}
