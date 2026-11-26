"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { ArrowRight, Lock, PackageOpen, Repeat, ShoppingBag, ShoppingBasket, Timer } from "lucide-react";
import { StatCard } from "@/components/admin/stat-card";
import { LoadingBlock } from "@/components/ui/spinner";
import { adminApi } from "@/lib/api/endpoints";
import type { PregledSedmice } from "@/lib/api/contracts";
import { preostalo } from "@/lib/odbrojavanje";
import { VELICINE } from "@/lib/planovi";
import { cn } from "@/lib/utils";

const NAZIV_VRSTE: Record<string, string> = {
  povrce: "🥦 Povrće",
  voce: "🍎 Voće",
  mlijecna: "🥛 Mliječna",
  suhomesnata: "🥓 Suhomesnata",
  domacinska: "🧺 Domaćinska",
};

const danFmt = new Intl.DateTimeFormat("bs-BA", { weekday: "short", day: "numeric", month: "numeric" });
const danSatFmt = new Intl.DateTimeFormat("bs-BA", {
  weekday: "short",
  day: "numeric",
  month: "numeric",
  hour: "2-digit",
  minute: "2-digit",
});
const kolicinaFmt = new Intl.NumberFormat("bs-BA", { maximumFractionDigits: 2 });

/** Sat koji kuca svake minute — za odbrojavanje do roka. */
function useSada(): number {
  const [sada, setSada] = useState(() => Date.now());
  useEffect(() => {
    const id = setInterval(() => setSada(Date.now()), 60_000);
    return () => clearInterval(id);
  }, []);
  return sada;
}

export default function AdminDashboardPage() {
  const q = useQuery({
    queryKey: ["admin", "dashboard"],
    queryFn: () => adminApi.pregled(),
    refetchInterval: 60_000,
  });
  const sada = useSada();
  const d = q.data;

  return (
    <div className="space-y-8">
      <div>
        <h1 className="font-display text-2xl font-bold">Pregled</h1>
        <p className="text-muted">Šta se pakuje i šta se upravo naručuje. Novac i poređenja su na Poslovanju.</p>
      </div>

      {q.isLoading || !d ? (
        <LoadingBlock />
      ) : (
        <>
          <TrakaSedmica pakovanje={d.pakovanje} otvorena={d.otvorena} sada={sada} />
          <SedmicaBlok
            naslov={`Pakovanje · sedmica ${d.pakovanje.sedmica}`}
            opis="Zaključeno — ovo ide u kutije i u kombi."
            s={d.pakovanje}
            akcija={{ href: `/admin/nabavka?datum=${d.pakovanje.isporuka.slice(0, 10)}`, natpis: "Nabavka i pakerska lista" }}
          />
          <SedmicaBlok
            naslov={`Otvorena · sedmica ${d.otvorena.sedmica}`}
            opis="Uživo — kupci još naručuju i mijenjaju do roka."
            s={d.otvorena}
            akcija={{ href: "/admin/orders", natpis: "Narudžbe" }}
          />
        </>
      )}
    </div>
  );
}

function TrakaSedmica({ pakovanje, otvorena, sada }: { pakovanje: PregledSedmice; otvorena: PregledSedmice; sada: number }) {
  const ostalo = preostalo(otvorena.rok, sada);
  return (
    <section aria-label="Sedmice" className="grid gap-3 sm:grid-cols-2">
      <div className="flex items-start gap-3 rounded-card bg-white p-4 shadow-card sm:p-5">
        <span className="grid h-10 w-10 shrink-0 place-items-center rounded-full bg-honey-100 text-honey-700">
          <Lock className="h-5 w-5" />
        </span>
        <div className="min-w-0">
          <p className="text-[11px] font-semibold uppercase tracking-wide text-muted">U pakovanju</p>
          <p className="font-display text-lg font-semibold">Sedmica {pakovanje.sedmica}</p>
          <p className="text-sm text-muted">
            Isporuka {danFmt.format(new Date(pakovanje.isporuka))} · zaključeno {danSatFmt.format(new Date(pakovanje.rok))}
          </p>
        </div>
      </div>
      <div className="flex items-start gap-3 rounded-card bg-white p-4 shadow-card sm:p-5">
        <span className="grid h-10 w-10 shrink-0 place-items-center rounded-full bg-brand-100 text-brand-700">
          <Timer className="h-5 w-5" />
        </span>
        <div className="min-w-0">
          <p className="text-[11px] font-semibold uppercase tracking-wide text-muted">Otvorena za narudžbe</p>
          <p className="font-display text-lg font-semibold">
            Sedmica {otvorena.sedmica}
            {ostalo && <span className="ml-2 text-base font-semibold text-brand-700">još {ostalo}</span>}
          </p>
          <p className="text-sm text-muted">
            Rok {danSatFmt.format(new Date(otvorena.rok))} · isporuka {danFmt.format(new Date(otvorena.isporuka))}
          </p>
        </div>
      </div>
    </section>
  );
}

function SedmicaBlok({
  naslov,
  opis,
  s,
  akcija,
}: {
  naslov: string;
  opis: string;
  s: PregledSedmice;
  akcija: { href: string; natpis: string };
}) {
  const spajzKomada = s.spajz.length;
  return (
    <section className="space-y-4">
      <div className="flex flex-wrap items-end justify-between gap-2">
        <div>
          <h2 className="font-display text-xl font-semibold">{naslov}</h2>
          <p className="text-sm text-muted">{opis}</p>
        </div>
        <Link href={akcija.href} className="inline-flex items-center gap-1 text-sm font-semibold text-brand-700 hover:underline">
          {akcija.natpis} <ArrowRight className="h-4 w-4" />
        </Link>
      </div>

      <div className="grid grid-cols-2 gap-3 sm:gap-4 xl:grid-cols-4">
        <StatCard label="Narudžbe" value={s.narudzbi} icon={<ShoppingBag className="h-4 w-4" />} accent="brand" />
        <StatCard label="Korpice" value={s.korpica} icon={<ShoppingBasket className="h-4 w-4" />} accent="honey" />
        <StatCard
          label="Iz pretplata"
          value={s.izPretplata}
          hint={s.narudzbi > 0 ? `${Math.round((s.izPretplata / s.narudzbi) * 100)}% narudžbi` : undefined}
          icon={<Repeat className="h-4 w-4" />}
          accent="purple"
        />
        <StatCard label="Artikli Špajza" value={spajzKomada} hint="različitih artikala" icon={<PackageOpen className="h-4 w-4" />} accent="blue" />
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <TabelaKorpica s={s} />
        <ListaSpajza s={s} />
      </div>
    </section>
  );
}

function TabelaKorpica({ s }: { s: PregledSedmice }) {
  const vrste = [...new Set(s.korpice.map((k) => k.tip))];
  const velicine = VELICINE.filter((v) => s.korpice.some((k) => k.velicina === v.velicina));
  const celija = (tip: string, velicina: string) => s.korpice.find((k) => k.tip === tip && k.velicina === velicina);

  return (
    <div className="rounded-card bg-white p-4 shadow-card sm:p-5">
      <h3 className="mb-3 font-semibold">Korpice po vrsti i veličini</h3>
      {vrste.length === 0 ? (
        <p className="py-4 text-center text-sm text-muted">Nema korpica u ponudi.</p>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="text-left text-xs uppercase tracking-wide text-muted">
                <th className="pb-2 font-medium">Vrsta</th>
                {velicine.map((v) => (
                  <th key={v.velicina} className="pb-2 text-right font-medium">
                    {v.naziv}
                  </th>
                ))}
                <th className="pb-2 text-right font-medium">Ukupno</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-line">
              {vrste.map((tip) => {
                const ukupno = s.korpice.filter((k) => k.tip === tip).reduce((a, k) => a + k.komada, 0);
                return (
                  <tr key={tip}>
                    <td className="py-2 font-medium">{NAZIV_VRSTE[tip] ?? tip}</td>
                    {velicine.map((v) => {
                      const k = celija(tip, v.velicina);
                      return (
                        <td key={v.velicina} className={cn("py-2 text-right tabular-nums", !k?.komada && "text-muted")}>
                          {k ? k.komada : "—"}
                          {k && k.izPretplata > 0 && (
                            <span className="ml-1 text-xs text-purple-700" title="iz pretplata">
                              ({k.izPretplata})
                            </span>
                          )}
                        </td>
                      );
                    })}
                    <td className="py-2 text-right font-semibold tabular-nums">{ukupno}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
          <p className="mt-2 text-xs text-muted">U zagradi: koliko ih je iz pretplata.</p>
        </div>
      )}
    </div>
  );
}

function ListaSpajza({ s }: { s: PregledSedmice }) {
  return (
    <div className="rounded-card bg-white p-4 shadow-card sm:p-5">
      <h3 className="mb-3 font-semibold">Špajz</h3>
      {s.spajz.length === 0 ? (
        <p className="py-4 text-center text-sm text-muted">Niko nije naručio ništa iz Špajza.</p>
      ) : (
        <ul className="max-h-72 divide-y divide-line overflow-y-auto pr-1 text-sm">
          {[...s.spajz].sort((a, b) => a.artikal.localeCompare(b.artikal, "bs")).map((a) => (
            <li key={a.artikal} className="flex items-center justify-between gap-3 py-2">
              <span className="min-w-0 truncate">{a.artikal}</span>
              <span className="shrink-0 font-semibold tabular-nums">
                {kolicinaFmt.format(a.kolicina)} {a.jedinica}
              </span>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
