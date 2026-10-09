"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { ShoppingBasket } from "lucide-react";
import { KorpicaEditor } from "@/components/nalog/korpica-editor";
import { Button, buttonVariants } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { LoadingBlock } from "@/components/ui/spinner";
import { queryKeys } from "@/lib/api/hooks";
import { accountApi } from "@/lib/api/endpoints";
import { cn } from "@/lib/utils";

/**
 * Nema sopstvene straže — `app/(shop)/moj-nalog/layout.tsx` već čuva cijelu oblast
 * naloga i ne renderuje djecu bez prijavljenog korisnika. Druga straža ovdje bi se
 * vremenom razišla sa onom u layoutu (vidi task-10-brief.md).
 */
export default function MojaKorpicaPage() {
  const {
    data,
    isLoading,
    isError,
    refetch,
  } = useQuery({
    queryKey: queryKeys.mojeKorpice(),
    queryFn: () => accountApi.mojeKorpice(),
  });

  const lista = data ?? [];

  return (
    <div className="space-y-6">
      <div>
        <h1 className="font-display text-2xl font-bold text-ink md:text-3xl">Moja korpica</h1>
        <p className="mt-1 text-muted">
          Zamijeni artikle u svojoj korpici dok ne istekne rok za ovu sedmicu.
        </p>
      </div>

      {isLoading ? (
        <LoadingBlock />
      ) : isError ? (
        /*
          Bez ove grane neuspjelo učitavanje padne u prazno stanje ispod, pa kupac sa
          naručenom korpicom pročita „nemaš korpicu" — izgleda kao da mu je narudžba
          nestala (vidi task-10-brief.md).
        */
        <EmptyState
          title="Ne mogu da učitam tvoje korpice"
          description="Provjeri konekciju pa pokušaj ponovo."
          icon={<ShoppingBasket className="h-7 w-7" />}
          action={<Button onClick={() => refetch()}>Pokušaj ponovo</Button>}
        />
      ) : lista.length === 0 ? (
        <EmptyState
          title="Nemaš korpicu za izmjenu"
          description="Kad naručiš korpicu, ovdje mijenjaš šta će u njoj biti — sve do roka za tu sedmicu."
          icon={<ShoppingBasket className="h-7 w-7" />}
          action={
            <Link href="/korpice" className={cn(buttonVariants())}>
              Pogledaj korpice
            </Link>
          }
        />
      ) : (
        <div className="flex flex-col gap-5">
          {lista.map((k) => (
            <KorpicaEditor key={k.orderItemId} orderItemId={k.orderItemId} />
          ))}
        </div>
      )}
    </div>
  );
}
