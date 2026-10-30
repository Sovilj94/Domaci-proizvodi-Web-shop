"use client";

import { useEffect, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ArrowDown, ArrowUp, Plus, Trash2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Field, Input, Textarea } from "@/components/ui/field";
import { LoadingBlock } from "@/components/ui/spinner";
import { useToast } from "@/components/ui/toast";
import { ApiRequestError } from "@/lib/api/client";
import { adminApi } from "@/lib/api/endpoints";
import { queryKeys, useSadrzajSajta } from "@/lib/api/hooks";
import { OZNAKE_PITANJA } from "@/lib/cesta-pitanja";
import { BlokoviEditor } from "@/components/admin/blokovi-editor";
import { formatDateTime } from "@/lib/utils";
import type { Pitanje } from "@/types/domain";

/**
 * Sadržaj sajta: kontakt (footer, /o-nama), česta pitanja (početna) i blokovi sekcija (slajdovi,
 * kartice, pločice, slike). Sve je u bazi — sajt nema svoje ugrađene tekstove, pa prazno polje znači
 * da se to mjesto na sajtu ne prikazuje.
 */
export default function AdminSadrzajPage() {
  const q = useSadrzajSajta();
  const qc = useQueryClient();
  const { show } = useToast();
  const [kontakt, setKontakt] = useState({ telefon: "", email: "", adresa: "", podrucje: "" });
  const [pitanja, setPitanja] = useState<Pitanje[]>([]);
  const [greske, setGreske] = useState<Record<string, string[]>>({});

  useEffect(() => {
    if (!q.data) return;
    setKontakt({
      telefon: q.data.telefon ?? "",
      email: q.data.email ?? "",
      adresa: q.data.adresa ?? "",
      podrucje: q.data.podrucje ?? "",
    });
    setPitanja(q.data.pitanja ?? []);
  }, [q.data]);

  const sacuvaj = useMutation({
    mutationFn: () => adminApi.sacuvajSadrzajSajta({ ...kontakt, pitanja }),
    onSuccess: (dto) => {
      qc.setQueryData(queryKeys.sadrzajSajta(), dto);
      show("Sadržaj sajta sačuvan");
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

  function izmijeni(i: number, polje: keyof Pitanje, v: string) {
    setPitanja((p) => p.map((x, j) => (j === i ? { ...x, [polje]: v } : x)));
  }
  function pomjeri(i: number, za: number) {
    setPitanja((p) => {
      const n = [...p];
      const [x] = n.splice(i, 1);
      n.splice(i + za, 0, x);
      return n;
    });
  }

  if (q.isLoading) return <LoadingBlock />;

  return (
    <div className="max-w-3xl space-y-8">
    <form
      onSubmit={(e) => {
        e.preventDefault();
        setGreske({});
        sacuvaj.mutate();
      }}
      className="space-y-8"
    >
      <div>
        <h1 className="font-display text-2xl font-bold text-ink">Sadržaj sajta</h1>
        <p className="text-sm text-muted">
          Sve što sajt prikazuje u ovim dijelovima je ovdje; prazno polje se na sajtu ne prikazuje.
          {q.data?.izmijenjeno && ` Zadnja izmjena: ${formatDateTime(q.data.izmijenjeno)}.`}
        </p>
      </div>

      <section className="space-y-4 rounded-card border border-line bg-surface p-5">
        <h2 className="font-display text-lg font-bold text-ink">Kontakt (footer i „O nama“)</h2>
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Telefon" error={greske.telefon?.[0]}>
            <Input value={kontakt.telefon} onChange={(e) => setKontakt({ ...kontakt, telefon: e.target.value })} placeholder="npr. +387 65 000 000" maxLength={40} />
          </Field>
          <Field label="E-mail (opcionalno)" error={greske.email?.[0]}>
            <Input type="email" value={kontakt.email} onChange={(e) => setKontakt({ ...kontakt, email: e.target.value })} placeholder="npr. info@epiljara.ba" maxLength={120} />
          </Field>
          <Field label="Adresa" error={greske.adresa?.[0]}>
            <Input value={kontakt.adresa} onChange={(e) => setKontakt({ ...kontakt, adresa: e.target.value })} placeholder="npr. Manjača, Banja Luka" maxLength={200} />
          </Field>
          <Field label="Područje dostave" error={greske.podrucje?.[0]}>
            <Input value={kontakt.podrucje} onChange={(e) => setKontakt({ ...kontakt, podrucje: e.target.value })} placeholder="npr. Banja Luka i okolina" maxLength={120} />
          </Field>
        </div>
      </section>

      <section className="space-y-4 rounded-card border border-line bg-surface p-5">
        <div>
          <h2 className="font-display text-lg font-bold text-ink">Česta pitanja (početna)</h2>
          <p className="text-sm text-muted">
            Bez pitanja se sekcija na početnoj ne prikazuje. U odgovoru možeš koristiti oznake {OZNAKE_PITANJA.join(", ")} —
            sajt ih zamijeni tekućim rokom, danom isporuke, procentima popusta i obavezom plana; rečenica čiji broj server
            ne pošalje se izostavlja.
          </p>
        </div>

        <ol className="space-y-4">
          {pitanja.map((p, i) => (
            <li key={i} className="space-y-2 rounded-xl border border-line p-4">
              <div className="flex items-center justify-between">
                <span className="text-sm font-semibold text-muted">Pitanje {i + 1}</span>
                <span className="flex gap-1">
                  <Button type="button" size="sm" variant="ghost" aria-label="Pomjeri gore" disabled={i === 0} onClick={() => pomjeri(i, -1)}>
                    <ArrowUp className="h-4 w-4" />
                  </Button>
                  <Button type="button" size="sm" variant="ghost" aria-label="Pomjeri dolje" disabled={i === pitanja.length - 1} onClick={() => pomjeri(i, 1)}>
                    <ArrowDown className="h-4 w-4" />
                  </Button>
                  <Button type="button" size="sm" variant="ghost" aria-label="Ukloni pitanje" onClick={() => setPitanja((x) => x.filter((_, j) => j !== i))}>
                    <Trash2 className="h-4 w-4 text-red-700" />
                  </Button>
                </span>
              </div>
              <Field label="Pitanje" required error={greske[`pitanja[${i}].pitanje`]?.[0]}>
                <Input value={p.pitanje} onChange={(e) => izmijeni(i, "pitanje", e.target.value)} maxLength={200} />
              </Field>
              <Field label="Odgovor" required error={greske[`pitanja[${i}].odgovor`]?.[0]}>
                <Textarea value={p.odgovor} onChange={(e) => izmijeni(i, "odgovor", e.target.value)} rows={3} maxLength={1500} />
              </Field>
              <Field label="Pitanje (engleski)" error={greske[`pitanja[${i}].pitanjeEn`]?.[0]}>
                <Input value={p.pitanjeEn ?? ""} onChange={(e) => izmijeni(i, "pitanjeEn", e.target.value)} maxLength={200} />
              </Field>
              <Field label="Odgovor (engleski)" error={greske[`pitanja[${i}].odgovorEn`]?.[0]}>
                <Textarea value={p.odgovorEn ?? ""} onChange={(e) => izmijeni(i, "odgovorEn", e.target.value)} rows={3} maxLength={1500} />
              </Field>
            </li>
          ))}
        </ol>

        <div className="flex flex-wrap gap-2">
          <Button type="button" variant="secondary" size="sm" disabled={pitanja.length >= 30} onClick={() => setPitanja((p) => [...p, { pitanje: "", odgovor: "" }])}>
            <Plus className="h-4 w-4" /> Dodaj pitanje
          </Button>
        </div>
      </section>

      <Button type="submit" disabled={sacuvaj.isPending}>
        {sacuvaj.isPending ? "Čuvam…" : "Sačuvaj kontakt i pitanja"}
      </Button>
    </form>

    <BlokoviEditor />
    </div>
  );
}
