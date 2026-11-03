"use client";

import { Suspense } from "react";
import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { ChevronLeft, ChevronRight, Printer } from "lucide-react";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { LoadingBlock } from "@/components/ui/spinner";
import { GrafikonGodine } from "@/components/admin/poslovanje/grafikon-godine";
import { Odjeljak } from "@/components/admin/poslovanje/odjeljak";
import { Tabela, type Kolona } from "@/components/admin/poslovanje/tabela";
import { adminApi } from "@/lib/api/endpoints";
import type { PoslovanjeGodine } from "@/lib/api/contracts";
import { MJESECI, procenat } from "@/lib/admin/poslovanje";
import { formatPrice } from "@/lib/utils";

type RedSezone = PoslovanjeGodine["sezona"][number];

/** Godišnji pregled poslovanja po mjesecima isporuke. Bez ?godina= otvara tekuću godinu. */
export default function AdminPoslovanjeGodinaPage() {
  return (
    <Suspense fallback={<LoadingBlock />}>
      <Godina />
    </Suspense>
  );
}

function Godina() {
  const params = useSearchParams();
  const router = useRouter();
  const pathname = usePathname();
  const g = Number(params.get("godina")) || undefined;
  const q = useQuery({
    queryKey: ["admin", "poslovanje", "godina", g ?? "tekuca"],
    queryFn: () => adminApi.poslovanjeGodina(g),
  });
  const d = q.data;

  const koloneSezone: Kolona<RedSezone>[] = [
    { naslov: "Kategorija", vrijednost: (r) => r.kategorija },
    ...MJESECI.map((ime, i) => ({
      naslov: ime.slice(0, 3),
      vrijednost: (r: RedSezone) => (r.mjeseci[i] ? Math.round(r.mjeseci[i]).toLocaleString("sr-Latn") : "—"),
      desno: true,
    })),
  ];

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="font-display text-2xl font-bold">Poslovanje{d ? ` — ${d.godina}.` : ""}</h1>
          <p className="text-muted">Pregled godine po mjesecima isporuke.</p>
        </div>
        <div className="flex flex-wrap items-center gap-2 print:hidden">
          <Button
            variant="secondary"
            size="sm"
            disabled={!d}
            aria-label="Prethodna godina"
            onClick={() => d && router.replace(`${pathname}?godina=${d.godina - 1}`, { scroll: false })}
          >
            <ChevronLeft className="h-4 w-4" />
          </Button>
          <Button
            variant="secondary"
            size="sm"
            disabled={!d}
            aria-label="Sljedeća godina"
            onClick={() => d && router.replace(`${pathname}?godina=${d.godina + 1}`, { scroll: false })}
          >
            <ChevronRight className="h-4 w-4" />
          </Button>
          <Button variant="secondary" size="sm" onClick={() => window.print()} disabled={!d}>
            <Printer className="h-4 w-4" /> Štampaj
          </Button>
        </div>
      </div>

      {q.isLoading ? (
        <LoadingBlock />
      ) : q.isError || !d ? (
        <EmptyState
          title="Ne mogu da učitam godišnji pregled"
          description="Provjeri konekciju pa pokušaj ponovo."
          action={<Button onClick={() => q.refetch()}>Pokušaj ponovo</Button>}
        />
      ) : (
        <>
          <div className="rounded-card bg-white p-5 shadow-card print:shadow-none">
            <GrafikonGodine mjeseci={d.mjeseci} />
          </div>

          <Odjeljak naslov="Po mjesecima">
            <Tabela
              kljuc={(r) => String(r.mjesec)}
              redovi={d.mjeseci}
              kolone={[
                {
                  naslov: "Mjesec",
                  vrijednost: (r) => (
                    <Link
                      className="underline-offset-2 hover:underline"
                      href={`/admin/poslovanje?godina=${d.godina}&mjesec=${r.mjesec}`}
                    >
                      {MJESECI[r.mjesec - 1]}
                      {r.procjena ? " *" : ""}
                    </Link>
                  ),
                },
                { naslov: "Neto", vrijednost: (r) => formatPrice(r.neto), desno: true },
                { naslov: "Zarada", vrijednost: (r) => formatPrice(r.zarada), desno: true },
                { naslov: "Marža", vrijednost: (r) => procenat(r.marza), desno: true },
                { naslov: "Narudžbi", vrijednost: (r) => r.narudzbi, desno: true },
                { naslov: "Kupaca", vrijednost: (r) => r.aktivniKupci, desno: true },
                { naslov: "Pretplata", vrijednost: (r) => r.aktivnePretplate ?? "—", desno: true },
              ]}
            />
            {d.mjeseci.some((m) => m.procjena) && (
              <p className="text-xs text-muted">* procjena — zarada po današnjim nabavnim cijenama.</p>
            )}
          </Odjeljak>

          <Odjeljak naslov="Sezona po kategoriji" opis="Prihod po kategoriji robe, svaki mjesec (Špajz i sadržaj korpica zajedno).">
            <Tabela kljuc={(r) => r.kategorija} redovi={d.sezona} kolone={koloneSezone} prazno="Nema prodaje u ovoj godini." />
          </Odjeljak>
        </>
      )}
    </div>
  );
}
