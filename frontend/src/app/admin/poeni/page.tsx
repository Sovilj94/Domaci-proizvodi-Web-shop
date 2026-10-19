"use client";

import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { AdminLadica } from "@/components/admin/admin-ladica";
import { Button } from "@/components/ui/button";
import { Field, Input } from "@/components/ui/field";
import { LoadingBlock } from "@/components/ui/spinner";
import { useToast } from "@/components/ui/toast";
import { ApiRequestError } from "@/lib/api/client";
import { adminApi } from "@/lib/api/endpoints";
import { cn, formatDateTime } from "@/lib/utils";
import type { AdminPoeniKupac } from "@/types/domain";

/**
 * Poeni kupaca (B4): stanje, cijela istorija i ručna korekcija sa razlogom. Korekcija je nova
 * stavka u knjizi (`Korekcija`), nikad izmjena postojeće; razlog vidi i kupac u svojoj istoriji.
 */
export default function AdminPoeniPage() {
  const [unos, setUnos] = useState("");
  const [pretraga, setPretraga] = useState("");
  const [izabran, setIzabran] = useState<AdminPoeniKupac | null>(null);

  // Pretraga ide na server tek kad se kucanje smiri, ne po svakom tasteru.
  useEffect(() => {
    const t = setTimeout(() => setPretraga(unos.trim()), 300);
    return () => clearTimeout(t);
  }, [unos]);

  const q = useQuery({
    queryKey: ["admin", "poeni", pretraga],
    queryFn: () => adminApi.poeniKupci(pretraga),
  });

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Poeni kupaca</h1>
        <p className="text-sm text-muted">Stanje, istorija i ručna korekcija. Prikazano je do 100 kupaca, najveće stanje prvo.</p>
      </div>

      <div className="max-w-sm">
        <Field label="Pretraga po imenu ili e-mailu">
          <Input type="search" value={unos} onChange={(e) => setUnos(e.target.value)} placeholder="npr. Marko" />
        </Field>
      </div>

      {q.isLoading ? (
        <LoadingBlock />
      ) : q.isError ? (
        <p className="text-red-700">Lista nije učitana.</p>
      ) : !q.data?.length ? (
        <p className="text-muted">Nema kupaca{pretraga && " za ovu pretragu"}.</p>
      ) : (
        <ul className="divide-y divide-line rounded-card border border-line bg-surface">
          {q.data.map((k) => (
            <li key={k.userId}>
              <button
                type="button"
                onClick={() => setIzabran(k)}
                className="flex w-full items-center justify-between gap-3 px-4 py-3 text-left text-sm hover:bg-sand"
              >
                <span className="min-w-0">
                  <span className="block font-medium">{k.ime}</span>
                  <span className="block truncate text-muted">{k.email}</span>
                </span>
                <span className={cn("shrink-0 font-semibold tabular-nums", k.stanje < 0 && "text-red-700")}>
                  {k.stanje} poena
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}

      {izabran && <PoeniKupca kupac={izabran} onClose={() => setIzabran(null)} />}
    </div>
  );
}

function PoeniKupca({ kupac, onClose }: { kupac: AdminPoeniKupac; onClose: () => void }) {
  const qc = useQueryClient();
  const { show } = useToast();
  const [iznos, setIznos] = useState("");
  const [razlog, setRazlog] = useState("");
  const [greske, setGreske] = useState<Record<string, string[]>>({});

  const detalj = useQuery({
    queryKey: ["admin", "poeni", "kupac", kupac.userId],
    queryFn: () => adminApi.poeniKupca(kupac.userId),
  });

  const korekcija = useMutation({
    mutationFn: () => adminApi.korigujPoene(kupac.userId, { iznos: Math.trunc(Number(iznos)), razlog }),
    onSuccess: (dto) => {
      qc.setQueryData(["admin", "poeni", "kupac", kupac.userId], dto);
      qc.invalidateQueries({ queryKey: ["admin", "poeni"], exact: false });
      setIznos("");
      setRazlog("");
      show(`Korekcija upisana — novo stanje ${dto.stanje} poena`);
    },
    onError: (err) => {
      if (err instanceof ApiRequestError) {
        setGreske(err.fieldErrors);
        if (Object.keys(err.fieldErrors).length > 0) return;
        show(err.message, "error");
        return;
      }
      show("Korekcija nije upisana", "error");
    },
  });

  const broj = Number(iznos);
  const ispravno = iznos.trim() !== "" && Number.isInteger(broj) && broj !== 0 && razlog.trim() !== "";

  return (
    <AdminLadica naslov={kupac.ime} podnaslov={kupac.email} onClose={onClose}>
      <div className="flex-1 space-y-6 overflow-y-auto p-6">
        <p className="text-sm text-muted">
          Stanje:{" "}
          <span className="font-display text-2xl font-bold text-ink tabular-nums">
            {detalj.data?.stanje ?? kupac.stanje}
          </span>{" "}
          poena
        </p>

        <form
          onSubmit={(e) => {
            e.preventDefault();
            setGreske({});
            korekcija.mutate();
          }}
          className="space-y-3 rounded-card border border-line bg-surface p-4"
        >
          <h3 className="font-semibold">Ručna korekcija</h3>
          <Field label="Iznos (minus oduzima)" required error={greske.iznos?.[0]}>
            <Input type="number" step={1} value={iznos} onChange={(e) => setIznos(e.target.value)} placeholder="npr. 50 ili -20" />
          </Field>
          <Field label="Razlog (vidi ga i kupac)" required error={greske.razlog?.[0]}>
            <Input value={razlog} onChange={(e) => setRazlog(e.target.value)} maxLength={200} placeholder="npr. Izvinjenje za kasnu isporuku" />
          </Field>
          <Button type="submit" disabled={!ispravno || korekcija.isPending}>
            {korekcija.isPending ? "Upisujem…" : "Upiši korekciju"}
          </Button>
        </form>

        <div>
          <h3 className="mb-2 font-semibold">Istorija</h3>
          {detalj.isLoading ? (
            <LoadingBlock />
          ) : detalj.isError ? (
            <p className="text-sm text-red-700">Istorija nije učitana.</p>
          ) : !detalj.data?.istorija.length ? (
            <p className="text-sm text-muted">Kupac još nema nijednu stavku.</p>
          ) : (
            <ul className="divide-y divide-line rounded-card border border-line bg-surface text-sm">
              {detalj.data.istorija.map((s, i) => (
                <li key={i} className="flex items-start justify-between gap-3 px-4 py-2.5">
                  <span className="min-w-0">
                    <span className="block">{s.opis}</span>
                    <span className="block text-xs text-muted">
                      {formatDateTime(s.kreirana)}
                      {s.unio && ` · unio ${s.unio}`}
                    </span>
                  </span>
                  <span className={cn("shrink-0 font-semibold tabular-nums", s.iznos < 0 ? "text-red-700" : "text-brand-700")}>
                    {s.iznos > 0 ? `+${s.iznos}` : s.iznos}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>
    </AdminLadica>
  );
}
