"use client";

import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { AddressPicker } from "@/components/shop/address-picker";
import { Button } from "@/components/ui/button";
import { useToast } from "@/components/ui/toast";
import { adminApi } from "@/lib/api/endpoints";

/**
 * Ručno postavljanje koordinata narudžbi koju geokodiranje nije pogodilo.
 * Adresa se ne mijenja — mijenja se samo tačka na mapi.
 */
export function StopLocationDrawer({
  orderId,
  orderCode,
  address,
  onClose,
}: {
  orderId: string;
  orderCode: string;
  address: string;
  onClose: () => void;
}) {
  const qc = useQueryClient();
  const { show } = useToast();
  const [coords, setCoords] = useState<{ lat: number; lon: number } | null>(null);
  const [tekst, setTekst] = useState(address);

  const save = useMutation({
    mutationFn: () => {
      if (!coords) throw new Error("Nema odabrane tačke.");
      return adminApi.setOrderCoordinates(orderId, coords.lat, coords.lon);
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin", "routes"] });
      show("Lokacija je sačuvana");
      onClose();
    },
    onError: () => show("Lokacija nije sačuvana"),
  });

  return (
    <div className="fixed inset-0 z-50 flex items-end justify-center bg-black/40 sm:items-center">
      <div className="w-full max-w-lg rounded-card bg-white p-5 shadow-lg">
        <h2 className="font-display text-lg font-semibold">Postavi lokaciju</h2>
        <p className="text-sm text-muted">
          {orderCode} · pomjeri pin na tačno mjesto dostave
        </p>

        <div className="mt-4">
          <AddressPicker
            value={tekst}
            onChange={setTekst}
            onCoordinates={(lat, lon) => setCoords({ lat, lon })}
          />
        </div>

        <div className="mt-4 flex justify-end gap-2">
          <Button variant="secondary" onClick={onClose}>
            Odustani
          </Button>
          <Button disabled={!coords || save.isPending} onClick={() => save.mutate()}>
            {save.isPending ? "Čuvanje…" : "Sačuvaj lokaciju"}
          </Button>
        </div>
      </div>
    </div>
  );
}
