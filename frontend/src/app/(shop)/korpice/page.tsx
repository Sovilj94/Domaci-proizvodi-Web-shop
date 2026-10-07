"use client";

import { Suspense, useState } from "react";
import { useSearchParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { korpiceApi } from "@/lib/api/endpoints";
import { TIPOVI_KORPICE, type TipKorpice } from "@/types/domain";
import { KorpicaCard } from "@/components/korpice/korpica-card";
import { TipoviTabovi } from "@/components/korpice/tipovi-tabovi";
import { RokTraka } from "@/components/korpice/rok-traka";
import { LoadingBlock } from "@/components/ui/spinner";
import { EmptyState } from "@/components/ui/empty-state";

// useSearchParams() izbacuje stranicu iz statičke generacije osim ako ne stoji ispod
// <Suspense> granice — isti razlog zbog kojeg su /login, /register i /admin/dostava
// već ovako napisani. Bez ovoga `npm run build` pada u prerender koraku.
export default function KorpicePage() {
  return (
    <Suspense fallback={<LoadingBlock />}>
      <KorpiceSadrzaj />
    </Suspense>
  );
}

function KorpiceSadrzaj() {
  const params = useSearchParams();
  const tipIzUrla = params.get("tip") as TipKorpice | null;
  const tip: TipKorpice =
    tipIzUrla && TIPOVI_KORPICE.includes(tipIzUrla) ? tipIzUrla : "povrce";

  // Slug korpice čiji sadržaj je otvoren — modal dolazi u Tasku 12, ovdje samo
  // čuvamo koji je slug trenutno izabran.
  const [slugZaModal, setSlugZaModal] = useState<string | null>(null);

  const { data: korpice, isLoading, isError } = useQuery({
    queryKey: ["korpice", tip],
    queryFn: () => korpiceApi.lista(tip),
  });

  return (
    <>
      <RokTraka />

      <div className="mx-auto max-w-6xl px-4 py-8 sm:py-10">
        <div className="text-center">
          <h1 className="font-display text-3xl font-bold text-ink sm:text-4xl">
            Izaberi korpicu
          </h1>
          <p className="mt-2 text-muted">Direktno sa Manjače. Samo ono što bismo i sami jeli.</p>
        </div>

        <div className="mt-6 flex justify-center">
          <TipoviTabovi aktivan={tip} />
        </div>

        <div className="mt-8">
          {isLoading ? (
            <LoadingBlock />
          ) : isError ? (
            <EmptyState
              title="Greška pri učitavanju"
              description="Nismo uspjeli dohvatiti korpice. Pokušajte ponovo."
            />
          ) : korpice && korpice.length > 0 ? (
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 lg:gap-6">
              {korpice.map((k) => (
                <KorpicaCard
                  key={k.id}
                  korpica={k}
                  istaknuta={k.velicina === "srednja"}
                  onPogledajSadrzaj={setSlugZaModal}
                  onDodaj={() => {
                    /* Task 13 veže ovo na korpu. */
                  }}
                />
              ))}
            </div>
          ) : (
            <EmptyState
              title="Ova korpica trenutno nije u ponudi"
              description="Pokušajte drugu vrstu korpice."
            />
          )}
        </div>
      </div>

      {/* Modal sa sadržajem korpice dolazi u Tasku 12. */}
      {slugZaModal && null}
    </>
  );
}
