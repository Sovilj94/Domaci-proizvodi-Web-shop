"use client";

import { useState } from "react";
import { MapPin, Plus } from "lucide-react";
import { AddressPicker } from "@/components/shop/address-picker";
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
