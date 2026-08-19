"use client";

import { useEffect, useRef, useState } from "react";
import type * as LType from "leaflet";
import { MapPin } from "lucide-react";
import "leaflet/dist/leaflet.css";
import { fieldClass } from "@/components/ui/field";
import { cn } from "@/lib/utils";

/**
 * Address field with a small OpenStreetMap map underneath (Leaflet + Nominatim).
 * Type an address → it's geocoded and pinned on the map so you can confirm it;
 * if it's off, drag the pin or click the map to correct it and the field updates
 * to match. Keyless and free — no Google Maps API key required.
 */

const BANJA_LUKA: [number, number] = [44.7722, 17.191];

interface NominatimAddress {
  quarter?: string;
  neighbourhood?: string;
  suburb?: string;
  city_district?: string;
}
interface NominatimHit {
  lat: string;
  lon: string;
  display_name: string;
  address?: NominatimAddress;
}

/** Candidate locality names, for best-effort delivery-zone matching. */
function localitiesOf(a?: NominatimAddress): string[] {
  if (!a) return [];
  return [a.quarter, a.neighbourhood, a.suburb, a.city_district].filter(
    (x): x is string => !!x,
  );
}

async function geocode(query: string): Promise<NominatimHit | null> {
  const url =
    "https://nominatim.openstreetmap.org/search?format=jsonv2&addressdetails=1&countrycodes=ba&limit=1&accept-language=bs&q=" +
    encodeURIComponent(query);
  const res = await fetch(url, { headers: { Accept: "application/json" } });
  if (!res.ok) return null;
  const data = (await res.json()) as NominatimHit[];
  return data[0] ?? null;
}

interface ReverseResult {
  display: string;
  localities: string[];
}
async function reverse(lat: number, lon: number): Promise<ReverseResult | null> {
  const url = `https://nominatim.openstreetmap.org/reverse?format=jsonv2&addressdetails=1&accept-language=bs&lat=${lat}&lon=${lon}`;
  const res = await fetch(url, { headers: { Accept: "application/json" } });
  if (!res.ok) return null;
  const data = (await res.json()) as { display_name?: string; address?: NominatimAddress };
  if (!data.display_name) return null;
  return { display: data.display_name, localities: localitiesOf(data.address) };
}

export function AddressPicker({
  value,
  onChange,
  onLocalities,
  onCoordinates,
  id = "address",
}: {
  value: string;
  onChange: (value: string) => void;
  /** Emits candidate neighbourhood names for best-effort delivery-zone matching. */
  onLocalities?: (localities: string[]) => void;
  /**
   * Emits the pin's coordinates so the order can be placed on the driver's map — and
   * `null` the moment they stop describing the address in the field, so a point can
   * never outlive the address it was found for.
   */
  onCoordinates?: (coords: { lat: number; lon: number } | null) => void;
  id?: string;
}) {
  const containerRef = useRef<HTMLDivElement>(null);
  const mapRef = useRef<LType.Map | null>(null);
  const markerRef = useRef<LType.Marker | null>(null);
  // Keep live references so the map handlers never go stale.
  const onChangeRef = useRef(onChange);
  onChangeRef.current = onChange;
  const onLocalitiesRef = useRef(onLocalities);
  onLocalitiesRef.current = onLocalities;
  const onCoordinatesRef = useRef(onCoordinates);
  onCoordinatesRef.current = onCoordinates;
  // Set when we update the address from the map, so the geocode effect below
  // doesn't re-geocode our own reverse-geocoded value (which would loop).
  const suppressGeocode = useRef(false);

  const [found, setFound] = useState<string | null>(null);
  const [status, setStatus] = useState<"idle" | "searching" | "notfound" | "pinned">("idle");

  // The pin is gone when the picker goes: whoever holds the coordinates must not keep
  // them after this field disappears (e.g. checkout switching back to a saved address).
  useEffect(() => () => onCoordinatesRef.current?.(null), []);

  // Initialise the map once, client-side only.
  useEffect(() => {
    let cancelled = false;
    (async () => {
      const L = (await import("leaflet")).default;
      if (cancelled || !containerRef.current || mapRef.current) return;

      const map = L.map(containerRef.current, { attributionControl: true }).setView(
        BANJA_LUKA,
        13,
      );
      L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
        maxZoom: 19,
        attribution: "© OpenStreetMap",
      }).addTo(map);

      const icon = L.divIcon({
        className: "",
        html: '<div style="font-size:28px;line-height:1;filter:drop-shadow(0 1px 1px rgba(0,0,0,.4))">📍</div>',
        iconSize: [28, 28],
        iconAnchor: [14, 26],
      });
      const marker = L.marker(BANJA_LUKA, { draggable: true, icon }).addTo(map);

      const commitFromMap = async (lat: number, lon: number) => {
        const res = await reverse(lat, lon);
        // Unmounted while the lookup was in flight: the cleanup below already told the
        // parent to forget the point, and reporting one now would resurrect it.
        if (cancelled) return;
        if (res) {
          setFound(res.display);
          setStatus("idle");
          suppressGeocode.current = true;
          onChangeRef.current(res.display);
          onLocalitiesRef.current?.(res.localities);
          onCoordinatesRef.current?.({ lat, lon });
          return;
        }
        // Reverse geocoding missed, so the address text stays as typed — but the pin the
        // user dropped is still the truth about where to deliver, and leaving the previous
        // point in place would silently disagree with the marker on screen.
        setFound(null);
        setStatus("pinned");
        onCoordinatesRef.current?.({ lat, lon });
      };

      marker.on("dragend", () => {
        const { lat, lng } = marker.getLatLng();
        void commitFromMap(lat, lng);
      });
      map.on("click", (e: LType.LeafletMouseEvent) => {
        marker.setLatLng(e.latlng);
        void commitFromMap(e.latlng.lat, e.latlng.lng);
      });

      mapRef.current = map;
      markerRef.current = marker;
      // The map often mounts before its container has final size.
      setTimeout(() => map.invalidateSize(), 0);
    })();

    return () => {
      cancelled = true;
      mapRef.current?.remove();
      mapRef.current = null;
      markerRef.current = null;
    };
  }, []);

  // Geocode the typed address (debounced) and move the pin to it.
  useEffect(() => {
    if (suppressGeocode.current) {
      suppressGeocode.current = false;
      return;
    }
    // The address text just changed under us, so whatever point we last reported belongs
    // to the previous address. Drop it now — before the debounce, before the request — so
    // there is no window in which a submit could pair this address with the old pin. A
    // missing coordinate is recoverable (admin can set it); a wrong one is invisible.
    onCoordinatesRef.current?.(null);

    const q = value.trim();
    if (q.length < 4) {
      setFound(null);
      setStatus("idle");
      return;
    }
    setStatus("searching");
    let stale = false;
    const t = setTimeout(async () => {
      const hit = await geocode(q);
      // `clearTimeout` cannot recall a request that already left. Without this the answer
      // for a previous address could arrive after the field moved on and re-attach its
      // coordinates to the new text.
      if (stale) return;
      const map = mapRef.current;
      const marker = markerRef.current;
      if (!map || !marker) return;
      if (hit) {
        const lat = Number(hit.lat);
        const lon = Number(hit.lon);
        marker.setLatLng([lat, lon]);
        map.setView([lat, lon], 16);
        setFound(hit.display_name);
        setStatus("idle");
        onLocalitiesRef.current?.(localitiesOf(hit.address));
        onCoordinatesRef.current?.({ lat, lon });
      } else {
        setStatus("notfound");
        setFound(null);
        // Nothing was found for this address; make sure nothing is reported for it either.
        onCoordinatesRef.current?.(null);
      }
    }, 800);
    return () => {
      stale = true;
      clearTimeout(t);
    };
  }, [value]);

  return (
    <div className="space-y-2">
      <div className="relative">
        <MapPin className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-brand-600" />
        <input
          id={id}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          placeholder="Ulica i broj, Banja Luka"
          className={cn(fieldClass, "pl-9")}
          autoComplete="off"
        />
      </div>

      <div
        ref={containerRef}
        className="h-48 w-full overflow-hidden rounded-xl ring-1 ring-black/10"
        style={{ zIndex: 0 }}
      />

      {status === "searching" && (
        <p className="text-xs text-muted">Tražim lokaciju na mapi…</p>
      )}
      {status === "notfound" && (
        <p className="text-xs text-honey-600">
          Adresa nije pronađena — kliknite na mapu ili prevucite pin da označite tačnu lokaciju.
        </p>
      )}
      {status === "pinned" && (
        <p className="text-xs text-muted">
          📍 Lokacija je označena na mapi. Adresu nismo prepoznali — provjerite da tekst iznad
          odgovara označenoj tački.
        </p>
      )}
      {status === "idle" && found && (
        <p className="text-xs text-muted">
          📍 <span className="font-medium text-ink">{found}</span> — je li ovo tačna adresa? Ako
          nije, prevucite pin ili kliknite na mapu.
        </p>
      )}
      {status === "idle" && !found && (
        <p className="text-xs text-muted">
          Upišite adresu iznad ili kliknite na mapu da označite lokaciju dostave.
        </p>
      )}
    </div>
  );
}
