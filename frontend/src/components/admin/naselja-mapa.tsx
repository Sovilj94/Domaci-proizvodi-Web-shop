"use client";

import { useEffect, useRef, useState } from "react";
import "leaflet/dist/leaflet.css";
import type * as Leaflet from "leaflet";
import type { NaseljeAdmin } from "@/types/domain";
import {
  dodajNaIvicu,
  procitajGranicu,
  type Tacka,
} from "@/lib/naselja-granica";

const BANJA_LUKA: [number, number] = [44.7722, 17.191];

/**
 * Mapa Banje Luke sa svim naseljima. Aktivno naselje je puna površina, isključeno isprekidano.
 * Izabranom naselju se granica uređuje:
 * - teme se vuče mišem;
 * - klik na mapu dodaje teme na najbližu ivicu (ili na kraj kod nove granice);
 * - desni klik na teme ga briše.
 * Mapa ništa ne čuva — javlja novi prsten kroz `onPromjena`, a stranica odlučuje šta sa njim.
 */
export function NaseljaMapa({
  naselja,
  izabranoId,
  prsten,
  onIzaberi,
  onPromjena,
}: {
  naselja: NaseljeAdmin[];
  izabranoId: string | null;
  /** Prsten koji se uređuje ([lat, lon]); null = ništa se ne uređuje. */
  prsten: Tacka[] | null;
  onIzaberi: (id: string) => void;
  onPromjena: (prsten: Tacka[]) => void;
}) {
  const containerRef = useRef<HTMLDivElement>(null);
  const mapRef = useRef<Leaflet.Map | null>(null);
  const LRef = useRef<typeof Leaflet | null>(null);
  const slojNaseljaRef = useRef<Leaflet.LayerGroup | null>(null);
  const slojUredjivanjaRef = useRef<Leaflet.LayerGroup | null>(null);
  const [spremna, setSpremna] = useState(false);

  // Najnoviji prsten i povratni pozivi za Leaflet-ove handlere, bez ponovnog kačenja.
  const prstenRef = useRef(prsten);
  prstenRef.current = prsten;
  const onPromjenaRef = useRef(onPromjena);
  onPromjenaRef.current = onPromjena;

  useEffect(() => {
    let otkazano = false;
    (async () => {
      const L = (await import("leaflet")).default;
      if (otkazano || !containerRef.current || mapRef.current) return;
      const map = L.map(containerRef.current, {
        inertiaMaxSpeed: 1200,
      }).setView(BANJA_LUKA, 12);
      L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
        maxZoom: 19,
        attribution: "© OpenStreetMap",
      }).addTo(map);
      map.on("click", (e: Leaflet.LeafletMouseEvent) => {
        const p = prstenRef.current;
        if (!p) return;
        const t: Tacka = [round(e.latlng.lat), round(e.latlng.lng)];
        onPromjenaRef.current(p.length < 3 ? [...p, t] : dodajNaIvicu(p, t));
      });
      LRef.current = L;
      mapRef.current = map;
      slojNaseljaRef.current = L.layerGroup().addTo(map);
      slojUredjivanjaRef.current = L.layerGroup().addTo(map);
      setSpremna(true);
    })();
    return () => {
      otkazano = true;
      mapRef.current?.remove();
      mapRef.current = null;
      setSpremna(false);
    };
  }, []);

  // Sva naselja.
  useEffect(() => {
    const L = LRef.current;
    const sloj = slojNaseljaRef.current;
    if (!spremna || !L || !sloj) return;
    sloj.clearLayers();
    for (const n of naselja) {
      if (n.id === izabranoId && prsten) continue; // izabrano crta sloj uređivanja
      const dijelovi = procitajGranicu(n.granica);
      if (!dijelovi.length) continue;
      const latLng = dijelovi.map((dio) =>
        dio.map((r) => r.map(([lon, lat]) => [lat, lon] as [number, number])),
      );
      const izabrano = n.id === izabranoId;
      const poly = L.polygon(latLng, {
        color: n.isActive ? "#4d7c0f" : "#78716c",
        weight: izabrano ? 3 : 1.5,
        dashArray: n.isActive ? undefined : "6 4",
        fillOpacity: n.isActive ? (izabrano ? 0.35 : 0.15) : 0.04,
      });
      poly.bindTooltip(
        `${n.name}${n.otvorenihNarudzbi ? ` · ${n.otvorenihNarudzbi} otvorenih` : ""}${
          n.vanGranica ? ` · ${n.vanGranica} van granica` : ""
        }`,
        { sticky: true },
      );
      poly.on("click", (e) => {
        if (prstenRef.current) return; // tokom uređivanja klik dodaje tačku, ne mijenja izbor
        L.DomEvent.stopPropagation(e);
        onIzaberi(n.id);
      });
      poly.addTo(sloj);
    }
  }, [spremna, naselja, izabranoId, prsten, onIzaberi]);

  // Granica koja se uređuje: poligon + temena koja se vuku.
  useEffect(() => {
    const L = LRef.current;
    const sloj = slojUredjivanjaRef.current;
    if (!spremna || !L || !sloj) return;
    sloj.clearLayers();
    if (!prsten) return;

    if (prsten.length >= 2)
      L.polygon(prsten, {
        color: "#b45309",
        weight: 2,
        fillOpacity: 0.2,
        interactive: false,
      }).addTo(sloj);

    const ikona = L.divIcon({
      className: "",
      html: '<div style="width:14px;height:14px;border-radius:9999px;background:#fff;border:3px solid #b45309"></div>',
      iconSize: [14, 14],
      iconAnchor: [7, 7],
    });
    prsten.forEach((t, i) => {
      const m = L.marker(t, { icon: ikona, draggable: true, keyboard: false });
      m.on("dragend", () => {
        const p = m.getLatLng();
        const novi = [...prsten];
        novi[i] = [round(p.lat), round(p.lng)];
        onPromjenaRef.current(novi);
      });
      m.on("contextmenu", () => {
        if (prsten.length <= 3) return;
        onPromjenaRef.current(prsten.filter((_, j) => j !== i));
      });
      m.addTo(sloj);
    });
  }, [spremna, prsten]);

  // Na izbor naselja mapa se centrira na njega.
  useEffect(() => {
    const L = LRef.current;
    const map = mapRef.current;
    if (!spremna || !L || !map || !izabranoId) return;
    const n = naselja.find((x) => x.id === izabranoId);
    const dijelovi = procitajGranicu(n?.granica);
    if (!dijelovi.length) return;
    const tacke = dijelovi.flatMap((d) =>
      d.flatMap((r) => r.map(([lon, lat]) => [lat, lon] as [number, number])),
    );
    map.fitBounds(L.latLngBounds(tacke), { padding: [30, 30], maxZoom: 15 });
    // Namjerno samo na promjenu izbora — ne na svaku izmjenu granice.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [spremna, izabranoId]);

  return (
    <div
      ref={containerRef}
      className="h-[420px] w-full overflow-hidden rounded-card border border-line lg:h-[620px]"
    />
  );
}

function round(x: number) {
  return Math.round(x * 1e6) / 1e6;
}
