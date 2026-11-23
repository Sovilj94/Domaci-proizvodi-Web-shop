"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { MapPin, Plus, UserRound } from "lucide-react";
import { AddressPicker } from "@/components/shop/address-picker";
import { Field, Input } from "@/components/ui/field";
import { geocodeApi } from "@/lib/api/endpoints";
import { ranijeBezAdresara } from "@/lib/checkout-adrese";
import { cn } from "@/lib/utils";
import type { Address } from "@/types/domain";

export type Primalac = { ime: string; telefon: string };
/** Unos u režimu „Naruči za nekog drugog". Telefon je lokalni broj, bez +387. */
export type ZaDrugogUnos = { naziv: string; ime: string; telefon: string };

type Rezim = "lista" | "nova" | "zaDrugog";

const kljuc = (s: string) => s.trim().toLowerCase();

/**
 * Adresa u checkoutu. Prijavljeni kupac vidi svoj adresar (sa nazivom i primaocem), pa
 * ranije adrese iz narudžbi koje nisu u adresaru. Ispod su „Nova adresa" (za sebe) i
 * „Naruči za nekog drugog" (mapa + ime i telefon primaoca). Gost dobija samo mapu.
 *
 * Primalac putuje gore kroz `onPrimalac` (izabrana adresa iz adresara koja ga ima) i
 * `onZaDrugog` (ono što kupac kuca u režimu „za nekog drugog"). Svaki drugi izbor javlja
 * `null` za oba — primalac ne smije preći na adresu koja nije njegova.
 */
export function AddressChooser({
  value,
  onChange,
  savedAddresses,
  adresar = [],
  mozeZaDrugog = false,
  onPrimalac,
  onZaDrugog,
  onLocalities,
  onCoordinates,
  querySuffix,
  onBroj,
  onVanPodrucja,
  id,
}: {
  value: string;
  onChange: (value: string) => void;
  /** Adrese ranijih isporučenih narudžbi (samo tekst). */
  savedAddresses: string[];
  /** Kupčev adresar (`/account/addresses`) — adrese sa nazivom, tačkom i možda primaocem. */
  adresar?: Address[];
  /** Prijavljeni kupac — samo on može naručiti za nekog drugog. */
  mozeZaDrugog?: boolean;
  onPrimalac?: (p: Primalac | null) => void;
  onZaDrugog?: (u: ZaDrugogUnos | null) => void;
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
  const ranije = useMemo(() => ranijeBezAdresara(adresar, savedAddresses), [adresar, savedAddresses]);
  const imaListu = adresar.length > 0 || ranije.length > 0;
  const [rezim, setRezim] = useState<Rezim>("lista");
  const showMap = rezim !== "lista" || !imaListu;

  const [unos, setUnos] = useState<ZaDrugogUnos>({ naziv: "", ime: "", telefon: "" });

  const refs = useRef({ onCoordinates, onVanPodrucja, onPrimalac, onZaDrugog });
  useEffect(() => {
    refs.current = { onCoordinates, onVanPodrucja, onPrimalac, onZaDrugog };
  });

  // Izabrana stavka liste: prvo adresar (ima tačku i možda primaoca), pa ranija adresa.
  const izAdresara = adresar.find((a) => kljuc(a.fullAddress) === kljuc(value));
  const izRanijih = !izAdresara && ranije.includes(value);
  const sacuvanaIzabrana = !showMap && (!!izAdresara || izRanijih);
  const tackaAdresara =
    izAdresara?.latitude != null && izAdresara?.longitude != null
      ? { lat: izAdresara.latitude, lon: izAdresara.longitude }
      : null;

  // Sačuvana adresa mora imati tačku, inače checkout ne zna naselje i ne pušta dalje.
  // Adresa iz adresara nosi svoju; ranija adresa (samo tekst) se geokodira preko `/geocode`.
  // Ako je geokoder ne nađe, kupac je potvrđuje na mapi.
  const [lokacija, setLokacija] = useState<"trazim" | "nadjena" | "nema">("trazim");
  const primalacIme = izAdresara?.primalacIme;
  const primalacTelefon = izAdresara?.primalacTelefon;
  const tLat = tackaAdresara?.lat;
  const tLon = tackaAdresara?.lon;
  useEffect(() => {
    if (!sacuvanaIzabrana) return;
    const r = refs.current;
    r.onZaDrugog?.(null);
    r.onPrimalac?.(primalacIme && primalacTelefon ? { ime: primalacIme, telefon: primalacTelefon } : null);
    r.onVanPodrucja?.(false);
    if (tLat !== undefined && tLon !== undefined) {
      r.onCoordinates?.({ lat: tLat, lon: tLon });
      setLokacija("nadjena");
      return;
    }
    let ponisteno = false;
    setLokacija("trazim");
    geocodeApi
      .search(value)
      .catch(() => null)
      .then((hit) => {
        if (ponisteno) return;
        if (hit) {
          refs.current.onCoordinates?.({ lat: hit.latitude, lon: hit.longitude });
          refs.current.onVanPodrucja?.(hit.uPodrucjuDostave === false);
          setLokacija("nadjena");
        } else {
          refs.current.onCoordinates?.(null);
          setLokacija("nema");
        }
      });
    return () => {
      ponisteno = true;
    };
  }, [sacuvanaIzabrana, value, primalacIme, primalacTelefon, tLat, tLon]);

  // Stara tačka ne smije putovati uz novi izbor dok efekat gore ne javi njegovu.
  function izaberi(adresa: string) {
    onCoordinates?.(null);
    onChange(adresa);
  }

  function predjiNa(novi: Rezim, ocistiTekst: boolean) {
    onCoordinates?.(null);
    onPrimalac?.(null);
    if (novi === "zaDrugog") onZaDrugog?.(unos);
    else onZaDrugog?.(null);
    if (ocistiTekst) onChange("");
    setRezim(novi);
  }

  function promijeniUnos(dio: Partial<ZaDrugogUnos>) {
    const novi = { ...unos, ...dio };
    setUnos(novi);
    onZaDrugog?.(novi);
  }

  const dugmeKlasa =
    "flex w-full items-center gap-2 rounded-xl border border-dashed border-black/15 px-3 py-2.5 text-sm font-medium text-brand-700 transition hover:bg-sand/50";

  if (showMap) {
    return (
      <div className="space-y-3">
        {rezim === "zaDrugog" && (
          <p className="rounded-lg bg-beige px-3 py-2 text-xs text-ink/80">
            Naručuješ za nekog drugog: korpica ide na njegovu adresu, vozač zove njega i on plaća
            pouzećem. Potvrda stiže tebi. Adresu pamtimo u tvom adresaru.
          </p>
        )}
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
        {rezim === "zaDrugog" && (
          <div className="space-y-4 rounded-xl border border-line p-4">
            <Field label="Ime i prezime primaoca" required>
              <Input
                value={unos.ime}
                onChange={(e) => promijeniUnos({ ime: e.target.value })}
                placeholder="npr. Petar Petrović"
              />
            </Field>
            <Field label="Telefon primaoca" htmlFor="checkout-primalac-telefon" required>
              <div className="flex items-stretch">
                <span className="flex shrink-0 items-center rounded-l-xl border border-r-0 border-line bg-sand px-3.5 text-sm font-medium text-muted">
                  +387
                </span>
                <Input
                  id="checkout-primalac-telefon"
                  type="tel"
                  value={unos.telefon}
                  onChange={(e) => promijeniUnos({ telefon: e.target.value })}
                  placeholder="6x xxx xxx"
                  className="min-w-0 flex-1 rounded-l-none"
                />
              </div>
            </Field>
            <Field label="Naziv adrese (opcionalno)">
              <Input
                value={unos.naziv}
                onChange={(e) => promijeniUnos({ naziv: e.target.value })}
                placeholder="npr. Mama i tata"
              />
            </Field>
          </div>
        )}
        {imaListu ? (
          <button
            type="button"
            // Povratak na listu: tačka i primalac nove adrese ostaju iza, ne prelaze na izbor.
            onClick={() => predjiNa("lista", false)}
            className="text-sm font-medium text-brand-700 hover:underline"
          >
            ← Sačuvane adrese
          </button>
        ) : (
          mozeZaDrugog &&
          rezim !== "zaDrugog" && (
            <button type="button" onClick={() => predjiNa("zaDrugog", true)} className={dugmeKlasa}>
              <UserRound className="h-4 w-4" /> Naruči za nekog drugog
            </button>
          )
        )}
        {!imaListu && rezim === "zaDrugog" && (
          <button
            type="button"
            onClick={() => predjiNa("nova", true)}
            className="text-sm font-medium text-brand-700 hover:underline"
          >
            ← Naručujem za sebe
          </button>
        )}
      </div>
    );
  }

  return (
    <div role="radiogroup" aria-label="Sačuvane adrese" className="space-y-2">
      {adresar.map((a) => (
        <Stavka
          key={a.id}
          izabrana={izAdresara?.id === a.id}
          onIzbor={() => izaberi(a.fullAddress)}
          naslov={a.label}
          adresa={a.fullAddress}
          primalac={a.primalacIme ? `${a.primalacIme}${a.primalacTelefon ? ` · ${a.primalacTelefon}` : ""}` : undefined}
        />
      ))}
      {ranije.map((addr) => (
        <Stavka key={addr} izabrana={izRanijih && value === addr} onIzbor={() => izaberi(addr)} adresa={addr} />
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
            onClick={() => predjiNa("nova", false)}
            className="font-semibold text-brand-700 underline"
          >
            Potvrdi lokaciju na mapi
          </button>
        </div>
      )}
      <button type="button" onClick={() => predjiNa("nova", true)} className={dugmeKlasa}>
        <Plus className="h-4 w-4" /> Nova adresa
      </button>
      {mozeZaDrugog && (
        <button type="button" onClick={() => predjiNa("zaDrugog", true)} className={dugmeKlasa}>
          <UserRound className="h-4 w-4" /> Naruči za nekog drugog
        </button>
      )}
    </div>
  );
}

function Stavka({
  izabrana,
  onIzbor,
  naslov,
  adresa,
  primalac,
}: {
  izabrana: boolean;
  onIzbor: () => void;
  naslov?: string;
  adresa: string;
  primalac?: string;
}) {
  return (
    <label
      className={cn(
        "flex cursor-pointer items-start gap-3 rounded-xl border p-3 text-sm transition",
        izabrana ? "border-brand-400 bg-brand-50" : "border-black/10 hover:bg-sand/50",
      )}
    >
      <input
        type="radio"
        name="saved-address"
        checked={izabrana}
        onChange={onIzbor}
        className="mt-0.5 h-4 w-4 accent-brand-600"
      />
      <span className="flex items-start gap-2">
        <MapPin className="mt-0.5 h-4 w-4 shrink-0 text-brand-600" />
        <span>
          {naslov && <span className="block font-semibold text-ink">{naslov}</span>}
          {adresa}
          {primalac && <span className="mt-0.5 block text-xs text-muted">Prima: {primalac}</span>}
        </span>
      </span>
    </label>
  );
}
