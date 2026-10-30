"use client";

import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ArrowDown, ArrowUp, Plus, Trash2 } from "lucide-react";
import { SlikaPolje } from "@/components/admin/slika-polje";
import { Button } from "@/components/ui/button";
import { Field, Input, Textarea } from "@/components/ui/field";
import { LoadingBlock } from "@/components/ui/spinner";
import { useToast } from "@/components/ui/toast";
import { ApiRequestError } from "@/lib/api/client";
import { adminApi } from "@/lib/api/endpoints";
import { queryKeys } from "@/lib/api/hooks";
import { cn } from "@/lib/utils";
import { SEKCIJE_SADRZAJA, type BlokSadrzaja, type SekcijaSadrzaja } from "@/types/domain";

type Polje = "ikona" | "naslov" | "tekst" | "dugme" | "link" | "slikaUrl" | "boja";

/** Gdje se sekcija vidi na sajtu i koja polja koristi — opis interfejsa, ne podatak. */
const OPIS: Record<SekcijaSadrzaja, { naziv: string; polja: Polje[]; tekst?: string }> = {
  "pocetna-slajdovi": { naziv: "Početna — slajdovi", polja: ["naslov", "tekst", "dugme", "link", "slikaUrl"] },
  "pocetna-koraci": { naziv: "Početna — kako funkcioniše", polja: ["ikona", "naslov", "tekst"] },
  "pocetna-plocice": { naziv: "Početna — sa njive do stola", polja: ["ikona", "naslov", "link", "boja"] },
  "pocetna-domacini": { naziv: "Početna — baner domaćina", polja: ["naslov", "tekst", "slikaUrl"] },
  "pocetna-korpica": {
    naziv: "Početna — hoću svoju korpicu",
    polja: ["naslov", "tekst", "dugme", "link", "slikaUrl"],
    tekst: "Tekst je opis slike za čitače ekrana.",
  },
  "pocetna-utisci": { naziv: "Početna — fotografije uz utiske", polja: ["slikaUrl"] },
  "spajz-razlozi": {
    naziv: "Špajz — zato što znaš šta vrijedi",
    polja: ["ikona", "naslov", "tekst"],
    tekst: "Ikona može biti emoji ili Sprout, MapPin, Award.",
  },
  "spajz-zasto": { naziv: "Špajz — zašto kod nas", polja: ["ikona", "naslov", "tekst"] },
  "korpice-mogucnosti": { naziv: "Korpice — u svakom trenutku možeš", polja: ["ikona", "naslov", "tekst"] },
  "partner-ponuda": { naziv: "Postani partner — šta nudimo", polja: ["naslov"] },
};

type Unos = Omit<BlokSadrzaja, "id" | "sekcija" | "redoslijed">;
const prazan = (): Unos => ({ objavljen: true });
const uUnos = (b: BlokSadrzaja): Unos => {
  const u: Partial<BlokSadrzaja> = { ...b };
  delete u.id;
  delete u.sekcija;
  delete u.redoslijed;
  return u as Unos;
};

/**
 * Blokovi sadržaja po sekcijama. Sekcija se čuva cijela odjednom; redoslijed na sajtu je
 * redoslijed ovdje. Sekcija bez blokova se na sajtu ne prikazuje.
 */
export function BlokoviEditor() {
  const [sekcija, setSekcija] = useState<SekcijaSadrzaja>(SEKCIJE_SADRZAJA[0]);
  const qc = useQueryClient();
  const { show } = useToast();
  const q = useQuery({ queryKey: ["admin", "blokovi", sekcija], queryFn: () => adminApi.blokovi(sekcija) });
  const [blokovi, setBlokovi] = useState<Unos[]>([]);
  const [greske, setGreske] = useState<Record<string, string[]>>({});

  useEffect(() => {
    if (q.data) setBlokovi(q.data.map(uUnos));
  }, [q.data]);

  const sacuvaj = useMutation({
    mutationFn: () => adminApi.sacuvajBlokove(sekcija, blokovi),
    onSuccess: (dto) => {
      qc.setQueryData(["admin", "blokovi", sekcija], dto);
      qc.invalidateQueries({ queryKey: queryKeys.blokoviSadrzaja() });
      show(`${OPIS[sekcija].naziv}: sačuvano`);
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

  const opis = OPIS[sekcija];
  const ima = (p: Polje) => opis.polja.includes(p);
  const izmijeni = (i: number, izmjena: Partial<Unos>) =>
    setBlokovi((b) => b.map((x, j) => (j === i ? { ...x, ...izmjena } : x)));
  const pomjeri = (i: number, za: number) =>
    setBlokovi((b) => {
      const n = [...b];
      const [x] = n.splice(i, 1);
      n.splice(i + za, 0, x);
      return n;
    });
  const greska = (i: number, polje: string) => greske[`blokovi[${i}].${polje}`]?.[0];

  return (
    <section className="space-y-4 rounded-card border border-line bg-surface p-5">
      <div>
        <h2 className="font-display text-lg font-bold text-ink">Sekcije sajta</h2>
        <p className="text-sm text-muted">Izaberi sekciju, uredi blokove i sačuvaj. Engleska polja koristi engleska verzija sajta.</p>
      </div>
      <div className="no-scrollbar -mx-1 flex gap-2 overflow-x-auto px-1">
        {SEKCIJE_SADRZAJA.map((s) => (
          <button
            key={s}
            type="button"
            onClick={() => {
              setGreske({});
              setSekcija(s);
            }}
            className={cn(
              "whitespace-nowrap rounded-full px-3.5 py-1.5 text-sm font-medium transition",
              s === sekcija ? "bg-ink text-white" : "bg-white text-ink ring-1 ring-black/10 hover:bg-sand",
            )}
          >
            {OPIS[s].naziv}
          </button>
        ))}
      </div>
      {opis.tekst && <p className="text-sm text-muted">{opis.tekst}</p>}

      {q.isLoading ? (
        <LoadingBlock />
      ) : (
        <form
          onSubmit={(e) => {
            e.preventDefault();
            setGreske({});
            sacuvaj.mutate();
          }}
          className="space-y-4"
        >
          {blokovi.length === 0 && <p className="text-sm text-muted">Sekcija je prazna i na sajtu se ne prikazuje.</p>}
          <ol className="space-y-4">
            {blokovi.map((b, i) => (
              <li key={i} className="space-y-3 rounded-xl border border-line p-4">
                <div className="flex items-center justify-between">
                  <label className="flex items-center gap-2 text-sm font-semibold text-muted">
                    Blok {i + 1}
                    <input
                      type="checkbox"
                      checked={b.objavljen}
                      onChange={(e) => izmijeni(i, { objavljen: e.target.checked })}
                      className="h-4 w-4 accent-brand-600"
                    />
                    <span className="font-normal">objavljen</span>
                  </label>
                  <span className="flex gap-1">
                    <Button type="button" size="sm" variant="ghost" aria-label="Pomjeri gore" disabled={i === 0} onClick={() => pomjeri(i, -1)}>
                      <ArrowUp className="h-4 w-4" />
                    </Button>
                    <Button type="button" size="sm" variant="ghost" aria-label="Pomjeri dolje" disabled={i === blokovi.length - 1} onClick={() => pomjeri(i, 1)}>
                      <ArrowDown className="h-4 w-4" />
                    </Button>
                    <Button type="button" size="sm" variant="ghost" aria-label="Ukloni blok" onClick={() => setBlokovi((x) => x.filter((_, j) => j !== i))}>
                      <Trash2 className="h-4 w-4 text-red-700" />
                    </Button>
                  </span>
                </div>
                <div className="grid gap-3 sm:grid-cols-2">
                  {ima("ikona") && (
                    <Field label="Ikona" error={greska(i, "ikona")}>
                      <Input value={b.ikona ?? ""} onChange={(e) => izmijeni(i, { ikona: e.target.value })} maxLength={40} />
                    </Field>
                  )}
                  {ima("naslov") && (
                    <Field label="Naslov" error={greska(i, "naslov")}>
                      <Input value={b.naslov ?? ""} onChange={(e) => izmijeni(i, { naslov: e.target.value })} maxLength={200} />
                    </Field>
                  )}
                  {ima("naslov") && (
                    <Field label="Naslov (engleski)" error={greska(i, "naslovEn")}>
                      <Input value={b.naslovEn ?? ""} onChange={(e) => izmijeni(i, { naslovEn: e.target.value })} maxLength={200} />
                    </Field>
                  )}
                  {ima("dugme") && (
                    <Field label="Dugme" error={greska(i, "dugme")}>
                      <Input value={b.dugme ?? ""} onChange={(e) => izmijeni(i, { dugme: e.target.value })} maxLength={60} />
                    </Field>
                  )}
                  {ima("dugme") && (
                    <Field label="Dugme (engleski)" error={greska(i, "dugmeEn")}>
                      <Input value={b.dugmeEn ?? ""} onChange={(e) => izmijeni(i, { dugmeEn: e.target.value })} maxLength={60} />
                    </Field>
                  )}
                  {ima("link") && (
                    <Field label="Link (npr. /korpice/povrce)" error={greska(i, "link")}>
                      <Input value={b.link ?? ""} onChange={(e) => izmijeni(i, { link: e.target.value })} maxLength={300} />
                    </Field>
                  )}
                  {ima("boja") && (
                    <Field label="Boja pozadine (#RRGGBB)" error={greska(i, "boja")}>
                      <Input value={b.boja ?? ""} onChange={(e) => izmijeni(i, { boja: e.target.value })} maxLength={7} />
                    </Field>
                  )}
                  {ima("boja") && (
                    <Field label="Boja teksta (#RRGGBB)" error={greska(i, "bojaTeksta")}>
                      <Input value={b.bojaTeksta ?? ""} onChange={(e) => izmijeni(i, { bojaTeksta: e.target.value })} maxLength={7} />
                    </Field>
                  )}
                </div>
                {ima("tekst") && (
                  <Field label="Tekst" error={greska(i, "tekst")}>
                    <Textarea value={b.tekst ?? ""} onChange={(e) => izmijeni(i, { tekst: e.target.value })} rows={2} maxLength={1000} />
                  </Field>
                )}
                {ima("tekst") && (
                  <Field label="Tekst (engleski)" error={greska(i, "tekstEn")}>
                    <Textarea value={b.tekstEn ?? ""} onChange={(e) => izmijeni(i, { tekstEn: e.target.value })} rows={2} maxLength={1000} />
                  </Field>
                )}
                {ima("slikaUrl") && (
                  <SlikaPolje label="Slika" folder="izgled" value={b.slikaUrl ?? ""} onChange={(url) => izmijeni(i, { slikaUrl: url })} error={greska(i, "slikaUrl")} />
                )}
              </li>
            ))}
          </ol>
          <div className="flex flex-wrap gap-2">
            <Button type="button" variant="secondary" size="sm" disabled={blokovi.length >= 30} onClick={() => setBlokovi((b) => [...b, prazan()])}>
              <Plus className="h-4 w-4" /> Dodaj blok
            </Button>
            <Button type="submit" size="sm" disabled={sacuvaj.isPending}>
              {sacuvaj.isPending ? "Čuvam…" : "Sačuvaj sekciju"}
            </Button>
          </div>
        </form>
      )}
    </section>
  );
}
