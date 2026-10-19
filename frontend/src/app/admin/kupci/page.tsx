"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { AdminLadica } from "@/components/admin/admin-ladica";
import { Field, Input } from "@/components/ui/field";
import { LoadingBlock } from "@/components/ui/spinner";
import { adminApi } from "@/lib/api/endpoints";
import { STATUS_META } from "@/lib/orders";
import { PLAN_TEKST } from "@/lib/planovi";
import { cn, formatDate, formatPrice } from "@/lib/utils";
import type { AdminKupac } from "@/types/domain";

/** Kupci: ko naručuje, koliko, sa kojom pretplatom i poenima. Klik otvara narudžbe, pretplate i adrese. */
export default function AdminKupciPage() {
  const [unos, setUnos] = useState("");
  const [pretraga, setPretraga] = useState("");
  const [izabran, setIzabran] = useState<AdminKupac | null>(null);

  useEffect(() => {
    const t = setTimeout(() => setPretraga(unos.trim()), 300);
    return () => clearTimeout(t);
  }, [unos]);

  const q = useQuery({ queryKey: ["admin", "kupci", pretraga], queryFn: () => adminApi.kupci(pretraga) });

  return (
    <div className="space-y-6">
      <div>
        <h1 className="font-display text-2xl font-bold text-ink">Kupci</h1>
        <p className="text-sm text-muted">Registrovani kupci, najskorije aktivni prvi (do 200). Gosti su vidljivi u narudžbama.</p>
      </div>

      <div className="max-w-sm">
        <Field label="Pretraga po imenu, e-mailu ili telefonu">
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
        <div className="overflow-x-auto rounded-card border border-line bg-surface">
          <table className="w-full text-left text-sm">
            <thead className="border-b border-line text-xs uppercase text-muted">
              <tr>
                <th className="px-4 py-2 font-medium">Kupac</th>
                <th className="px-4 py-2 font-medium">Narudžbe</th>
                <th className="px-4 py-2 font-medium">Isporučeno</th>
                <th className="px-4 py-2 font-medium">Pretplate</th>
                <th className="px-4 py-2 font-medium">Poeni</th>
                <th className="px-4 py-2 font-medium">Zadnja narudžba</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-line">
              {q.data.map((k) => (
                <tr key={k.id} className="cursor-pointer hover:bg-sand" onClick={() => setIzabran(k)}>
                  <td className="px-4 py-2.5">
                    <button type="button" className="text-left" onClick={() => setIzabran(k)}>
                      <span className="block font-medium text-ink">{k.ime}</span>
                      <span className="block text-muted">
                        {k.email}
                        {!k.emailPotvrdjen && " · nepotvrđen"}
                      </span>
                    </button>
                  </td>
                  <td className="px-4 py-2.5 tabular-nums">{k.brojNarudzbi}</td>
                  <td className="px-4 py-2.5 tabular-nums">{formatPrice(k.isporuceno)}</td>
                  <td className="px-4 py-2.5 tabular-nums">{k.aktivnePretplate || "—"}</td>
                  <td className="px-4 py-2.5 tabular-nums">{k.poeni}</td>
                  <td className="px-4 py-2.5 text-muted">{k.zadnjaNarudzba ? formatDate(k.zadnjaNarudzba) : "—"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {izabran && <Kupac kupac={izabran} onClose={() => setIzabran(null)} />}
    </div>
  );
}

function Kupac({ kupac, onClose }: { kupac: AdminKupac; onClose: () => void }) {
  const q = useQuery({ queryKey: ["admin", "kupci", "detalj", kupac.id], queryFn: () => adminApi.kupac(kupac.id) });
  const d = q.data;

  return (
    <AdminLadica naslov={kupac.ime} podnaslov={kupac.email} onClose={onClose}>
      <div className="flex-1 space-y-6 overflow-y-auto p-6 text-sm">
        {q.isLoading || !d ? (
          <LoadingBlock />
        ) : (
          <>
            <dl className="grid grid-cols-2 gap-3">
              <div>
                <dt className="text-muted">Telefon</dt>
                <dd className="font-medium">{d.telefon || "—"}</dd>
              </div>
              <div>
                <dt className="text-muted">Registrovan</dt>
                <dd className="font-medium">{formatDate(d.registrovan)}</dd>
              </div>
              <div>
                <dt className="text-muted">E-mail</dt>
                <dd className="font-medium">{d.emailPotvrdjen ? "potvrđen" : "nije potvrđen"}</dd>
              </div>
              <div>
                <dt className="text-muted">Poeni</dt>
                <dd className="font-medium">
                  {d.poeni}{" "}
                  <Link href="/admin/poeni" className="text-brand-700 hover:underline">
                    korekcija →
                  </Link>
                </dd>
              </div>
            </dl>

            {d.adrese.length > 0 && (
              <section>
                <h3 className="mb-1 font-semibold">Adrese</h3>
                <ul className="space-y-1 text-muted">
                  {d.adrese.map((a) => (
                    <li key={a}>{a}</li>
                  ))}
                </ul>
              </section>
            )}

            <section>
              <h3 className="mb-1 font-semibold">Pretplate</h3>
              {d.pretplate.length === 0 ? (
                <p className="text-muted">Nema pretplata.</p>
              ) : (
                <ul className="space-y-1">
                  {d.pretplate.map((p) => (
                    <li key={p.id}>
                      {p.korpica} · {PLAN_TEKST[p.plan as keyof typeof PLAN_TEKST] ?? p.plan} · {p.status} · {p.brojIsporuka} isporuka
                    </li>
                  ))}
                </ul>
              )}
            </section>

            <section>
              <h3 className="mb-1 font-semibold">Narudžbe</h3>
              {d.narudzbe.length === 0 ? (
                <p className="text-muted">Nema narudžbi.</p>
              ) : (
                <ul className="divide-y divide-line rounded-card border border-line">
                  {d.narudzbe.map((n) => (
                    <li key={n.id} className="flex items-center justify-between gap-3 px-3 py-2">
                      <span>
                        <Link href={`/admin/orders?pretraga=${encodeURIComponent(n.code)}`} className="font-medium text-brand-700 hover:underline">
                          {n.code}
                        </Link>
                        <span className="block text-xs text-muted">
                          {formatDate(n.kreirana)} · sedmica {n.sedmica}
                          {n.pretplatna && " · pretplata"}
                        </span>
                      </span>
                      <span className="text-right">
                        <span className={cn("rounded-full px-2 py-0.5 text-xs font-semibold", STATUS_META[n.status].badge)}>
                          {STATUS_META[n.status].label}
                        </span>
                        <span className="block tabular-nums">{formatPrice(n.ukupno)}</span>
                      </span>
                    </li>
                  ))}
                </ul>
              )}
            </section>
          </>
        )}
      </div>
    </AdminLadica>
  );
}
