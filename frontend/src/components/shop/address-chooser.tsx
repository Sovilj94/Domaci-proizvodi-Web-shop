"use client";

import { useEffect, useRef, useState } from "react";
import { MapPin, Plus } from "lucide-react";
import { AddressPicker } from "@/components/shop/address-picker";
import { geocodeApi } from "@/lib/api/endpoints";
import { cn } from "@/lib/utils";

/**
 * Delivery-address input for checkout. A logged-in customer with past delivered
 * orders sees a list of their saved addresses (the most recent pre-selected by the
 * parent) plus a "Nova adresa" option that reveals the map picker. Guests / customers
 * with no history get the map picker directly.
 */
export function AddressChooser({
  value,
  onChange,
  savedAddresses,
  onLocalities,
  onCoordinates,
  querySuffix,
  onBroj,
  onVanPodrucja,
  id,
}: {
  value: string;
  onChange: (value: string) => void;
  savedAddresses: string[];
  onLocalities?: (localities: string[]) => void;
  /** `null` clears the parent's point — a saved address carries no pin of its own. */
  onCoordinates?: (coords: { lat: number; lon: number } | null) => void;
  /** Passed straight through to `AddressPicker` — see its own doc comment. */
  querySuffix?: string;
  /** Kućni broj iz izabranog prijedloga — vidi `AddressPicker`. */
  onBroj?: (broj: string) => void;
  /** Adresa van grada dostave — vidi `AddressPicker`. */
  onVanPodrucja?: (van: boolean) => void;
  /** `id` polja ulice na mapi, da ga labela (`Field htmlFor`) imenuje. */
  id?: string;
}) {
  const [addingNew, setAddingNew] = useState(false);
  const showMap = addingNew || savedAddresses.length === 0;

  // Sačuvana adresa je samo tekst, bez tačke — a checkout bez tačke ne zna naselje i ne
  // pušta dalje. Zato se izabrana sačuvana adresa (i ona koju roditelj unaprijed izabere)
  // geokodira preko našeg `/geocode`. Tačka je njena vlastita, ne pozajmljena od druge adrese.
  // Ako je geokoder ne nađe, kupac je potvrđuje na mapi.
  const [lokacija, setLokacija] = useState<"trazim" | "nadjena" | "nema">("trazim");
  const onCoordinatesRef = useRef(onCoordinates);
  const onVanPodrucjaRef = useRef(onVanPodrucja);
  useEffect(() => {
    onCoordinatesRef.current = onCoordinates;
    onVanPodrucjaRef.current = onVanPodrucja;
  });
  const sacuvanaIzabrana = !showMap && savedAddresses.includes(value);
  useEffect(() => {
    if (!sacuvanaIzabrana) return;
    let ponisteno = false;
    setLokacija("trazim");
    onVanPodrucjaRef.current?.(false);
    geocodeApi
      .search(value)
      .catch(() => null)
      .then((hit) => {
        if (ponisteno) return;
        if (hit) {
          onCoordinatesRef.current?.({ lat: hit.latitude, lon: hit.longitude });
          onVanPodrucjaRef.current?.(hit.uPodrucjuDostave === false);
          setLokacija("nadjena");
        } else {
          onCoordinatesRef.current?.(null);
          setLokacija("nema");
        }
      });
    return () => {
      ponisteno = true;
    };
  }, [sacuvanaIzabrana, value]);

  if (showMap) {
    return (
      <div className="space-y-2">
        <AddressPicker
          value={value}
          onChange={onChange}
          onLocalities={onLocalities}
          onCoordinates={onCoordinates}
          querySuffix={querySuffix}
          onBroj={onBroj}
          onVanPodrucja={onVanPodrucja}
          id={id}
        />
        {savedAddresses.length > 0 && (
          <button
            type="button"
            onClick={() => {
              // Back to the saved list: the pin dropped for the new address is abandoned
              // with it, so it must not travel to whichever saved address gets picked.
              onCoordinates?.(null);
              setAddingNew(false);
            }}
            className="text-sm font-medium text-brand-700 hover:underline"
          >
            ← Sačuvane adrese
          </button>
        )}
      </div>
    );
  }

  return (
    <div role="radiogroup" aria-label="Sačuvane adrese" className="space-y-2">
      {savedAddresses.map((addr) => (
        <label
          key={addr}
          className={cn(
            "flex cursor-pointer items-start gap-3 rounded-xl border p-3 text-sm transition",
            value === addr ? "border-brand-400 bg-brand-50" : "border-black/10 hover:bg-sand/50",
          )}
        >
          <input
            type="radio"
            name="saved-address"
            checked={value === addr}
            onChange={() => {
              onCoordinates?.(null);
              onChange(addr);
            }}
            className="mt-0.5 h-4 w-4 accent-brand-600"
          />
          <span className="flex items-start gap-2">
            <MapPin className="mt-0.5 h-4 w-4 shrink-0 text-brand-600" />
            {addr}
          </span>
        </label>
      ))}
      {sacuvanaIzabrana && lokacija === "trazim" && (
        <p className="text-xs text-muted">Tražim lokaciju adrese na mapi…</p>
      )}
      {sacuvanaIzabrana && lokacija === "nema" && (
        <div className="rounded-lg bg-honey-100 px-3 py-2 text-xs text-ink">
          Ovu adresu ne možemo sami da pronađemo na mapi.{" "}
          <button
            type="button"
            // Tekst adrese ostaje — kupac samo potvrđuje tačku na mapi.
            onClick={() => setAddingNew(true)}
            className="font-semibold text-brand-700 underline"
          >
            Potvrdi lokaciju na mapi
          </button>
        </div>
      )}
      <button
        type="button"
        onClick={() => {
          onCoordinates?.(null);
          onChange("");
          setAddingNew(true);
        }}
        className="flex w-full items-center gap-2 rounded-xl border border-dashed border-black/15 px-3 py-2.5 text-sm font-medium text-brand-700 transition hover:bg-sand/50"
      >
        <Plus className="h-4 w-4" /> Nova adresa
      </button>
    </div>
  );
}
