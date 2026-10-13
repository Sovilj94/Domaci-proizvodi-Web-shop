"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import type * as LType from "leaflet";
import { MapPin } from "lucide-react";
import "leaflet/dist/leaflet.css";
import { fieldClass } from "@/components/ui/field";
import { geocodeApi } from "@/lib/api/endpoints";
import type { GeocodeHit } from "@/types/domain";
import { cn } from "@/lib/utils";

/**
 * Address field with a small OpenStreetMap map underneath (Leaflet tiles).
 * Type an address → it's geocoded and pinned on the map so you can confirm it;
 * if it's off, drag the pin or click the map to correct it and the field updates
 * to match. Keyless and free — no Google Maps API key required.
 *
 * Geokodiranje ide kroz NAŠ backend (`geocodeApi`), nikad direktno na Nominatim:
 * inače bi adresa svakog kupca išla trećoj strani sa IP-a tog kupca, na svako
 * kucanje. Ne vraćati `fetch` na nominatim.openstreetmap.org ovdje.
 */

const BANJA_LUKA: [number, number] = [44.7722, 17.191];

/** Pogodak koji ne uspije nije greška ekrana — polje ostaje kako je, bez pina. */
async function geocode(query: string): Promise<GeocodeHit | null> {
  try {
    return await geocodeApi.search(query);
  } catch {
    return null;
  }
}

async function reverse(lat: number, lon: number): Promise<GeocodeHit | null> {
  try {
    return await geocodeApi.reverse(lat, lon);
  } catch {
    return null;
  }
}

export function AddressPicker({
  value,
  onChange,
  onLocalities,
  onCoordinates,
  initialCoordinates,
  id = "address",
  querySuffix = "",
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
  /**
   * Tačka koja je već negdje sačuvana (npr. magacin iz baze): pin i pogled mape polaze
   * od nje umjesto od centra Banje Luke, da ekran pokaže gdje je lokacija *sada*.
   * Čita se jednom, na mount, i **ne** emituje se kroz `onCoordinates` — vlasnik te
   * vrijednosti je ionako već ima, a emitovanje bi se sudarilo sa poništavanjem iznad.
   * Zato ova tačka i ukida geokodiranje polaznog teksta (vidi `polazniTekst`): inače bi
   * mount odmah prijavio `null`, pa eventualno tuđu tačku za istu adresu.
   */
  initialCoordinates?: { lat: number; lon: number };
  id?: string;
  /**
   * Tekst koji se dodaje SAMO geokodirajućem upitu (npr. kućni broj iz posebnog polja u
   * checkoutu) — nikad prikazanoj vrijednosti polja niti onChange-u. Namjerno odvojeno
   * od `value`: kad bi input prikazivao "ulica broj" i onChange upisivao taj cijeli
   * tekst nazad, svaka izmjena ulice bi ponovo zalijepila stari broj na kraj (dvosmjerno
   * vezivanje koje samo sebe truje). Ovako input ostaje vezan isključivo za ulicu, a broj
   * utiče samo na to gdje se traži pin.
   */
  querySuffix?: string;
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
  // Pročitano jednom, na mount: kasnije promjene ne smiju vraćati pin natrag pod rukom
  // korisniku koji ga je upravo pomjerio.
  const pocetnaTacka = useRef(initialCoordinates);
  // Holds the exact address text we ourselves wrote from the map, so the geocode effect
  // below does not re-geocode (and re-null) our own reverse-geocoded value. Keyed to the
  // value rather than a bare boolean: a second click near the first returns the same
  // display_name, so `value` never changes, the effect never runs, and a boolean would
  // stay armed — swallowing the NEXT real edit of the address, pin and all.
  const suppressFor = useRef<string | null>(null);
  // Polazni tekst uz `initialCoordinates`: taj par (tekst + tačka) je neko ranije
  // sačuvao, pa ga efekat ispod ne smije geokodirati — poslao bi `onCoordinates(null)`
  // i onda, u najboljem slučaju suvišno, tražio tuđu tačku za istu adresu; u najgorem
  // tiho pomjeri ili izgubi pin koji je kupac ručno postavio.
  //
  // Zašto zaseban ref a ne `suppressFor` iznad: ono se troši pri prvom prolazu, a React
  // u razvoju (StrictMode) montira i pušta efekte dvaput — drugi prolaz bi zatekao
  // razoružan ref i ipak obrisao tačku. Ovo je idempotentno: dok je tekst isti kao
  // polazni, prolaz se preskače koliko god puta došao. Prva izmjena teksta ga gasi
  // zauvijek, pa i ponovo otkucan isti tekst poslije toga normalno geokodira.
  const polazniTekst = useRef<string | null>(initialCoordinates ? value : null);

  const [found, setFound] = useState<string | null>(null);
  // `sacuvana` je samo polazno stanje za tačku koja je došla iz baze: efekat ispod nad
  // polaznim tekstom ćuti (vidi `polazniTekst`), pa bi bez ovoga ekran pored pina na mapi
  // pisao „Upišite adresu iznad" kao da tačke nema. Gasi ga prva izmjena teksta ili pina.
  const [status, setStatus] = useState<
    "idle" | "searching" | "notfound" | "pinned" | "sacuvana"
  >(initialCoordinates ? "sacuvana" : "idle");

  // Je li ovo polje ikad javilo tačku roditelju. Sve prijave idu kroz `javiTacku` baš
  // zbog toga.
  const preuzeoTacku = useRef(false);
  const javiTacku = useCallback((coords: { lat: number; lon: number } | null) => {
    preuzeoTacku.current = true;
    onCoordinatesRef.current?.(coords);
  }, []);

  // The pin is gone when the picker goes: whoever holds the coordinates must not keep
  // them after this field disappears (e.g. checkout switching back to a saved address).
  //
  // Ali samo ako je tačka naša. Tuđu — onu koju je roditelj dobio iz baze i dao nam kao
  // `initialCoordinates` — ovo bi obrisalo a da je niko nije ni dirao. U razvoju je to
  // odmah vidljivo: StrictMode montira, pusti čišćenje, pa montira ponovo, pa je forma
  // adresara gubila sačuvani pin čim bi se otvorila.
  useEffect(() => () => {
    if (preuzeoTacku.current) onCoordinatesRef.current?.(null);
  }, []);

  // Initialise the map once, client-side only.
  useEffect(() => {
    let cancelled = false;
    (async () => {
      const L = (await import("leaflet")).default;
      if (cancelled || !containerRef.current || mapRef.current) return;

      const pocetak: LType.LatLngExpression = pocetnaTacka.current
        ? [pocetnaTacka.current.lat, pocetnaTacka.current.lon]
        : BANJA_LUKA;
      const map = L.map(containerRef.current, { attributionControl: true }).setView(
        pocetak,
        pocetnaTacka.current ? 15 : 13,
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
      const marker = L.marker(pocetak, { draggable: true, icon }).addTo(map);

      const commitFromMap = async (lat: number, lon: number) => {
        const res = await reverse(lat, lon);
        // Unmounted while the lookup was in flight: the cleanup below already told the
        // parent to forget the point, and reporting one now would resurrect it.
        if (cancelled) return;
        if (res) {
          setFound(res.displayName);
          setStatus("idle");
          suppressFor.current = res.displayName;
          onChangeRef.current(res.displayName);
          onLocalitiesRef.current?.(res.localities);
          javiTacku({ lat, lon });
          return;
        }
        // Reverse geocoding missed, so the address text stays as typed — but the pin the
        // user dropped is still the truth about where to deliver, and leaving the previous
        // point in place would silently disagree with the marker on screen.
        setFound(null);
        setStatus("pinned");
        javiTacku({ lat, lon });
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
  // `javiTacku` je stabilna (useCallback bez zavisnosti) — stoji ovdje samo zbog
  // pravila o zavisnostima i ne ponovo pokreće inicijalizaciju mape.
  }, [javiTacku]);

  // Geocode the typed address (debounced) and move the pin to it.
  useEffect(() => {
    if (polazniTekst.current !== null) {
      // Tekst je i dalje onaj sa kojim je došla sačuvana tačka — ništa se nije desilo.
      if (polazniTekst.current === value) return;
      // Krenulo je kucanje: od sad je ovo polje kao i svako drugo.
      polazniTekst.current = null;
    }
    if (suppressFor.current === value) {
      // This exact text is ours, straight from the pin: the coordinates already match it.
      suppressFor.current = null;
      return;
    }
    // Any other text is the user editing, so an armed value is stale by definition —
    // disarm it here so it can never match a later edit that happens to retype it.
    suppressFor.current = null;
    // The address text just changed under us, so whatever point we last reported belongs
    // to the previous address. Drop it now — before the debounce, before the request — so
    // there is no window in which a submit could pair this address with the old pin. A
    // missing coordinate is recoverable (admin can set it); a wrong one is invisible.
    javiTacku(null);

    // `querySuffix` (e.g. the house number, typed in its own field) only ever widens the
    // geocoding query — it never touches `value`/`onChange`, so it can't round-trip back
    // into the street text the way baking it into `value` did.
    const q = [value.trim(), querySuffix.trim()].filter(Boolean).join(" ");
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
        const { latitude: lat, longitude: lon } = hit;
        marker.setLatLng([lat, lon]);
        map.setView([lat, lon], 16);
        setFound(hit.displayName);
        setStatus("idle");
        onLocalitiesRef.current?.(hit.localities);
        javiTacku({ lat, lon });
      } else {
        setStatus("notfound");
        setFound(null);
        // Nothing was found for this address; make sure nothing is reported for it either.
        javiTacku(null);
      }
    }, 800);
    return () => {
      stale = true;
      clearTimeout(t);
    };
  }, [value, querySuffix, javiTacku]);

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
        className="h-48 w-full overflow-hidden rounded-xl ring-1 ring-line"
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
      {status === "sacuvana" && (
        <p className="text-xs text-muted">
          📍 Sačuvana lokacija je označena na mapi. Ako nije tačna, prevucite pin ili
          kliknite na mapu.
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
