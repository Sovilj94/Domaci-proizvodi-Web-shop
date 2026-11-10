"use client";

import { useState } from "react";
import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { ChartColumn, Printer, TriangleAlert } from "lucide-react";
import { KarticaPoslovanja } from "@/components/admin/poslovanje/kartica-poslovanja";
import { Odjeljak } from "@/components/admin/poslovanje/odjeljak";
import { Tabela } from "@/components/admin/poslovanje/tabela";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { Field, Input, Select } from "@/components/ui/field";
import { LoadingBlock } from "@/components/ui/spinner";
import { ApiRequestError } from "@/lib/api/client";
import { adminApi } from "@/lib/api/endpoints";
import type { PlanScenarij } from "@/lib/api/contracts";
import { MJESECI } from "@/lib/admin/poslovanje";
import { cn, formatPrice } from "@/lib/utils";

const pct = (x?: number | null) => (x == null ? "—" : `${(Math.round(x * 1000) / 10).toLocaleString("sr-Latn")} %`);
const broj = (v: string) => {
  const t = v.trim().replace(",", ".");
  return t === "" ? 0 : /^\d+(\.\d+)?$/.test(t) ? Number(t) : null;
};

/**
 * Plan poslovanja: šta donosi mjesec sa N narudžbi. Prosječna narudžba je iz stvarnih narudžbi zadnja tri
 * mjeseca (koje korpice, koliko Špajza, dostava), preračunata po DANAŠNJIM cijenama i nabavnim; troškovi
 * iz Troškova. Plate koje nisu u Troškovima unose se ovdje.
 */
export default function PlanPoslovanjaPage() {
  const sad = new Date();
  const sljedeci = new Date(sad.getFullYear(), sad.getMonth() + 1, 1);
  const [narudzbi, setNarudzbi] = useState("200");
  const [mjesec, setMjesec] = useState(`${sljedeci.getFullYear()}-${sljedeci.getMonth() + 1}`);
  const [dodatno, setDodatno] = useState("");
  const [g, m] = mjesec.split("-").map(Number);
  const n = broj(narudzbi);
  const dod = broj(dodatno);
  const ispravno = n != null && n >= 1 && dod != null;

  const q = useQuery({
    queryKey: ["admin", "poslovanje", "plan", n, g, m, dod],
    queryFn: () => adminApi.planPoslovanja(Math.round(n!), g, m, dod!),
    enabled: ispravno,
  });
  const d = q.data;
  const izbor = Array.from({ length: 12 }, (_, i) => new Date(sad.getFullYear(), sad.getMonth() + i, 1));

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="font-display text-2xl font-bold">Plan poslovanja</h1>
          <p className="max-w-3xl text-muted">
            Koliko ostaje u mjesecu sa zadatim brojem narudžbi. Prosječna narudžba je iz stvarnih narudžbi zadnja tri mjeseca,
            ali po <b className="text-ink">današnjim</b> cijenama korpica i Špajza, nabavnim cijenama i troškovima.
          </p>
        </div>
        <div className="flex gap-2 print:hidden">
          <Link href="/admin/poslovanje">
            <Button variant="secondary" size="sm">
              <ChartColumn className="h-4 w-4" /> Poslovanje
            </Button>
          </Link>
          <Button variant="secondary" size="sm" onClick={() => window.print()} disabled={!d}>
            <Printer className="h-4 w-4" /> Štampaj
          </Button>
        </div>
      </div>

      <div className="flex flex-wrap items-end gap-3 rounded-card bg-white p-4 shadow-card print:hidden">
        <Field label="Narudžbi mjesečno">
          <Input inputMode="numeric" value={narudzbi} onChange={(e) => setNarudzbi(e.target.value)} className="w-28" />
        </Field>
        <Field label="Mjesec plana">
          <Select value={mjesec} onChange={(e) => setMjesec(e.target.value)}>
            {izbor.map((x) => (
              <option key={x.toISOString()} value={`${x.getFullYear()}-${x.getMonth() + 1}`}>
                {MJESECI[x.getMonth()]} {x.getFullYear()}.
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Plate i ostalo, mjesečno (KM)">
          <Input inputMode="decimal" value={dodatno} onChange={(e) => setDodatno(e.target.value)} placeholder="0" className="w-36" />
        </Field>
        <p className="max-w-sm text-xs text-muted">
          Ovdje upiši plate vozača i svoju, doprinose i sve mjesečno što nije u{" "}
          <Link href="/admin/poslovanje/troskovi" className="underline">
            Troškovima
          </Link>
          . Ništa se ne snima — služi samo za plan.
        </p>
      </div>

      {!ispravno ? (
        <p className="text-sm text-red-700">Upiši broj narudžbi i iznos kao brojeve.</p>
      ) : q.isLoading ? (
        <LoadingBlock />
      ) : q.isError || !d ? (
        <EmptyState title="Plan nije izračunat" description={q.error instanceof ApiRequestError ? q.error.message : ""} />
      ) : (
        <>
          <div className="grid grid-cols-2 gap-3 sm:gap-4 lg:grid-cols-5">
            <KarticaPoslovanja label="Neto prihod" vrijednost={formatPrice(d.plan.neto)} hint={`${d.narudzbi} × ${formatPrice(d.prosjek.neto)}`} />
            <KarticaPoslovanja label="Roba, ambalaža, gorivo" vrijednost={formatPrice(d.plan.varijabilno)} hint="raste sa svakom narudžbom" />
            <KarticaPoslovanja label="Fiksni troškovi" vrijednost={formatPrice(d.fiksni)} hint="isti bez obzira na broj narudžbi" />
            <KarticaPoslovanja label="Dobit" vrijednost={formatPrice(d.plan.dobit)} hint={`marža dobiti ${pct(d.plan.marzaDobiti)}`} />
            <KarticaPoslovanja
              label="Prag isplativosti"
              vrijednost={d.prag == null ? "—" : `${d.prag} narudžbi`}
              hint={d.prag == null ? "narudžba ne pokriva ni svoje troškove" : "mjesečno, da se pokriju fiksni"}
            />
          </div>

          {(d.fiksniStavke.every((f) => !/plat/i.test(f.vrsta)) || d.narudzbiSedmicno > d.kapacitetSedmicno) && (
            <div className="flex gap-3 rounded-card border border-honey-300 bg-honey-100 p-4 text-sm text-honey-700">
              <TriangleAlert className="h-5 w-5 shrink-0" />
              <div className="space-y-1">
                {d.fiksniStavke.every((f) => !/plat/i.test(f.vrsta)) && (
                  <p>
                    U planu nema plata — dobit je ono što ostaje <b>prije</b> plata vozača, tvoje plate i doprinosa. Upiši ih u
                    „Plate i ostalo“ iznad da vidiš stvarnu zaradu.
                  </p>
                )}
                {d.narudzbiSedmicno > d.kapacitetSedmicno && (
                  <p>
                    {d.narudzbiSedmicno} narudžbi sedmično je više od kapaciteta dostave ({d.kapacitetSedmicno}) — treba još
                    vozača ili dan dostave.
                  </p>
                )}
              </div>
            </div>
          )}

          <div className="grid gap-6 lg:grid-cols-2">
            <Odjeljak naslov="Prosječna narudžba" opis={`Iz ${d.narudzbiOsnova} stvarnih narudžbi zadnja tri mjeseca, po današnjim cijenama.`}>
              <Redovi
                redovi={[
                  ["Korpice", d.prosjek.korpice],
                  ["Špajz", d.prosjek.spajz],
                  ["Dostava", d.prosjek.dostava],
                  ["Popusti (promo, poeni)", -d.prosjek.popusti],
                  ["Neto prihod", d.prosjek.neto, "zbir"],
                  ["Nabavno korpica", -d.prosjek.nabavnoKorpica],
                  ["Kutije korpica", -d.prosjek.kutijeKorpica],
                  ["Nabavno Špajza", -d.prosjek.nabavnoSpajz],
                  ["Ambalaža Špajza", -d.prosjek.ambalazaSpajz],
                  ["Gorivo po narudžbi", -d.prosjek.gorivo],
                  ["Ostaje po narudžbi", d.prosjek.doprinos, "zbir"],
                ]}
              />
            </Odjeljak>

            <Odjeljak naslov={`Mjesec sa ${d.narudzbi} narudžbi — ${MJESECI[d.mjesec - 1]} ${d.godina}.`}>
              <Redovi
                redovi={[
                  ["Neto prihod", d.plan.neto, "zbir"],
                  ["Nabavka robe", -d.plan.nabavno],
                  ["Kutije i ambalaža", -d.plan.ambalaza],
                  ["Gorivo", -d.plan.gorivo],
                  ...d.fiksniStavke.map((f) => [`${f.vrsta}${f.ponavljajuci ? "" : " (prosjek)"}`, -f.iznos] as [string, number]),
                  ["Dobit", d.plan.dobit, "zbir"],
                ]}
              />
              <p className="text-xs text-muted">
                Kapacitet dostave: {d.narudzbiSedmicno} narudžbi sedmično od mogućih {d.kapacitetSedmicno}.
              </p>
            </Odjeljak>
          </div>

          <Odjeljak naslov="Šta ako" opis="Isti prosjek, različit broj narudžbi. Fiksni troškovi ostaju isti, pa svaka narudžba preko praga ide skoro cijela u dobit.">
            <Tabela<PlanScenarij>
              kljuc={(s) => String(s.narudzbi)}
              redovi={d.scenariji}
              kolone={[
                { naslov: "Narudžbi", vrijednost: (s) => <span className={cn(s.narudzbi === d.narudzbi && "font-semibold text-brand-700")}>{s.narudzbi}</span> },
                { naslov: "Neto prihod", vrijednost: (s) => formatPrice(s.neto), desno: true },
                { naslov: "Roba, ambalaža, gorivo", vrijednost: (s) => formatPrice(s.varijabilno), desno: true },
                { naslov: "Fiksni", vrijednost: (s) => formatPrice(s.fiksni), desno: true },
                {
                  naslov: "Dobit",
                  vrijednost: (s) => <span className={cn("font-semibold", s.dobit < 0 ? "text-red-700" : "text-ink")}>{formatPrice(s.dobit)}</span>,
                  desno: true,
                },
                { naslov: "Marža dobiti", vrijednost: (s) => pct(s.marzaDobiti), desno: true },
              ]}
            />
          </Odjeljak>

          <Odjeljak naslov="Korpice u prosječnoj narudžbi" opis="Koliko komada korpice ide po narudžbi i šta svaka ostavi poslije nabavke i kutije (tipičan sadržaj mjeseca plana).">
            <Tabela
              kljuc={(k) => k.naziv}
              redovi={d.korpice}
              kolone={[
                { naslov: "Korpica", vrijednost: (k) => k.naziv },
                { naslov: "Po narudžbi", vrijednost: (k) => k.naNarudzbu.toLocaleString("sr-Latn"), desno: true },
                { naslov: "Cijena", vrijednost: (k) => formatPrice(k.cijena), desno: true },
                { naslov: "Nabavno", vrijednost: (k) => formatPrice(k.nabavno), desno: true },
                { naslov: "Kutija", vrijednost: (k) => formatPrice(k.kutija), desno: true },
                { naslov: "Ostaje", vrijednost: (k) => formatPrice(k.zarada), desno: true },
                { naslov: "Marža", vrijednost: (k) => pct(k.cijena ? k.zarada / k.cijena : null), desno: true },
              ]}
            />
          </Odjeljak>

          <Odjeljak naslov="Kako je stvarno bilo" opis="Isti mjeseci po cijenama koje su tada važile (iz analitike) — za poređenje sa planom.">
            <Tabela
              kljuc={(o) => `${o.godina}-${o.mjesec}`}
              redovi={d.osnova}
              kolone={[
                { naslov: "Mjesec", vrijednost: (o) => `${MJESECI[o.mjesec - 1]} ${o.godina}.` },
                { naslov: "Narudžbi", vrijednost: (o) => o.narudzbi, desno: true },
                { naslov: "Neto", vrijednost: (o) => formatPrice(o.neto), desno: true },
                { naslov: "Dobit", vrijednost: (o) => <span className={cn(o.dobit < 0 && "text-red-700")}>{formatPrice(o.dobit)}</span>, desno: true },
              ]}
            />
          </Odjeljak>

          <p className="text-xs text-muted">
            Nije uračunato: porez na dobit i doprinosi (osim ako ih upišeš gore), amortizacija kombija, servis i registracija
            (ako nisu u Troškovima), otpis robe koja propadne. Gorivo je prosjek po narudžbi iz osnovnih mjeseci.
          </p>
        </>
      )}
    </div>
  );
}

function Redovi({ redovi }: { redovi: [string, number, "zbir"?][] }) {
  return (
    <table className="w-full text-sm tabular-nums">
      <tbody>
        {redovi.map(([naziv, iznos, zbir]) => (
          <tr key={naziv} className={cn(zbir && "border-t border-line font-semibold text-ink")}>
            <td className="py-1 pr-3">{naziv}</td>
            <td className={cn("py-1 text-right", iznos < 0 && !zbir && "text-muted", zbir && iznos < 0 && "text-red-700")}>
              {iznos < 0 && !zbir ? `− ${formatPrice(-iznos)}` : formatPrice(iznos)}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
