"use client";

import { useState } from "react";
import { Star } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/field";
import { useToast } from "@/components/ui/toast";
import { ApiRequestError } from "@/lib/api/client";
import type { OcjenaInput } from "@/lib/api/contracts";
import { zvjezdaKlasa } from "./zvjezdice";

const MAX_KOMENTAR = 1000;

export function OcjenaForma({
  posalji,
  onUspjeh,
}: {
  posalji: (input: OcjenaInput) => Promise<unknown>;
  onUspjeh: () => void;
}) {
  const { show } = useToast();
  const [ocjena, setOcjena] = useState(0);
  const [hover, setHover] = useState(0);
  const [komentar, setKomentar] = useState("");
  const [dozvola, setDozvola] = useState(false);
  const [saljem, setSaljem] = useState(false);
  const [gotovo, setGotovo] = useState(false);
  const [errors, setErrors] = useState<Record<string, string[]>>({});
  const [poruka, setPoruka] = useState("");

  const prikaz = hover || ocjena;

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (ocjena === 0) return;
    setErrors({});
    setPoruka("");
    setSaljem(true);
    try {
      await posalji({
        ocjena,
        komentar: komentar.trim() || undefined,
        dozvolaZaObjavu: dozvola,
      });
      setGotovo(true);
      // A10: roditelj odmah osvježi narudžbu i zamijeni formu prikazom ocjene, pa bi poruka
      // ispod nestala prije nego što je iko vidi — toast ostaje vidljiv i poslije zamjene.
      show("Hvala na ocjeni!");
      onUspjeh();
    } catch (err) {
      if (err instanceof ApiRequestError) {
        if (err.status === 409 && err.apiError.code === "vec_ocijenjeno") {
          setPoruka("Ova narudžba je već ocijenjena.");
          onUspjeh();
        } else if (err.status === 429) {
          setPoruka("Previše pokušaja, pokušaj za minut.");
        } else if (Object.keys(err.fieldErrors).length > 0) {
          setErrors(err.fieldErrors);
        } else {
          show(err.message, "error");
        }
      } else {
        show("Slanje ocjene nije uspjelo", "error");
      }
    } finally {
      setSaljem(false);
    }
  }

  if (gotovo) {
    return (
      <p role="status" className="font-display text-lg font-bold text-ink">
        Hvala na ocjeni!
      </p>
    );
  }

  const greskaOcjena = errors["input.ocjena"]?.[0];
  const greskaKomentar = errors["input.komentar"]?.[0];

  return (
    <form onSubmit={submit} className="space-y-4" noValidate>
      <div>
        <div
          role="radiogroup"
          aria-label="Ocjena isporuke"
          className="flex items-center gap-1"
          onMouseLeave={() => setHover(0)}
        >
          {[1, 2, 3, 4, 5].map((n) => (
            <div key={n} className="relative">
              <input
                type="radio"
                name="ocjena"
                id={`ocjena-${n}`}
                value={n}
                aria-label={`${n} od 5`}
                checked={ocjena === n}
                onChange={() => setOcjena(n)}
                className="peer sr-only"
              />
              <label
                htmlFor={`ocjena-${n}`}
                onMouseEnter={() => setHover(n)}
                className="block cursor-pointer rounded-md p-1 peer-focus-visible:ring-2 peer-focus-visible:ring-brand-400"
              >
                <Star
                  aria-hidden="true"
                  className={zvjezdaKlasa(n <= prikaz)}
                  width={32}
                  height={32}
                />
              </label>
            </div>
          ))}
        </div>
        {greskaOcjena && <p className="mt-1 text-xs font-medium text-red-600">{greskaOcjena}</p>}
      </div>

      <div>
        <label htmlFor="ocjena-komentar" className="mb-1.5 block text-sm font-medium text-ink">
          Komentar (nije obavezan)
        </label>
        <Textarea
          id="ocjena-komentar"
          value={komentar}
          maxLength={MAX_KOMENTAR}
          onChange={(e) => setKomentar(e.target.value)}
        />
        <p className="mt-1 text-right text-xs text-muted">
          {komentar.length}/{MAX_KOMENTAR}
        </p>
        {greskaKomentar && (
          <p className="mt-1 text-xs font-medium text-red-600">{greskaKomentar}</p>
        )}
      </div>

      <label className="flex items-start gap-2 text-sm text-ink">
        <input
          type="checkbox"
          checked={dozvola}
          onChange={(e) => setDozvola(e.target.checked)}
          className="mt-0.5 h-4 w-4 shrink-0"
        />
        <span>
          Dozvoljavam da se moj komentar objavi na sajtu, potpisan imenom i inicijalom prezimena.
        </span>
      </label>

      {poruka && (
        <p role="alert" className="text-sm font-medium text-red-700">
          {poruka}
        </p>
      )}
      <Button type="submit" disabled={ocjena === 0 || saljem}>
        {saljem ? "Šaljem…" : "Pošalji ocjenu"}
      </Button>
      {ocjena === 0 && <p className="text-xs text-muted">Izaberi ocjenu zvjezdicama.</p>}
    </form>
  );
}
