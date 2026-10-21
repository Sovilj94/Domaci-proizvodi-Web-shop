"use client";

import { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { X } from "lucide-react";
import { AdminLadica } from "@/components/admin/admin-ladica";
import { KutijaEditor, Plocice } from "@/components/admin/kutija-editor";
import { Button } from "@/components/ui/button";
import { LoadingBlock } from "@/components/ui/spinner";
import { EmptyState } from "@/components/ui/empty-state";
import { useToast } from "@/components/ui/toast";
import { adminApi, adminKorpiceApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import { type KutijaRed, mjesecSedmice, sezonskaPonuda } from "@/lib/admin/kutija";
import { formatMarza } from "@/lib/marza";
import { formatPrice, unitSuffix } from "@/lib/utils";
import type { Product } from "@/types/domain";

/** Poruka iz ApiRequestError ako je ima; inače rezerva. */
function poruka(err: unknown, rezerva: string): string {
  return err instanceof ApiRequestError && err.message.trim() ? err.message : rezerva;
}

/**
 * Bočni panel za sedmični sadržaj korpice. Kutija se uređuje zajedničkim `KutijaEditor`-om
 * (+/−, zamjena iste vrijednosti, dodavanje klikom na pločicu sezonske ponude), a ispod su
 * predložene zamjene koje kupci vide — i one se dodaju klikom.
 *
 * `PUT /admin/korpice/sadrzaj/{id}` zamjenjuje CIJELI sadržaj (i spisak zamjena) onim što se
 * pošalje, pa se lokalni redovi popune tek kad nacrt stigne (prije toga su `null`, ne prazan
 * niz) i „Sačuvaj" je do tada onemogućen — nikad se ne šalje prazna lista preko nacrta.
 *
 * Objavljen ili zaključan sadržaj se otvara samo za pregled (`samoPregled`).
 */
export function SadrzajNacrtEditor({
  sadrzajId,
  korpicaNaziv,
  cilj,
  samoPregled = false,
  onClose,
}: {
  sadrzajId: string;
  korpicaNaziv: string;
  /**
   * Ciljna vrijednost iz pregleda sedmice (`ciljGenerisanja`), NE `SadrzajKorpice.ciljnaVrijednost`
   * — potonje polje backend upisuje tek pri objavi (`ObjaviSadrzaj`), pa je za nacrt uvijek 0.
   */
  cilj: number;
  samoPregled?: boolean;
  onClose: () => void;
}) {
  const qc = useQueryClient();
  const { show } = useToast();
  const [redovi, setRedovi] = useState<KutijaRed[] | null>(null);
  const [zamjene, setZamjene] = useState<string[] | null>(null);

  const sadrzajKljuc = ["admin", "korpice", "sadrzaj", sadrzajId] as const;
  const { data: sadrzaj, isLoading, isError } = useQuery({
    queryKey: sadrzajKljuc,
    queryFn: () => adminKorpiceApi.sadrzaj(sadrzajId),
  });
  const { data: sviArtikli } = useQuery({ queryKey: ["admin", "products"], queryFn: () => adminApi.products() });
  const poId = useMemo(() => new Map<string, Product>((sviArtikli ?? []).map((a) => [a.id, a])), [sviArtikli]);

  // Nacrt se u lokalni state popuni TAČNO JEDNOM (kad stignu i sadržaj i katalog) — refetch
  // sa nekog drugog panela ne smije pregaziti izmjene koje admin još nije sačuvao.
  useEffect(() => {
    if (!sadrzaj || !sviArtikli || redovi !== null) return;
    const cijene = new Map(sadrzaj.cijene.map((c) => [c.productId, c]));
    setRedovi(
      sadrzaj.stavke.map((s) => {
        const p = poId.get(s.productId);
        const c = cijene.get(s.productId);
        return {
          productId: s.productId,
          naziv: s.naziv,
          jedinica: s.jedinica,
          kolicina: s.kolicina,
          // Objavljen: snimak cijene; nacrt: katalog (server pri čuvanju nacrta uzima katalog).
          cijena: samoPregled ? (c?.jedinicnaCijena ?? p?.price ?? 0) : (p?.price ?? c?.jedinicnaCijena ?? 0),
          nabavna: p?.nabavnaCijena ?? c?.nabavnaCijena ?? null,
          imageUrl: s.imageUrl,
          boja: p?.boja,
        };
      }),
    );
    setZamjene(sadrzaj.zamjene.map((z) => z.productId));
  }, [sadrzaj, sviArtikli, redovi, poId, samoPregled]);

  const spreman = redovi !== null && zamjene !== null;

  const sacuvaj = useMutation({
    mutationFn: () => {
      if (!redovi || !zamjene) throw new Error("Nacrt još nije učitan.");
      return adminKorpiceApi.izmijeni(sadrzajId, {
        stavke: redovi.map((r) => ({ productId: r.productId, kolicina: r.kolicina })),
        zamjene: zamjene.filter((id) => !redovi.some((r) => r.productId === id)),
      });
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin", "korpice", "pregled"] });
      qc.invalidateQueries({ queryKey: sadrzajKljuc });
      qc.invalidateQueries({ queryKey: ["admin", "zadaci"] });
      show("Nacrt sačuvan");
      onClose();
    },
    onError: (err) => show(poruka(err, "Čuvanje nije uspjelo"), "error"),
  });

  const ponuda = useMemo(
    () => (sadrzaj && sviArtikli ? sezonskaPonuda(sviArtikli, mjesecSedmice(sadrzaj.godina, sadrzaj.sedmica)) : []),
    [sadrzaj, sviArtikli],
  );
  const ciljPrikaz = samoPregled && sadrzaj?.ciljnaVrijednost ? sadrzaj.ciljnaVrijednost : cilj;
  const zauzeti = new Set([...(redovi ?? []).map((r) => r.productId), ...(zamjene ?? [])]);

  return (
    <AdminLadica naslov={samoPregled ? "Sadržaj korpice" : "Izmjena nacrta"} podnaslov={korpicaNaziv} onClose={onClose} sirina="max-w-2xl">
      <div className="flex-1 overflow-y-auto p-5">
        {isLoading || !spreman ? (
          isError ? (
            <EmptyState title="Nacrt nije učitan" description="Pokušaj ponovo malo kasnije." />
          ) : (
            <LoadingBlock label="Učitavanje…" />
          )
        ) : (
          <>
            <KutijaEditor
              redovi={redovi!}
              onChange={(novi) => {
                setRedovi(novi);
                // Artikal koji uđe u kutiju više nije zamjena (isto pravilo kao server).
                setZamjene((z) => z && z.filter((id) => !novi.some((r) => r.productId === id)));
              }}
              cilj={ciljPrikaz}
              cijenaKorpice={sadrzaj!.cijenaKorpice}
              minimalnaMarza={sadrzaj!.minimalnaMarza}
              ponuda={ponuda}
              samoPregled={samoPregled}
            />

            <h3 className="mb-1 mt-6 text-sm font-semibold">Predložene zamjene</h3>
            <p className="mb-2 text-xs text-muted">
              Kupac koji neće neki artikal bira jedan od ovih (ili „izaberi umjesto mene“); količinu računa server.
            </p>
            <div className="flex flex-wrap gap-1.5">
              {zamjene!.length === 0 && <p className="text-sm text-muted">Nema predloženih zamjena.</p>}
              {zamjene!.map((id) => {
                const p = poId.get(id);
                const z = sadrzaj!.zamjene.find((x) => x.productId === id);
                const naziv = p?.name ?? z?.naziv ?? id;
                return (
                  <span key={id} className="inline-flex items-center gap-1 rounded-full border border-line bg-white py-1 pl-3 pr-1 text-sm">
                    {naziv}
                    <span className="text-xs text-muted">
                      {p ? `${formatPrice(p.price)}${unitSuffix(p.unit)}` : ""}
                      {p?.marza != null ? ` · ${formatMarza(p.marza)}` : ""}
                    </span>
                    {!samoPregled && (
                      <button
                        type="button"
                        onClick={() => setZamjene((zz) => zz && zz.filter((x) => x !== id))}
                        aria-label={`Ukloni zamjenu ${naziv}`}
                        className="grid h-6 w-6 place-items-center rounded-full text-muted hover:bg-red-50 hover:text-red-600"
                      >
                        <X className="h-3.5 w-3.5" />
                      </button>
                    )}
                  </span>
                );
              })}
            </div>
            {!samoPregled && (
              <div className="mt-3">
                <Plocice
                  ponuda={ponuda.filter((p) => !zauzeti.has(p.id))}
                  najvise={9}
                  onIzaberi={(p) => setZamjene((z) => (z ? [...z, p.id] : z))}
                />
              </div>
            )}
          </>
        )}
      </div>

      <div className="flex gap-3 border-t border-line p-4">
        <Button variant="secondary" className="flex-1" onClick={onClose} type="button">
          {samoPregled ? "Zatvori" : "Odustani"}
        </Button>
        {!samoPregled && (
          <Button className="flex-1" onClick={() => sacuvaj.mutate()} disabled={!spreman || redovi!.length === 0 || sacuvaj.isPending}>
            {sacuvaj.isPending ? "Čuvam…" : "Sačuvaj"}
          </Button>
        )}
      </div>
    </AdminLadica>
  );
}
