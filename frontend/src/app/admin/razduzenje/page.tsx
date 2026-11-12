"use client";

import { Suspense, useState } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ChevronLeft, ChevronRight } from "lucide-react";
import { KarticaPoslovanja } from "@/components/admin/poslovanje/kartica-poslovanja";
import { Odjeljak } from "@/components/admin/poslovanje/odjeljak";
import { Tabela } from "@/components/admin/poslovanje/tabela";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { Input } from "@/components/ui/field";
import { LoadingBlock } from "@/components/ui/spinner";
import { useToast } from "@/components/ui/toast";
import { ApiRequestError } from "@/lib/api/client";
import { adminApi } from "@/lib/api/endpoints";
import type { RazduzenjeDan } from "@/lib/api/contracts";
import { MJESECI, susjedniMjesec } from "@/lib/admin/poslovanje";
import { cn, formatPrice } from "@/lib/utils";

const pad = (n: number) => String(n).padStart(2, "0");
const datum = (iso: string) => {
  const [g, m, d] = iso.split("-");
  return `${Number(d)}.${Number(m)}.${g}.`;
};

/** "12,5" i "12.5" su oba broj; prazno ili pogrešno je null. */
function broj(v: string): number | null {
  const t = v.trim().replace(",", ".");
  if (!/^\d+(\.\d{1,2})?$/.test(t)) return null;
  return Number(t);
}

function poruka(err: unknown) {
  return err instanceof ApiRequestError ? err.message : "Čuvanje nije uspjelo. Pokušaj ponovo.";
}

export default function RazduzenjePage() {
  return (
    <Suspense fallback={<LoadingBlock />}>
      <Razduzenje />
    </Suspense>
  );
}

function Razduzenje() {
  const params = useSearchParams();
  const router = useRouter();
  const pathname = usePathname();
  const danas = new Date();
  const godina = Number(params.get("godina")) || danas.getFullYear();
  const mjesec = Number(params.get("mjesec")) || danas.getMonth() + 1;
  const od = `${godina}-${pad(mjesec)}-01`;
  const doDatuma = `${godina}-${pad(mjesec)}-${pad(new Date(godina, mjesec, 0).getDate())}`;

  const q = useQuery({
    queryKey: ["admin", "razduzenje", godina, mjesec],
    queryFn: () => adminApi.razduzenje(od, doDatuma),
  });

  function idi(pomak: number) {
    const s = susjedniMjesec(godina, mjesec, pomak);
    router.replace(`${pathname}?godina=${s.godina}&mjesec=${s.mjesec}`, { scroll: false });
  }

  const d = q.data;
  const ukupnoNerazduzeno = d?.nerazduzeno.reduce((s, n) => s + n.iznos, 0) ?? 0;
  const naplacenoMjesec = d?.dani.reduce((s, n) => s + n.zaPredati, 0) ?? 0;
  const manjak = d?.dani.reduce((s, n) => s + Math.min(0, n.razlika ?? 0), 0) ?? 0;

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="font-display text-2xl font-bold">
            Razduženje vozača — {MJESECI[mjesec - 1]} {godina}.
          </h1>
          <p className="text-muted">
            Naplata je pouzećem: vozač donosi gotovinu za svaku isporučenu narudžbu. Upiši koliko je predao.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="secondary" size="sm" onClick={() => idi(-1)} aria-label="Prethodni mjesec">
            <ChevronLeft className="h-4 w-4" />
          </Button>
          <Button variant="secondary" size="sm" onClick={() => idi(1)} aria-label="Sljedeći mjesec">
            <ChevronRight className="h-4 w-4" />
          </Button>
        </div>
      </div>

      {q.isLoading ? (
        <LoadingBlock />
      ) : q.isError || !d ? (
        <EmptyState
          title="Ne mogu da učitam razduženje"
          description={poruka(q.error)}
          action={<Button onClick={() => q.refetch()}>Pokušaj ponovo</Button>}
        />
      ) : (
        <>
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
            <KarticaPoslovanja
              label="Treba da donesu"
              vrijednost={formatPrice(ukupnoNerazduzeno)}
              hint="svi nerazduženi dani, i iz ranijih mjeseci"
            />
            <KarticaPoslovanja label="Naplaćeno ovog mjeseca" vrijednost={formatPrice(naplacenoMjesec)} hint="isporučene narudžbe" />
            <KarticaPoslovanja
              label="Manjak ovog mjeseca"
              vrijednost={formatPrice(Math.abs(manjak))}
              hint="predano manje nego naplaćeno"
            />
          </div>

          <Odjeljak naslov="Ko treba da donese" opis="Zbir po vozaču za dane bez evidentirane predaje.">
            <Tabela
              kolone={[
                { naslov: "Vozač", vrijednost: (n) => n.vozac },
                { naslov: "Dana", vrijednost: (n) => n.dana, desno: true },
                { naslov: "Od", vrijednost: (n) => datum(n.odDatuma), desno: true },
                { naslov: "Iznos", vrijednost: (n) => <span className="font-semibold">{formatPrice(n.iznos)}</span>, desno: true },
                { naslov: " ", vrijednost: (n) => <PredaoSve driverId={n.driverId} vozac={n.vozac} />, desno: true },
              ]}
              redovi={d.nerazduzeno}
              kljuc={(n) => n.driverId}
              prazno="Svi vozači su razduženi."
            />
          </Odjeljak>

          <Odjeljak naslov="Po danima" opis="Iznos je predložen; ispravi ga ako je vozač predao drugačije.">
            <Tabela<RazduzenjeDan>
              kolone={[
                { naslov: "Datum", vrijednost: (r) => datum(r.datum) },
                { naslov: "Vozač", vrijednost: (r) => r.vozac },
                { naslov: "Dostava", vrijednost: (r) => r.dostavljeno, desno: true },
                { naslov: "Za predati", vrijednost: (r) => formatPrice(r.zaPredati), desno: true },
                { naslov: "Predano", vrijednost: (r) => <Predaja red={r} />, desno: true },
                {
                  naslov: "Razlika",
                  vrijednost: (r) =>
                    r.razlika == null ? (
                      "—"
                    ) : (
                      <span className={cn(r.razlika < 0 ? "font-semibold text-red-700" : r.razlika > 0 ? "text-ink" : "text-muted")}>
                        {r.razlika > 0 ? "+" : ""}
                        {formatPrice(r.razlika)}
                      </span>
                    ),
                  desno: true,
                },
              ]}
              redovi={d.dani}
              kljuc={(r) => `${r.driverId}-${r.datum}`}
              prazno="Nema isporučenih narudžbi u ovom mjesecu."
            />
          </Odjeljak>
        </>
      )}
    </div>
  );
}

/** Svi nerazduženi dani vozača do danas postaju predati u punom iznosu; traži drugi klik za potvrdu. */
function PredaoSve({ driverId, vozac }: { driverId: string; vozac: string }) {
  const qc = useQueryClient();
  const { show } = useToast();
  const [potvrda, setPotvrda] = useState(false);
  const d = new Date();
  const danas = `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;

  const m = useMutation({
    mutationFn: () => adminApi.razduziSve(driverId, danas),
    onSuccess: (broj) => {
      qc.invalidateQueries({ queryKey: ["admin", "razduzenje"] });
      show(`${vozac}: razduženo ${broj} ${broj === 1 ? "dan" : "dana"}`);
      setPotvrda(false);
    },
    onError: (err) => show(poruka(err)),
  });

  if (!potvrda)
    return (
      <Button size="sm" variant="secondary" onClick={() => setPotvrda(true)}>
        Predao sve
      </Button>
    );
  return (
    <span className="inline-flex items-center gap-2">
      <Button size="sm" onClick={() => m.mutate()} disabled={m.isPending}>
        Potvrdi
      </Button>
      <button type="button" className="text-xs text-muted underline" onClick={() => setPotvrda(false)}>
        odustani
      </button>
    </span>
  );
}

/** Polje za iznos sa dugmetom „Predao"; evidentirana predaja se može poništiti. */
function Predaja({ red }: { red: RazduzenjeDan }) {
  const qc = useQueryClient();
  const { show } = useToast();
  const [iznos, setIznos] = useState(String(red.predano ?? red.zaPredati).replace(".", ","));
  const [greska, setGreska] = useState<string | null>(null);

  const m = useMutation({
    mutationFn: (vrijednost: number | null) =>
      adminApi.evidentirajPredaju({ driverId: red.driverId, datum: red.datum, iznos: vrijednost }),
    onSuccess: (r) => {
      qc.invalidateQueries({ queryKey: ["admin", "razduzenje"] });
      show(r.predano == null ? "Predaja poništena" : `${red.vozac}: predano ${formatPrice(r.predano)}`);
    },
    onError: (err) => setGreska(poruka(err)),
  });

  if (red.predano != null) {
    return (
      <span className="inline-flex items-center gap-2">
        <span className="font-semibold">{formatPrice(red.predano)}</span>
        <button
          type="button"
          className="text-xs text-muted underline hover:text-ink"
          onClick={() => m.mutate(null)}
          disabled={m.isPending}
        >
          poništi
        </button>
      </span>
    );
  }

  function sacuvaj(e: React.FormEvent) {
    e.preventDefault();
    const v = broj(iznos);
    if (v === null) {
      setGreska("Upiši iznos, npr. 54,50.");
      return;
    }
    setGreska(null);
    m.mutate(v);
  }

  return (
    <form onSubmit={sacuvaj} className="inline-flex flex-col items-end gap-1">
      <span className="inline-flex items-center gap-2">
        <Input
          value={iznos}
          onChange={(e) => setIznos(e.target.value)}
          inputMode="decimal"
          aria-label={`Predano — ${red.vozac}, ${datum(red.datum)}`}
          className="w-24 text-right"
        />
        <Button size="sm" type="submit" disabled={m.isPending}>
          Predao
        </Button>
      </span>
      {greska && <span className="text-xs text-red-700">{greska}</span>}
    </form>
  );
}
