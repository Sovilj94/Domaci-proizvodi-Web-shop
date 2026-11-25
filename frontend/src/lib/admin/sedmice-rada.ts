"use client";

import { useQuery } from "@tanstack/react-query";
import { adminApi } from "@/lib/api/endpoints";

/** Sedmica koja se pakuje i otvorena sedmica (server zna rok). Dijele je Narudžbe i Dostava. */
export function useSedmiceRada() {
  return useQuery({
    queryKey: ["admin", "sedmice-rada"],
    queryFn: () => adminApi.sedmiceRada(),
    staleTime: 60_000,
    refetchInterval: 5 * 60_000,
  });
}
