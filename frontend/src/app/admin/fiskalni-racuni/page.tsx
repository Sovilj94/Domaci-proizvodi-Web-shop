"use client";

import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { RotateCcw, Undo2 } from "lucide-react";
import { AdminLadica } from "@/components/admin/admin-ladica";
import { KarticaPoslovanja } from "@/components/admin/poslovanje/kartica-poslovanja";
import { Odjeljak } from "@/components/admin/poslovanje/odjeljak";
import { Tabela } from "@/components/admin/poslovanje/tabela";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { Field, Input } from "@/components/ui/field";
import { LoadingBlock } from "@/components/ui/spinner";
import { useToast } from "@/components/ui/toast";
import { ApiRequestError } from "@/lib/api/client";
import { adminApi } from "@/lib/api/endpoints";
import type { FiskalnaPodesavanja, FiskalniRacunRed, StatusFiskalnog } from "@/lib/api/contracts";
import { cn, formatPrice } from "@/lib/utils";

/** Naziv vrste računa; storno je poništavanje (vozač ispravio isporuku), refundacija povrat novca. */
const VRSTA = { Promet: "Promet", Refundacija: "Refundacija", Storno: "Storno" } as const;
const pad = (n: number) => String(n).padStart(2, "0");
const iso = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
const vrijeme = (s?: string | null) =>
  s ? new Date(s).toLocaleString("sr-Latn", { day: "numeric", month: "numeric", year: "numeric", hour: "2-digit", minute: "2-digit" }) : "—";
const greskaPoruka = (e: unknown) => (e instanceof ApiRequestError ? e.message : "Akcija nije uspjela. Pokušaj ponovo.");

const STATUS: Record<StatusFiskalnog, { tekst: string; klasa: string }> = {
  Izdat: { tekst: "Izdat", klasa: "bg-brand-100 text-brand-800" },
  NaCekanju: { tekst: "Na čekanju", klasa: "bg-honey-100 text-honey-700" },
  Greska: { tekst: "Greška", klasa: "bg-red-50 text-red-700" },
};

/**
 * Fiskalni računi: nastaju sami kad narudžba postane Isporučena, posao ih predaje dobavljaču (ESIR).
 * Ovdje se vidi stanje, ponavlja neuspjelo, refundira i podešavaju poreske oznake.
 */
export default function FiskalniRacuniPage() {
  const danas = new Date();
  const [od, setOd] = useState(iso(new Date(danas.getFullYear(), danas.getMonth(), 1)));
  const [doDana, setDoDana] = useState(iso(danas));
  const [otvoren, setOtvoren] = useState<string | null>(null);
  const q = useQuery({
    queryKey: ["admin", "fiskalni", od, doDana],
    queryFn: () => adminApi.fiskalniRacuni(od, doDana),
    refetchInterval: 30_000,
  });
  const d = q.data;

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="font-display text-2xl font-bold">Fiskalni računi</h1>
          <p className="text-muted">Račun nastaje sam kad vozač označi narudžbu kao isporučenu (naplata pouzećem).</p>
        </div>
        <div className="flex flex-wrap items-end gap-2">
          <Field label="Od">
            <Input type="date" value={od} onChange={(e) => setOd(e.target.value)} />
          </Field>
          <Field label="Do">
            <Input type="date" value={doDana} onChange={(e) => setDoDana(e.target.value)} />
          </Field>
        </div>
      </div>

      {d?.probni && (
        <p className="rounded-card bg-honey-100 px-4 py-3 text-sm font-medium text-honey-700">
          Radi probni dobavljač — računi imaju broj „PROBNI-…“ i nemaju fiskalnu vrijednost. Kad izaberete
          certifikovanog dobavljača ESIR-a, priključuje se umjesto probnog; ova stranica ostaje ista.
        </p>
      )}

      {q.isLoading ? (
        <LoadingBlock />
      ) : q.isError || !d ? (
        <EmptyState
          title="Ne mogu da učitam fiskalne račune"
          description={greskaPoruka(q.error)}
          action={<Button onClick={() => q.refetch()}>Pokušaj ponovo</Button>}
        />
      ) : (
        <>
          <div className="grid grid-cols-2 gap-3 sm:gap-4 lg:grid-cols-4">
            <KarticaPoslovanja label="Izdato u periodu" vrijednost={String(d.izdato)} hint={`promet ${formatPrice(d.iznosPromet)}`} />
            <KarticaPoslovanja label="Refundirano i stornirano" vrijednost={formatPrice(d.iznosRefundacija + (d.iznosStorno ?? 0))} hint={`storno ${formatPrice(d.iznosStorno ?? 0)}`} />
            <KarticaPoslovanja label="Na čekanju" vrijednost={String(d.naCekanju)} hint="šalju se svakih 30 sekundi" />
            <KarticaPoslovanja label="Greške" vrijednost={String(d.greske)} hint={d.greske ? "otvori račun za detalje" : "nema"} />
          </div>

          <Odjeljak naslov="Računi" opis="Neizdati računi su uvijek na listi, bez obzira na period.">
            <Tabela<FiskalniRacunRed>
              kljuc={(r) => r.id}
              redovi={d.racuni}
              prazno="Nema fiskalnih računa u ovom periodu."
              kolone={[
                { naslov: "Nastao", vrijednost: (r) => vrijeme(r.kreiran) },
                { naslov: "Narudžba", vrijednost: (r) => `${r.kodNarudzbe} · ${r.kupac}` },
                {
                  naslov: "Vrsta",
                  vrijednost: (r) => (r.vrsta !== "Promet" ? VRSTA[r.vrsta] : r.refundiran ? "Promet (poništen)" : "Promet"),
                },
                {
                  naslov: "Status",
                  vrijednost: (r) => (
                    <span className={cn("rounded-full px-2 py-0.5 text-xs font-semibold", STATUS[r.status].klasa)}>
                      {STATUS[r.status].tekst}
                    </span>
                  ),
                },
                { naslov: "Broj", vrijednost: (r) => <span className="font-mono text-xs">{r.broj ?? "—"}</span> },
                {
                  naslov: "Iznos",
                  vrijednost: (r) => (r.vrsta !== "Promet" ? `− ${formatPrice(r.iznos)}` : formatPrice(r.iznos)),
                  desno: true,
                },
                {
                  naslov: "",
                  vrijednost: (r) => (
                    <button
                      type="button"
                      onClick={() => setOtvoren(r.id)}
                      className="rounded-full px-2 py-1 text-xs font-semibold text-brand-700 hover:bg-brand-50"
                    >
                      Otvori
                    </button>
                  ),
                  desno: true,
                },
              ]}
            />
          </Odjeljak>

          <PodesavanjaOdjeljak />
        </>
      )}

      {otvoren && <RacunLadica id={otvoren} onClose={() => setOtvoren(null)} />}
    </div>
  );
}

function RacunLadica({ id, onClose }: { id: string; onClose: () => void }) {
  const qc = useQueryClient();
  const { show } = useToast();
  const q = useQuery({ queryKey: ["admin", "fiskalni", "racun", id], queryFn: () => adminApi.fiskalniRacun(id) });
  const [potvrdiRefund, setPotvrdiRefund] = useState(false);
  const osvjezi = () => qc.invalidateQueries({ queryKey: ["admin", "fiskalni"] });
  const ponovi = useMutation({
    mutationFn: () => adminApi.ponoviFiskalniRacun(id),
    onSuccess: () => {
      osvjezi();
      show("Račun ide dobavljaču pri sljedećem prolazu (do 30 s)");
    },
    onError: (e) => show(greskaPoruka(e), "error"),
  });
  const refund = useMutation({
    mutationFn: (orderId: string) => adminApi.refundirajNarudzbu(orderId),
    onSuccess: () => {
      osvjezi();
      show("Refundacioni račun je napravljen i ide dobavljaču");
      onClose();
    },
    onError: (e) => show(greskaPoruka(e), "error"),
  });
  const r = q.data;

  return (
    <AdminLadica naslov={r ? (r.vrsta === "Promet" ? "Fiskalni račun" : `${VRSTA[r.vrsta]} — račun`) : "Račun"} podnaslov={r?.kodNarudzbe} onClose={onClose}>
      <div className="flex-1 space-y-4 overflow-y-auto p-4 sm:p-6">
        {q.isLoading || !r ? (
          <LoadingBlock />
        ) : (
          <>
            <div className="space-y-3 rounded-card border border-line bg-white p-4 font-mono text-xs">
              <p className="text-center font-bold">E piljara</p>
              {r.dobavljac?.startsWith("Probni") && <p className="text-center font-bold text-red-700">PROBNI — NEMA FISKALNU VRIJEDNOST</p>}
              <p className="text-center">{r.vrsta === "Promet" ? "PROMET" : VRSTA[r.vrsta].toUpperCase()} · {r.nacinPlacanja}</p>
              <table className="w-full">
                <tbody>
                  {r.stavke.map((s, i) => (
                    <tr key={i} className="align-top">
                      <td className="py-0.5 pr-2">
                        {s.naziv} ({s.poreskaOznaka})
                        <br />
                        {s.kolicina.toLocaleString("sr-Latn")} {s.jedinica} × {s.jedinicnaCijena.toFixed(2)}
                        {s.popust > 0 && ` − popust ${s.popust.toFixed(2)}`}
                      </td>
                      <td className="py-0.5 text-right">{s.ukupno.toFixed(2)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
              <p className="flex justify-between border-t border-dashed border-line pt-2 font-bold">
                <span>UKUPNO</span>
                <span>{r.vrsta !== "Promet" ? "− " : ""}{formatPrice(r.iznos)}</span>
              </p>
              <p>Broj: {r.broj ?? "—"}{r.brojac ? ` · ${r.brojac}` : ""}</p>
              <p>Vrijeme: {vrijeme(r.izdat)}</p>
              {r.refundiraniBroj && <p>{r.vrsta === "Storno" ? "Poništava" : "Refundira"}: {r.refundiraniBroj}</p>}
              {r.qrSadrzaj && <p className="break-all rounded bg-sand p-2">QR: {r.qrSadrzaj}</p>}
              {r.verifikacijaUrl && (
                <a href={r.verifikacijaUrl} target="_blank" rel="noreferrer" className="block text-brand-700 underline">
                  Provjera računa
                </a>
              )}
            </div>

            <dl className="grid grid-cols-2 gap-2 text-sm">
              <dt className="text-muted">Status</dt>
              <dd>{STATUS[r.status].tekst}</dd>
              <dt className="text-muted">Nastao</dt>
              <dd>{vrijeme(r.kreiran)}</dd>
              <dt className="text-muted">Pokušaja</dt>
              <dd>{r.pokusaja}</dd>
              {r.dobavljac && (
                <>
                  <dt className="text-muted">Dobavljač</dt>
                  <dd>{r.dobavljac}</dd>
                </>
              )}
              {r.status !== "Izdat" && r.sljedeciPokusaj && !r.trajnaGreska && (
                <>
                  <dt className="text-muted">Sljedeći pokušaj</dt>
                  <dd>{vrijeme(r.sljedeciPokusaj)}</dd>
                </>
              )}
            </dl>
            {r.zadnjaGreska && (
              <p className="rounded-card bg-red-50 px-3 py-2 text-sm text-red-700">
                {r.trajnaGreska ? "Dobavljač je odbio račun: " : "Zadnja greška: "}
                {r.zadnjaGreska}
              </p>
            )}

            <div className="flex flex-wrap justify-end gap-2 pt-2">
              {r.status !== "Izdat" && (
                <Button variant="secondary" size="sm" onClick={() => ponovi.mutate()} disabled={ponovi.isPending}>
                  <RotateCcw className="h-4 w-4" /> Pokušaj ponovo
                </Button>
              )}
              {r.vrsta === "Promet" && r.status === "Izdat" && !r.refundiran && (
                <Button
                  variant={potvrdiRefund ? "danger" : "secondary"}
                  size="sm"
                  onClick={() => (potvrdiRefund ? refund.mutate(r.orderId) : setPotvrdiRefund(true))}
                  disabled={refund.isPending}
                >
                  <Undo2 className="h-4 w-4" /> {potvrdiRefund ? `Sigurno — vrati ${formatPrice(r.iznos)}?` : "Refundiraj"}
                </Button>
              )}
            </div>
            {r.vrsta === "Promet" && r.status === "Izdat" && !r.refundiran && (
              <p className="text-xs text-muted">Refundacija vraća cijeli iznos (kupac vratio robu ili novac). Djelimična još ne postoji.</p>
            )}
          </>
        )}
      </div>
    </AdminLadica>
  );
}

function PodesavanjaOdjeljak() {
  const qc = useQueryClient();
  const { show } = useToast();
  const q = useQuery({ queryKey: ["admin", "fiskalna-podesavanja"], queryFn: adminApi.fiskalnaPodesavanja });
  const [unos, setUnos] = useState<FiskalnaPodesavanja | null>(null);
  const [stopa, setStopa] = useState("");
  useEffect(() => {
    if (q.data) {
      setUnos(q.data);
      setStopa(String(q.data.stopaPdv));
    }
  }, [q.data]);
  const sacuvaj = useMutation({
    mutationFn: () =>
      adminApi.sacuvajFiskalnaPodesavanja({
        pdvObveznik: unos!.pdvObveznik,
        stopaPdv: Number(stopa.replace(",", ".")),
        oznakaPdv: unos!.oznakaPdv,
        oznakaBezPdv: unos!.oznakaBezPdv,
      }),
    onSuccess: (novo) => {
      qc.setQueryData(["admin", "fiskalna-podesavanja"], novo);
      show("Fiskalna podešavanja sačuvana — važe za nove račune");
    },
    onError: (e) => show(greskaPoruka(e), "error"),
  });
  if (!unos) return null;

  return (
    <Odjeljak
      naslov="Podešavanja fiskalizacije"
      opis="Poreske oznake propisuje dobavljač ESIR-a — provjeri ih sa njim i knjigovođom prije početka rada."
    >
      <form
        onSubmit={(e) => {
          e.preventDefault();
          sacuvaj.mutate();
        }}
        className="space-y-4"
      >
        <label className="flex items-center gap-2 text-sm font-medium">
          <input
            type="checkbox"
            checked={unos.pdvObveznik}
            onChange={(e) => setUnos({ ...unos, pdvObveznik: e.target.checked })}
            className="h-4 w-4 accent-brand-600"
          />
          Firma je u sistemu PDV-a
        </label>
        <div className="grid gap-3 sm:grid-cols-3">
          <Field label="Stopa PDV-a (%)">
            <Input inputMode="decimal" value={stopa} onChange={(e) => setStopa(e.target.value)} disabled={!unos.pdvObveznik} />
          </Field>
          <Field label="Oznaka stavke sa PDV-om">
            <Input value={unos.oznakaPdv} maxLength={10} onChange={(e) => setUnos({ ...unos, oznakaPdv: e.target.value })} />
          </Field>
          <Field label="Oznaka kad firma nije u PDV-u">
            <Input value={unos.oznakaBezPdv} maxLength={10} onChange={(e) => setUnos({ ...unos, oznakaBezPdv: e.target.value })} />
          </Field>
        </div>
        <p className="text-sm text-muted">
          Sada se koristi oznaka <strong>{unos.pdvObveznik ? unos.oznakaPdv : unos.oznakaBezPdv}</strong> za sve stavke.
          Dobavljač: <strong>{unos.dobavljac}</strong>.
        </p>
        <div className="flex justify-end">
          <Button type="submit" size="sm" disabled={sacuvaj.isPending}>
            Sačuvaj
          </Button>
        </div>
      </form>
    </Odjeljak>
  );
}
