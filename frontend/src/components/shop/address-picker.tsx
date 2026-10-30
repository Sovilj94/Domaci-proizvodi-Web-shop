"use client";

import { useCallback, useEffect, useId, useRef, useState } from "react";
import type * as LType from "leaflet";
import { AlertTriangle, CheckCircle2, LocateFixed, MapPin } from "lucide-react";
import "leaflet/dist/leaflet.css";
import { fieldClass } from "@/components/ui/field";
import { geocodeApi } from "@/lib/api/endpoints";
import { redoviPrijedloga, tekstIzPogotka } from "@/lib/adresa-prijedlog";
import type { GeocodeHit } from "@/types/domain";
import { cn } from "@/lib/utils";

/**
 * Polje adrese po uzoru na farmit.rs checkout:
 *
 * 1. Kupac kuca, ispod polja iskaču **prijedlozi** („Gundulićeva 10 / Banja Luka").
 *    Izbor prijedloga upisuje adresu i postavlja tačku.
 * 2. Tačka se pokazuje na **mirnoj mapi** sa pinom, uz dugme „Izmijeni lokaciju na mapi".
 * 3. U izmjeni pin stoji u centru, a kupac pomjera mapu ispod njega; „Potvrdi lokaciju"
 *    uzima centar i iz njega čita adresu.
 *
 * Uz to „Koristi moju trenutnu lokaciju" (GPS pregledača) i „Ne možeš da pronađeš
 * adresu?" (mapa za ručno označavanje). Ko upiše tekst a ne izabere prijedlog, dobija
 * tačku iz geokodiranja upisanog teksta kad napusti polje — kao i ranije.
 *
 * Sve ide kroz NAŠ backend (`geocodeApi`), nikad direktno na Nominatim/Photon: inače bi
 * adresa svakog kupca išla trećoj strani sa IP-a tog kupca, na svako kucanje. Ne vraćati
 * `fetch` na spoljne geokodere ovdje.
 */

// Polazni pogled mape kad tačke još nema — grad dostave.
const BANJA_LUKA: [number, number] = [44.7722, 17.191];

/** Pin u bojama sajta; vrh mu je donja sredina, pa se sidri na dno. */
const PIN_SVG =
  '<svg xmlns="http://www.w3.org/2000/svg" width="34" height="44" viewBox="0 0 34 44">' +
  '<path d="M17 43C17 43 32 27.5 32 17A15 15 0 0 0 2 17C2 27.5 17 43 17 43Z" fill="#fe7e0f" stroke="#fff" stroke-width="2"/>' +
  '<circle cx="17" cy="17" r="6" fill="#fff"/></svg>';

type Tacka = { lat: number; lon: number };

type Status =
  | "idle"
  | "sacuvana" // tačka je došla iz baze uz polazni tekst
  | "izabrana" // izabran prijedlog
  | "pronadjena" // geokodiran upisani tekst (bez izbora prijedloga)
  | "rucno" // potvrđena na mapi, adresa pročitana iz tačke
  | "bezAdrese" // potvrđena na mapi, ali za tačku nema adrese
  | "nijePronadjena"
  | "lokacijaGreska";

async function bezGreske<T>(p: Promise<T>, prazno: T): Promise<T> {
  try {
    return await p;
  } catch {
    return prazno;
  }
}

export function AddressPicker({
  value,
  onChange,
  onLocalities,
  onCoordinates,
  onBroj,
  onVanPodrucja,
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
  onCoordinates?: (coords: Tacka | null) => void;
  /**
   * Ekran sa zasebnim poljem za kućni broj (checkout): polje adrese tada dobija samo
   * ulicu, a broj iz izabranog prijedloga ili tačke stiže ovdje. Zove se samo kad pogodak
   * broj zaista ima — ulica bez broja ne briše broj koji je kupac već upisao.
   */
  onBroj?: (broj: string) => void;
  /**
   * `true` kad je izabrana/označena adresa van grada dostave (`Podesavanja.GradDostave`),
   * `false` čim to prestane da važi (nova adresa, izmjena teksta). Ekran koji prima narudžbu
   * ili čuva adresu blokira se dok je `true`; birač sam prikazuje upozorenje.
   */
  onVanPodrucja?: (van: boolean) => void;
  /**
   * Tačka koja je već negdje sačuvana (npr. magacin iz baze): pin i pogled mape polaze
   * od nje. Čita se jednom, na mount, i **ne** emituje se kroz `onCoordinates` — vlasnik
   * te vrijednosti je ionako već ima. Ova tačka i ukida geokodiranje polaznog teksta
   * (vidi `polazniTekst`), inače bi mount odmah prijavio `null`.
   */
  initialCoordinates?: Tacka;
  id?: string;
  /**
   * Tekst koji se dodaje SAMO upitima geokodera (npr. kućni broj iz posebnog polja u
   * checkoutu) — nikad prikazanoj vrijednosti polja niti onChange-u. Namjerno odvojeno
   * od `value`: dvosmjerno vezivanje „ulica broj" bi pri svakoj izmjeni ulice ponovo
   * zalijepilo stari broj na kraj.
   */
  querySuffix?: string;
}) {
  const listaId = useId();
  const containerRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const mapRef = useRef<LType.Map | null>(null);
  const markerRef = useRef<LType.Marker | null>(null);
  const [mapaSpremna, setMapaSpremna] = useState(false);

  // Žive reference, da handleri mape i odgođeni pozivi nikad ne zastare.
  const onChangeRef = useRef(onChange);
  onChangeRef.current = onChange;
  const onLocalitiesRef = useRef(onLocalities);
  onLocalitiesRef.current = onLocalities;
  const onCoordinatesRef = useRef(onCoordinates);
  onCoordinatesRef.current = onCoordinates;
  const onBrojRef = useRef(onBroj);
  onBrojRef.current = onBroj;
  const onVanPodrucjaRef = useRef(onVanPodrucja);
  onVanPodrucjaRef.current = onVanPodrucja;
  const valueRef = useRef(value);
  valueRef.current = value;
  const suffixRef = useRef(querySuffix);
  suffixRef.current = querySuffix;

  const [tacka, setTacka] = useState<Tacka | null>(initialCoordinates ?? null);
  const tackaRef = useRef(tacka);
  tackaRef.current = tacka;
  const [izmjena, setIzmjena] = useState(false);
  // Gdje mapa u izmjeni polazi kad to nije trenutna tačka (GPS pregledača).
  const polaziste = useRef<Tacka | null>(null);

  const [prijedlozi, setPrijedlozi] = useState<GeocodeHit[]>([]);
  const [otvoreno, setOtvoreno] = useState(false);
  const [aktivan, setAktivan] = useState(-1);
  const [found, setFound] = useState<string | null>(null);
  const [status, setStatus] = useState<Status>(initialCoordinates ? "sacuvana" : "idle");
  const [radi, setRadi] = useState(false);
  // Grad dostave kad je trenutna adresa van njega (prazan string ako ga server nije poslao).
  const [vanPodrucja, setVanPodrucja] = useState<string | null>(null);

  // Tekst koji smo SAMI upisali u polje (iz prijedloga ili sa mape), da ga efekat ispod ne
  // shvati kao kucanje i ne obriše tačku koja mu upravo odgovara. Vezano za vrijednost a
  // ne boolean: kad je upisani tekst isti kao postojeći, efekat se ne pokrene i boolean bi
  // ostao naoružan — progutao bi SLJEDEĆU pravu izmjenu.
  const suppressFor = useRef<string | null>(null);
  // Polazni tekst uz `initialCoordinates`: taj par je neko ranije sačuvao, pa se ne
  // geokodira. Idempotentno (StrictMode pušta efekte dvaput); prva izmjena ga gasi.
  const polazniTekst = useRef<string | null>(initialCoordinates ? value : null);
  // Broj koji smo sami predali kroz `onBroj` — njegov povratak kroz `querySuffix` nije
  // kupčeva izmjena i ne smije pomjeriti tačku koju je prijedlog upravo dao.
  const brojIzPogotka = useRef<string | null>(null);
  // Je li kupac kucao od posljednjeg izbora — samo tada napuštanje polja geokodira tekst.
  const kucano = useRef(false);
  // Svaki asinhroni odgovor nosi redni broj; stariji od posljednje radnje se baca.
  const radnja = useRef(0);

  // Je li ovo polje ikad javilo tačku roditelju. Sve prijave idu kroz `javiTacku`.
  const preuzeoTacku = useRef(false);
  const javiTacku = useCallback((coords: Tacka | null) => {
    preuzeoTacku.current = true;
    setTacka(coords);
    onCoordinatesRef.current?.(coords);
  }, []);

  // Tačka nestaje sa poljem — ali samo naša, ne ona koju je roditelj dao iz baze (StrictMode
  // montira, čisti i montira ponovo, pa bi forma adresara gubila sačuvani pin čim se otvori).
  useEffect(() => () => {
    if (preuzeoTacku.current) onCoordinatesRef.current?.(null);
    // Ni blokada „van područja" ne smije nadživjeti polje (npr. povratak na sačuvanu adresu).
    onVanPodrucjaRef.current?.(false);
  }, []);

  /**
   * Područje dostave za novu tačku: `false` sa servera znači „ne dostavljamo ovdje" — pin ostaje
   * na mapi da kupac vidi gdje je, ali roditelj dobija znak da adresa ne smije dalje. `null`
   * (geokoder ne zna grad) i `true` ne blokiraju.
   */
  const procijeniPodrucje = useCallback((hit: GeocodeHit | null) => {
    const van = hit?.uPodrucjuDostave === false;
    setVanPodrucja(van ? hit?.gradDostave || "" : null);
    onVanPodrucjaRef.current?.(van);
  }, []);

  /** Upisuje tekst iz pogotka u polje (i broj u njegovo polje), bez brisanja tačke. */
  const upisiIzPogotka = useCallback((hit: GeocodeHit) => {
    const { tekst, broj } = tekstIzPogotka(hit, !!onBrojRef.current);
    if (tekst !== valueRef.current) {
      suppressFor.current = tekst;
      onChangeRef.current(tekst);
    }
    if (broj && onBrojRef.current) {
      brojIzPogotka.current = broj;
      onBrojRef.current(broj);
    }
  }, []);

  const izaberi = useCallback(
    (hit: GeocodeHit) => {
      radnja.current++;
      kucano.current = false;
      upisiIzPogotka(hit);
      javiTacku({ lat: hit.latitude, lon: hit.longitude });
      onLocalitiesRef.current?.(hit.localities);
      procijeniPodrucje(hit);
      setFound(hit.displayName);
      setStatus("izabrana");
      setRadi(false);
      setPrijedlozi([]);
      setOtvoreno(false);
      setAktivan(-1);
      setIzmjena(false);
    },
    [javiTacku, upisiIzPogotka, procijeniPodrucje],
  );

  /** Geokodira upisani tekst (bez izbora prijedloga) i postavlja tačku ako je nađe. */
  const geokodirajTekst = useCallback(async () => {
    const q = [valueRef.current.trim(), suffixRef.current.trim()].filter(Boolean).join(" ");
    if (q.length < 4) return;
    const moja = ++radnja.current;
    setRadi(true);
    const hit = await bezGreske(geocodeApi.search(q), null);
    if (moja !== radnja.current) return;
    setRadi(false);
    procijeniPodrucje(hit);
    if (hit) {
      javiTacku({ lat: hit.latitude, lon: hit.longitude });
      onLocalitiesRef.current?.(hit.localities);
      setFound(hit.displayName);
      setStatus("pronadjena");
    } else {
      javiTacku(null);
      setFound(null);
      setStatus("nijePronadjena");
    }
  }, [javiTacku, procijeniPodrucje]);

  // ---------- Mapa: pravi se jednom, na klijentu ----------
  useEffect(() => {
    let cancelled = false;
    (async () => {
      const L = (await import("leaflet")).default;
      if (cancelled || !containerRef.current || mapRef.current) return;

      const map = L.map(containerRef.current, {
        attributionControl: true,
        // Leaflet-ova inercija je po defaultu neograničena: brz potez mišem odbaci mapu
        // stotinama kilometara, pa „Potvrdi" pročita tačku u drugoj državi.
        inertiaMaxSpeed: 1200,
      }).setView(BANJA_LUKA, 13);
      L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
        maxZoom: 19,
        attribution: "© OpenStreetMap",
      }).addTo(map);

      const icon = L.divIcon({
        className: "",
        html: PIN_SVG,
        iconSize: [34, 44],
        iconAnchor: [17, 43],
      });
      markerRef.current = L.marker(BANJA_LUKA, { icon, interactive: false });
      mapRef.current = map;
      setMapaSpremna(true);
    })();

    return () => {
      cancelled = true;
      mapRef.current?.remove();
      mapRef.current = null;
      markerRef.current = null;
      setMapaSpremna(false);
    };
  }, []);

  // Mirna mapa sa pinom ↔ mapa koja se pomjera ispod fiksnog pina.
  useEffect(() => {
    const map = mapRef.current;
    const marker = markerRef.current;
    if (!mapaSpremna || !map || !marker) return;
    if (!tacka && !izmjena) return;

    const kontrole = [
      map.dragging,
      map.touchZoom,
      map.doubleClickZoom,
      map.scrollWheelZoom,
      map.boxZoom,
      map.keyboard,
    ];
    if (izmjena) {
      kontrole.forEach((k) => k.enable());
      map.zoomControl.addTo(map);
      marker.remove();
      const p = polaziste.current ?? tacka;
      polaziste.current = null;
      // Visina kontejnera se upravo promijenila — Leaflet to mora znati prije pomjeranja.
      map.invalidateSize();
      map.setView(p ? [p.lat, p.lon] : BANJA_LUKA, p ? 17 : 13);
    } else if (tacka) {
      kontrole.forEach((k) => k.disable());
      map.zoomControl.remove();
      map.invalidateSize();
      marker.setLatLng([tacka.lat, tacka.lon]).addTo(map);
      map.setView([tacka.lat, tacka.lon], 16);
    }
  }, [mapaSpremna, tacka, izmjena]);

  // ---------- Kucanje: prijedlozi ----------
  useEffect(() => {
    if (polazniTekst.current !== null) {
      if (polazniTekst.current === value) return;
      polazniTekst.current = null;
    }
    if (suppressFor.current === value) {
      // Ovaj tekst smo upisali mi, iz pogotka — tačka mu već odgovara.
      suppressFor.current = null;
      return;
    }
    suppressFor.current = null;
    radnja.current++;
    // Tekst se promijenio, pa tačka pripada staroj adresi. Briše se odmah — prije
    // debounce-a i prije zahtjeva — da submit ne upari novu adresu sa starim pinom.
    // Tačka koja fali se može dopuniti (admin); pogrešna je nevidljiva.
    javiTacku(null);
    procijeniPodrucje(null);
    setFound(null);
    setStatus("idle");
    setRadi(false);

    if (value.trim().length < 3) {
      setPrijedlozi([]);
      setOtvoreno(false);
      return;
    }
    const q = [value.trim(), suffixRef.current.trim()].filter(Boolean).join(" ");
    const kontrola = new AbortController();
    const t = setTimeout(async () => {
      const lista = await bezGreske(geocodeApi.suggest(q, kontrola.signal), [] as GeocodeHit[]);
      if (kontrola.signal.aborted) return;
      setPrijedlozi(lista);
      setAktivan(-1);
      setOtvoreno(document.activeElement === inputRef.current);
    }, 300);
    return () => {
      clearTimeout(t);
      kontrola.abort();
    };
  }, [value, javiTacku, procijeniPodrucje]);

  // Mount sa tekstom a bez sačuvane tačke (npr. admin postavlja lokaciju narudžbe): pin
  // se traži odmah, kao i ranije, da mapa ne bude prazna. Ref se gasi pri prvom prolazu,
  // a `radnja` baca odgovor ako drugi (StrictMode) prolaz ipak stigne.
  const pocetnoGeokodiranje = useRef(!initialCoordinates && value.trim().length >= 4);
  useEffect(() => {
    if (!pocetnoGeokodiranje.current) return;
    pocetnoGeokodiranje.current = false;
    void geokodirajTekst();
  }, [geokodirajTekst]);

  // Kućni broj iz zasebnog polja: kad ga kupac promijeni, tačka se traži ponovo za
  // „ulica broj". Broj koji smo sami predali iz pogotka se preskače.
  const prethodniSuffix = useRef(querySuffix);
  useEffect(() => {
    if (prethodniSuffix.current === querySuffix) return;
    prethodniSuffix.current = querySuffix;
    if (brojIzPogotka.current !== null && brojIzPogotka.current === querySuffix) return;
    brojIzPogotka.current = null;
    if (valueRef.current.trim().length < 3) return;
    const t = setTimeout(() => void geokodirajTekst(), 800);
    return () => clearTimeout(t);
  }, [querySuffix, geokodirajTekst]);

  // ---------- Radnje ----------
  const potvrdiLokaciju = async () => {
    const map = mapRef.current;
    if (!map) return;
    const c = map.getCenter();
    const nova = { lat: c.lat, lon: c.lng };
    const moja = ++radnja.current;
    kucano.current = false;
    setIzmjena(false);
    // Tačka koju je kupac potvrdio je istina o mjestu dostave — javlja se prije čitanja
    // adrese, pa ostaje i kad za nju adrese nema.
    javiTacku(nova);
    procijeniPodrucje(null);
    setStatus("bezAdrese");
    setFound(null);
    setRadi(true);
    const res = await bezGreske(geocodeApi.reverse(nova.lat, nova.lon), null);
    if (moja !== radnja.current) return;
    setRadi(false);
    procijeniPodrucje(res);
    if (res) {
      upisiIzPogotka(res);
      onLocalitiesRef.current?.(res.localities);
      setFound(res.displayName);
      setStatus("rucno");
    }
  };

  const mojaLokacija = () => {
    if (!navigator.geolocation) {
      setStatus("lokacijaGreska");
      return;
    }
    setRadi(true);
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        setRadi(false);
        polaziste.current = { lat: pos.coords.latitude, lon: pos.coords.longitude };
        setIzmjena(true);
      },
      () => {
        setRadi(false);
        setStatus("lokacijaGreska");
      },
      { enableHighAccuracy: true, timeout: 10000 },
    );
  };

  const onKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (!otvoreno || prijedlozi.length === 0) return;
    if (e.key === "ArrowDown") {
      e.preventDefault();
      setAktivan((i) => (i + 1) % prijedlozi.length);
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      setAktivan((i) => (i <= 0 ? prijedlozi.length - 1 : i - 1));
    } else if (e.key === "Enter" && aktivan >= 0) {
      // Inače bi Enter u formi poslao narudžbu umjesto da izabere adresu.
      e.preventDefault();
      izaberi(prijedlozi[aktivan]);
    } else if (e.key === "Escape") {
      setOtvoreno(false);
    }
  };

  const mapaVidljiva = !!tacka || izmjena;
  const listaOtvorena = otvoreno && prijedlozi.length > 0;

  return (
    <div className="space-y-2">
      <div className="relative">
        <MapPin className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-honey-500" />
        <input
          ref={inputRef}
          id={id}
          value={value}
          onChange={(e) => {
            kucano.current = true;
            onChange(e.target.value);
          }}
          onKeyDown={onKeyDown}
          onFocus={() => prijedlozi.length > 0 && setOtvoreno(true)}
          onBlur={() => {
            setOtvoreno(false);
            // Upisan tekst bez izabranog prijedloga: tačka se traži za sam tekst.
            if (kucano.current && !tackaRef.current) {
              kucano.current = false;
              void geokodirajTekst();
            }
          }}
          placeholder={onBroj ? "Ime ulice" : "Ime ulice i broj"}
          className={cn(fieldClass, "pl-9")}
          autoComplete="off"
          role="combobox"
          aria-expanded={listaOtvorena}
          aria-controls={listaId}
          aria-autocomplete="list"
          aria-activedescendant={listaOtvorena && aktivan >= 0 ? `${listaId}-${aktivan}` : undefined}
        />

        {listaOtvorena && (
          <ul
            id={listaId}
            role="listbox"
            className="absolute inset-x-0 top-full z-[1000] mt-1 overflow-hidden rounded-xl border border-line bg-surface shadow-lg"
          >
            {prijedlozi.map((p, i) => {
              const { naslov, podnaslov } = redoviPrijedloga(p);
              return (
                <li
                  key={`${p.latitude},${p.longitude},${naslov}`}
                  id={`${listaId}-${i}`}
                  role="option"
                  aria-selected={i === aktivan}
                  // mousedown, ne click: click bi stigao poslije blur-a koji zatvara listu.
                  onMouseDown={(e) => {
                    e.preventDefault();
                    izaberi(p);
                  }}
                  onMouseEnter={() => setAktivan(i)}
                  className={cn(
                    "flex cursor-pointer items-start gap-3 border-b border-line/60 px-3.5 py-2.5 last:border-b-0",
                    i === aktivan ? "bg-sand" : "bg-surface",
                  )}
                >
                  <MapPin className="mt-0.5 h-4 w-4 shrink-0 text-ink" />
                  <span className="min-w-0 flex-1">
                    <span
                      className={cn(
                        "block truncate text-sm font-semibold",
                        p.uPodrucjuDostave === false ? "text-muted" : "text-ink",
                      )}
                    >
                      {naslov}
                    </span>
                    {podnaslov && (
                      <span className="block truncate text-xs text-muted">{podnaslov}</span>
                    )}
                  </span>
                  {p.uPodrucjuDostave === false && (
                    <span className="shrink-0 self-center rounded-full bg-red-50 px-2 py-0.5 text-[11px] font-medium text-red-700">
                      Ne dostavljamo
                    </span>
                  )}
                </li>
              );
            })}
          </ul>
        )}
      </div>

      <div className="flex flex-wrap items-center justify-between gap-x-4 gap-y-1">
        <button
          type="button"
          onClick={mojaLokacija}
          className="inline-flex items-center gap-1.5 text-xs font-medium text-honey-600 underline-offset-2 hover:underline"
        >
          <LocateFixed className="h-3.5 w-3.5" />
          Koristi moju trenutnu lokaciju
        </button>
        {!izmjena && (
          <button
            type="button"
            onClick={() => setIzmjena(true)}
            className="text-xs font-medium text-honey-600 underline underline-offset-2"
          >
            Ne možeš da pronađeš adresu?
          </button>
        )}
      </div>

      {/* Visina je na omotaču, ne na kontejneru mape: Leaflet kontejneru dodaje svoje
          klase (`leaflet-container`…), a promjena `className` bi ih React-om pregazila. */}
      <div className={cn("relative", izmjena ? "h-72" : "h-40", !mapaVidljiva && "hidden")}>
        <div
          ref={containerRef}
          className="h-full w-full overflow-hidden rounded-xl ring-1 ring-line"
          style={{ zIndex: 0 }}
        />
        {izmjena && (
          <>
            {/* Pin stoji, mapa se pomjera — vrh pina je tačno centar mape. */}
            <div
              aria-hidden
              className="pointer-events-none absolute left-1/2 top-1/2 z-[500] -translate-x-1/2 -translate-y-full"
              dangerouslySetInnerHTML={{ __html: PIN_SVG }}
            />
            <div className="absolute inset-x-0 bottom-7 z-[500] flex justify-center gap-2 px-3">
              {tacka && (
                <button
                  type="button"
                  onClick={() => setIzmjena(false)}
                  className="rounded-xl bg-surface px-4 py-2 text-sm font-medium text-ink shadow-md ring-1 ring-line"
                >
                  Odustani
                </button>
              )}
              <button
                type="button"
                onClick={() => void potvrdiLokaciju()}
                className="inline-flex items-center gap-2 rounded-xl bg-honey-500 px-5 py-2 text-sm font-semibold text-white shadow-md hover:bg-honey-600"
              >
                <CheckCircle2 className="h-4 w-4" />
                Potvrdi lokaciju
              </button>
            </div>
          </>
        )}
      </div>

      {vanPodrucja !== null && !izmjena && (
        <div
          role="alert"
          className="flex items-start gap-2.5 rounded-xl border border-red-200 bg-red-50 px-3.5 py-3 text-sm text-red-800"
        >
          <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
          <span>
            <span className="block font-semibold">Ne dostavljamo na ovu adresu.</span>
            {vanPodrucja
              ? `Dostava je moguća samo na području grada ${vanPodrucja}. Izaberi adresu u tom gradu.`
              : "Ova adresa je van našeg područja dostave. Izaberi drugu adresu."}
          </span>
        </div>
      )}

      {tacka && !izmjena && (
        <div className="flex flex-col items-center gap-2 pt-1 text-center">
          <p className="text-xs text-muted">Želiš li da izmijeniš tačnu lokaciju na mapi?</p>
          <button
            type="button"
            onClick={() => setIzmjena(true)}
            className="inline-flex items-center gap-2 rounded-xl border border-honey-500 px-4 py-2 text-sm font-medium text-honey-600 hover:bg-honey-100"
          >
            <MapPin className="h-4 w-4" />
            Izmijeni lokaciju na mapi
          </button>
        </div>
      )}

      <p className="text-xs text-muted" aria-live="polite">
        {radi
          ? "Tražim lokaciju…"
          : izmjena
            ? "Pomjeri mapu tako da pin pokazuje tačno mjesto dostave, pa potvrdi."
            : status === "nijePronadjena"
              ? "Adresu nismo pronašli na mapi — izaberi je sa liste prijedloga ili je označi na mapi."
              : status === "lokacijaGreska"
                ? "Ne možemo da pročitamo tvoju lokaciju — dozvoli pristup lokaciji u pregledaču ili upiši adresu."
                : status === "bezAdrese"
                  ? "📍 Lokacija je označena. Adresu za tu tačku nismo prepoznali — provjeri da tekst iznad odgovara pinu."
                  : vanPodrucja !== null
                    ? null
                    : status === "pronadjena" && found
                    ? `📍 ${found} — je li ovo tačno mjesto? Ako nije, izmijeni lokaciju na mapi.`
                    : status === "sacuvana"
                      ? "📍 Sačuvana lokacija je označena na mapi."
                      : !tacka
                        ? "Počni da kucaš ulicu i izaberi adresu sa liste."
                        : null}
      </p>
    </div>
  );
}
