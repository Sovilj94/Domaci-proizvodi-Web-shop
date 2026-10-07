"use client";

import { useEffect, useRef } from "react";
import { useQuery } from "@tanstack/react-query";
import { motion } from "motion/react";
import { X } from "lucide-react";
import { korpiceApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import { SmartImage } from "@/components/ui/smart-image";
import { LoadingBlock } from "@/components/ui/spinner";
import { EmptyState } from "@/components/ui/empty-state";
import { unitSuffix } from "@/lib/utils";

/**
 * Centrirani dijalog sa sadržajem korpice za tekuću sedmicu — ko je šta uzgojio
 * i u kom selu, stavka po stavku. To je ono što konkurencija ne pokazuje, zato
 * je proizvođač i selo ovdje naglašen, ne usputan.
 *
 * Nasloni se na isti obrazac kao `CartDrawer` (motion backdrop + Escape), samo
 * centriran umjesto bočnog panela — sadržaj korpice je lista za pregled, ne
 * radni panel koji ostaje otvoren uz ostatak stranice.
 */
export function SadrzajModal({ slug, onClose }: { slug: string; onClose: () => void }) {
  const panelRef = useRef<HTMLDivElement>(null);

  const { data, isPending, isError, error } = useQuery({
    queryKey: ["korpice", slug, "sadrzaj"],
    queryFn: () => korpiceApi.sadrzaj(slug),
    retry: false,
  });

  // Backend baca NotFoundException("sadrzaj_nije_objavljen") — NotFoundException
  // uvijek nosi Code = "not_found", pa se konkretan identifikator nalazi u
  // porukama (ApiRequestError.message), ne u kodu greške.
  const nijeObjavljen =
    error instanceof ApiRequestError &&
    error.status === 404 &&
    error.message === "sadrzaj_nije_objavljen";

  useEffect(() => {
    panelRef.current?.focus();
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose]);

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <motion.button
        type="button"
        aria-label="Zatvori"
        onClick={onClose}
        className="absolute inset-0 bg-black/40"
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        transition={{ duration: 0.2 }}
      />

      <motion.div
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby="sadrzaj-modal-naslov"
        tabIndex={-1}
        className="relative z-10 flex max-h-[85vh] w-full max-w-md flex-col overflow-hidden rounded-card bg-surface shadow-xl focus:outline-none"
        initial={{ opacity: 0, scale: 0.96, y: 8 }}
        animate={{ opacity: 1, scale: 1, y: 0 }}
        transition={{ duration: 0.2 }}
      >
        <div className="flex items-start justify-between gap-4 border-b border-line px-5 py-4">
          <div>
            <h2 id="sadrzaj-modal-naslov" className="font-display text-lg font-bold text-ink">
              Sadržaj ove sedmice
            </h2>
            <p className="text-sm text-muted">Ko je šta uzgojio, selo po selo</p>
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label="Zatvori"
            className="shrink-0 rounded-full p-1.5 text-muted transition hover:bg-sand"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        <div className="flex-1 overflow-y-auto p-5">
          {isPending ? (
            <LoadingBlock />
          ) : nijeObjavljen ? (
            <EmptyState
              title="Sadržaj za ovu sedmicu još nije objavljen"
              description="Javljamo se čim bude poznat — obično nekoliko dana prije isporuke."
            />
          ) : isError ? (
            <EmptyState
              title="Sadržaj trenutno nije dostupan"
              description="Pokušajte ponovo malo kasnije."
            />
          ) : (
            <>
              <ul className="flex flex-col gap-3">
                {data?.stavke.map((s) => (
                  <li
                    key={s.productId}
                    className="flex items-center gap-3 rounded-card border border-line bg-cream p-3"
                  >
                    <div className="relative h-14 w-14 shrink-0 overflow-hidden rounded-xl bg-sand">
                      <SmartImage src={s.imageUrl} alt={s.naziv} sizes="56px" />
                    </div>
                    <div className="min-w-0 flex-1">
                      <p className="truncate font-medium text-ink">
                        {s.naziv}{" "}
                        <span className="font-normal text-muted">
                          {s.kolicina}
                          {unitSuffix(s.jedinica)}
                        </span>
                      </p>
                      <p className="truncate text-sm font-medium text-brand-700">
                        {s.proizvodjacNaziv} · {s.proizvodjacSelo}
                      </p>
                    </div>
                  </li>
                ))}
              </ul>

              <p className="mt-5 text-xs text-muted">
                Slike su informativnog karaktera, a sadržaj se mijenja u zavisnosti od sezone i
                trenutne ponude sa Manjače.
              </p>
            </>
          )}
        </div>
      </motion.div>
    </div>
  );
}
