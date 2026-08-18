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
}: {
  value: string;
  onChange: (value: string) => void;
  savedAddresses: string[];
  onLocalities?: (localities: string[]) => void;
  onCoordinates?: (lat: number, lon: number) => void;
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
        />
        {savedAddresses.length > 0 && (
          <button
            type="button"
            onClick={() => setAddingNew(false)}
            className="text-sm font-medium text-brand-700 hover:underline"
          >
            ← Sačuvane adrese
          </button>
        )}
      </div>
    );
  }

  return (
    <div className="space-y-2">
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
            onChange={() => onChange(addr)}
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
