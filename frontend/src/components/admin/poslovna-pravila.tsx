"use client";

import { useEffect, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Button } from "@/components/ui/button";
import { Field, Input } from "@/components/ui/field";
import { LoadingBlock } from "@/components/ui/spinner";
import { useToast } from "@/components/ui/toast";
import { ApiRequestError } from "@/lib/api/client";
import { adminApi } from "@/lib/api/endpoints";
import { queryKeys, usePodesavanja } from "@/lib/api/hooks";
import type { Podesavanja } from "@/types/domain";

type Kljuc = keyof Podesavanja;

/** Opis polja forme — oznake interfejsa. Vrijednosti su isključivo iz baze. */
const GRUPE: { naslov: string; polja: { k: Kljuc; naziv: string; procenat?: boolean; korak?: string }[] }[] = [
  {
    naslov: "Korpice",
    polja: [
      { k: "minimalnaMarza", naziv: "Minimalna marža", procenat: true },
      { k: "koeficijentVrijednosti", naziv: "Koeficijent vrijednosti sadržaja", korak: "0.01" },
      { k: "brojZamjena", naziv: "Predloženih zamjena po sadržaju" },
      { k: "minArtikalaMala", naziv: "Najmanje artikala — mala" },
      { k: "minArtikalaSrednja", naziv: "Najmanje artikala — srednja" },
      { k: "minArtikalaVelika", naziv: "Najmanje artikala — velika" },
      { k: "rokSatiRezerva", naziv: "Rok u satima (kad nema termina dostave)" },
    ],
  },
  {
    naslov: "Pretplate",
    polja: [
      { k: "popustNedjeljna", naziv: "Popust — nedjeljni plan", procenat: true },
      { k: "popustMjesecna", naziv: "Popust — mjesečni plan", procenat: true },
      { k: "popustTriMjeseca", naziv: "Popust — plan na 3 mjeseca", procenat: true },
      { k: "obavezaMjesecna", naziv: "Obaveza mjesečnog plana (isporuka)" },
      { k: "obavezaTriMjeseca", naziv: "Obaveza plana na 3 mjeseca (isporuka)" },
      { k: "satiUnaprijed", naziv: "Narudžba nastaje sati prije roka" },
      { k: "alarmSatiPrijeRoka", naziv: "Alarm za neobjavljen sadržaj (sati prije roka)" },
      { k: "maksPauzaSedmica", naziv: "Najduža pauza (sedmica)" },
    ],
  },
  {
    naslov: "Poeni i prodavnica",
    polja: [
      { k: "poenaPoKM", naziv: "Poena po 1 KM" },
      { k: "vrijednostPoenaKM", naziv: "Vrijednost poena (KM)", korak: "0.01" },
      { k: "nagradaPreporuka", naziv: "Nagrada za preporuku (poena)" },
      { k: "pragPriKraju", naziv: "„Još samo X na stanju\" od", korak: "0.01" },
    ],
  },
];

const DANI = ["nedjelja", "ponedjeljak", "utorak", "srijeda", "četvrtak", "petak", "subota"];

const uPolje = (v: number, procenat?: boolean) => String(procenat ? Math.round(v * 1000) / 10 : v);
const izPolja = (s: string, procenat?: boolean) => {
  const n = Number(s.replace(",", "."));
  return procenat ? Math.round(n * 10) / 1000 : n;
};

/** Poslovna pravila piljare — jedan red u bazi; svaka izmjena važi odmah, bez deploya. */
export function PoslovnaPravila() {
  const q = usePodesavanja();
  const qc = useQueryClient();
  const { show } = useToast();
  const [unos, setUnos] = useState<Record<string, string>>({});
  const [greske, setGreske] = useState<Record<string, string[]>>({});

  useEffect(() => {
    if (!q.data) return;
    const u: Record<string, string> = {};
    for (const g of GRUPE) for (const p of g.polja) u[p.k] = uPolje(q.data[p.k] as number, p.procenat);
    u.danIsporukeRezerva = String(q.data.danIsporukeRezerva);
    u.vrijemeIsporukeRezerva = q.data.vrijemeIsporukeRezerva;
    u.gradDostave = q.data.gradDostave ?? "";
    setUnos(u);
  }, [q.data]);

  const sacuvaj = useMutation({
    mutationFn: () => {
      const tijelo = { ...q.data! } as Record<string, unknown>;
      for (const g of GRUPE) for (const p of g.polja) tijelo[p.k] = izPolja(unos[p.k] ?? "", p.procenat);
      tijelo.danIsporukeRezerva = Number(unos.danIsporukeRezerva);
      tijelo.vrijemeIsporukeRezerva = unos.vrijemeIsporukeRezerva;
      tijelo.gradDostave = unos.gradDostave ?? "";
      return adminApi.sacuvajPodesavanja(tijelo as unknown as Podesavanja);
    },
    onSuccess: (dto) => {
      qc.setQueryData(queryKeys.podesavanja(), dto);
      show("Poslovna pravila sačuvana");
    },
    onError: (err) => {
      if (err instanceof ApiRequestError) {
        setGreske(err.fieldErrors);
        show(Object.values(err.fieldErrors)[0]?.[0] ?? err.message, "error");
        return;
      }
      show("Čuvanje nije uspjelo", "error");
    },
  });

  const greska = (k: string) => greske[`podesavanja.${k}`]?.[0];

  return (
    <div className="space-y-4">
      <div>
        <h2 className="font-display text-xl font-bold">Poslovna pravila</h2>
        <p className="text-muted">Marža, popusti, obaveze, poeni. Izmjena važi odmah za sve nove obračune.</p>
      </div>
      {q.isLoading ? (
        <LoadingBlock />
      ) : q.isError ? (
        <p className="rounded-card bg-white p-6 text-sm text-red-600 shadow-card">Pravila nije moguće pročitati.</p>
      ) : (
        <form
          onSubmit={(e) => {
            e.preventDefault();
            setGreske({});
            sacuvaj.mutate();
          }}
          className="max-w-2xl space-y-6 rounded-card bg-white p-6 shadow-card"
        >
          {GRUPE.map((g) => (
            <fieldset key={g.naslov} className="space-y-3">
              <legend className="font-semibold text-ink">{g.naslov}</legend>
              <div className="grid gap-3 sm:grid-cols-2">
                {g.polja.map((p) => (
                  <Field key={p.k} label={p.procenat ? `${p.naziv} (%)` : p.naziv} error={greska(p.k)}>
                    <Input
                      type="number"
                      inputMode="decimal"
                      step={p.procenat ? "0.1" : (p.korak ?? "1")}
                      value={unos[p.k] ?? ""}
                      onChange={(e) => setUnos({ ...unos, [p.k]: e.target.value })}
                    />
                  </Field>
                ))}
                {g.naslov === "Korpice" && (
                  <>
                    <Field label="Dan isporuke (kad nema termina)" error={greska("danIsporukeRezerva")}>
                      <select
                        value={unos.danIsporukeRezerva ?? ""}
                        onChange={(e) => setUnos({ ...unos, danIsporukeRezerva: e.target.value })}
                        className="h-11 w-full rounded-xl border border-line bg-white px-3 text-sm"
                      >
                        {DANI.map((d, i) => (
                          <option key={d} value={i}>
                            {d}
                          </option>
                        ))}
                      </select>
                    </Field>
                    <Field label="Vrijeme isporuke (kad nema termina)" error={greska("vrijemeIsporukeRezerva")}>
                      <Input
                        value={unos.vrijemeIsporukeRezerva ?? ""}
                        onChange={(e) => setUnos({ ...unos, vrijemeIsporukeRezerva: e.target.value })}
                        placeholder="HH:MM"
                        maxLength={5}
                      />
                    </Field>
                  </>
                )}
              </div>
            </fieldset>
          ))}
          <fieldset className="space-y-3">
            <legend className="font-semibold text-ink">Dostava</legend>
            <Field label="Grad dostave" error={greska("gradDostave")}>
              <Input
                value={unos.gradDostave ?? ""}
                onChange={(e) => setUnos({ ...unos, gradDostave: e.target.value })}
                placeholder="npr. Banja Luka"
                maxLength={100}
              />
            </Field>
            <p className="text-xs text-muted">
              Kupac koji izabere adresu van ovog grada (ili njegove opštine) vidi „Ne dostavljamo na
              ovu adresu&ldquo; i ne može naručiti. Prazno polje = bez ograničenja.
            </p>
          </fieldset>
          <div className="flex justify-end">
            <Button type="submit" disabled={sacuvaj.isPending}>
              {sacuvaj.isPending ? "Spremam…" : "Spremi pravila"}
            </Button>
          </div>
        </form>
      )}
    </div>
  );
}
