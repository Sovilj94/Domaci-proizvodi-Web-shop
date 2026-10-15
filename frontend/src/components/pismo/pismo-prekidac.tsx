"use client";

import { useEffect, useState } from "react";
import { accountApi } from "@/lib/api/endpoints";
import { useAuth } from "@/lib/auth/auth-context";
import { cn } from "@/lib/utils";
import { postaviPismo, procitajPismo, type Pismo } from "@/lib/pismo/pismo";

/**
 * Dugme „Ћирилица" / „Latinica". Natpis je uvijek na pismu na koje se prelazi, pa nosi
 * `translate="no"` da ga preslovljavanje ne prepiše. Prebacivanje ponovo učitava stranicu:
 * povratak na latinicu bez toga bi tražio čuvanje originalnog teksta svakog čvora.
 */
export function PismoPrekidac({ className }: { className?: string }) {
  const { user } = useAuth();
  const [pismo, setPismo] = useState<Pismo>("lat");
  const [salje, setSalje] = useState(false);

  // Kolačić se čita tek na klijentu; server uvijek renderuje latinicu.
  useEffect(() => setPismo(procitajPismo()), []);

  async function prebaci() {
    const novo: Pismo = pismo === "cir" ? "lat" : "cir";
    setSalje(true);
    postaviPismo(novo);
    if (user) {
      // Pismo naloga je za mejlove; neuspjeh ne smije spriječiti promjenu na sajtu.
      await accountApi.postaviPismo(novo).catch(() => undefined);
    }
    window.location.reload();
  }

  return (
    <button
      type="button"
      translate="no"
      onClick={prebaci}
      disabled={salje}
      className={cn("transition disabled:opacity-60", className)}
    >
      {pismo === "cir" ? "Latinica" : "Ћирилица"}
    </button>
  );
}
