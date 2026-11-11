"use client";

import { MapPin } from "lucide-react";
import type { UseQueryResult } from "@tanstack/react-query";
import { ApiRequestError } from "@/lib/api/client";
import type { NaseljeZaTacku } from "@/types/domain";

/**
 * Ispod mape adrese: u koje naselje pada potvrđena tačka („Naselje: Borik"), ili serverova poruka
 * da se na tu adresu ne dostavlja (sa kontaktom administratora). Kupac naselje ne bira — sistem ga
 * određuje, a server isto provjerava i pri naručivanju.
 */
export function NaseljeOznaka({
  naselje,
  imaTacku,
}: {
  naselje: UseQueryResult<NaseljeZaTacku>;
  imaTacku: boolean;
}) {
  if (!imaTacku) {
    return <p className="text-xs text-muted">Potvrdi lokaciju na mapi da odredimo naselje.</p>;
  }
  if (naselje.isPending) return <p className="text-xs text-muted">Određujem naselje…</p>;
  if (naselje.isError) {
    const poruka =
      naselje.error instanceof ApiRequestError
        ? naselje.error.message
        : "Naselje trenutno ne mogu da odredim. Pokušaj ponovo.";
    return (
      <p role="alert" className="rounded-lg bg-red-50 px-3 py-2 text-xs font-medium text-red-700">
        {poruka}
      </p>
    );
  }
  return (
    <p className="flex items-center gap-1.5 text-sm text-brand-700">
      <MapPin className="h-4 w-4" /> Naselje: <span className="font-semibold">{naselje.data.name}</span>
    </p>
  );
}
