"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { Lock, LockOpen } from "lucide-react";
import { adminKorpiceApi } from "@/lib/api/endpoints";
import { narudzbeRijec } from "@/lib/orders";
import { rokTekst } from "@/lib/sedmica";
import { cn, formatDateTime } from "@/lib/utils";

/**
 * Traka iznad spiska narudžbi: da li su narudžbe izabrane sedmice još otvorene (kupci ih
 * mijenjaju do roka) ili zaključene (roba ide u nabavku). Rok i broj dolaze sa servera —
 * ista pakerska lista koju koristi nabavka, pa dijele upit.
 */
export function TrakaRoka({ godina, sedmica, datum }: { godina: number; sedmica: number; datum?: string | null }) {
  const q = useQuery({
    queryKey: ["admin", "korpice", "pakerska", godina, sedmica],
    queryFn: () => adminKorpiceApi.pakerskaLista(godina, sedmica),
    refetchInterval: 60_000,
  });
  if (!q.data) return null;
  const d = q.data;
  const broj = `${d.brojNarudzbi} ${narudzbeRijec(d.brojNarudzbi)}`;
  const Ikona = d.zakljucena ? Lock : LockOpen;

  return (
    <div
      className={cn(
        "flex flex-wrap items-center gap-x-3 gap-y-1 rounded-card px-4 py-3 text-sm",
        d.zakljucena ? "bg-zinc-100 text-zinc-700" : "bg-brand-50 text-brand-800",
      )}
    >
      <Ikona className="h-4 w-4 shrink-0" />
      <span className="font-semibold">Sedmica {d.sedmica}.</span>
      {d.zakljucena ? (
        <>
          <span>Zaključeno {formatDateTime(d.rok)} · {broj}</span>
          <Link
            href={`/admin/nabavka${datum ? `?datum=${datum}` : ""}`}
            className="ml-auto font-semibold underline-offset-2 hover:underline"
          >
            Nabavka →
          </Link>
        </>
      ) : (
        <span>
          Otvoreno do {rokTekst(new Date(d.rok))} · {broj} — kupci ih još mogu mijenjati
        </span>
      )}
    </div>
  );
}
