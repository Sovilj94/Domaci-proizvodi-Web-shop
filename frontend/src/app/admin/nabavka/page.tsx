"use client";

import { Suspense, useEffect } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { Banknote, ClipboardList, Phone, Printer, ShoppingBag, Tractor } from "lucide-react";
import { IzborSedmice, useIzabranaSedmica } from "@/components/admin/izbor-sedmice";
import { StatCard } from "@/components/admin/stat-card";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { LoadingBlock } from "@/components/ui/spinner";
import { poProizvodjacu } from "@/lib/admin/nabavka";
import { adminKorpiceApi } from "@/lib/api/endpoints";
import { cn, formatDateTime, formatPrice } from "@/lib/utils";

const broj = (x: number) => Number(x.toFixed(2)).toLocaleString("sr-Latn");
const datumIsporuke = (iso: string) =>
  new Date(iso).toLocaleDateString("sr-Latn", { weekday: "long", day: "numeric", month: "numeric", timeZone: "Europe/Sarajevo" });

/**
 * Nabavka sedmice: kad prođe rok, korpice su zaključane i sve je naručeno — ovdje stoji koliko
 * robe treba kupiti, po proizvođaču. Bez ?datum= otvara sedmicu kojoj je rok upravo prošao
 * (server zna rok); strelicama se ide na ostale, pa i na otvorenu sedmicu kao predračun.
 */
export default function AdminNabavkaPage() {
  return (
    <Suspense fallback={<LoadingBlock />}>
      <Nabavka />
    </Suspense>
  );
}

function Nabavka() {
  const params = useSearchParams();
  const router = useRouter();
  const pathname = usePathname();
  const izbor = useIzabranaSedmica();
  const izabrana = params.has("datum");

  const q = useQuery({
    queryKey: izabrana ? ["admin", "korpice", "pakerska", izbor.godina, izbor.sedmica] : ["admin", "korpice", "pakerska", "nabavka"],
    queryFn: () => (izabrana ? adminKorpiceApi.pakerskaLista(izbor.godina, izbor.sedmica) : adminKorpiceApi.pakerskaLista()),
    refetchInterval: 60_000,
  });

  // Prva posjeta: sedmicu bira server, pa se upiše u URL da strelice kreću od nje.
  useEffect(() => {
    if (izabrana || !q.data) return;
    const next = new URLSearchParams(params.toString());
    next.set("datum", q.data.isporuka.slice(0, 10));
    router.replace(`${pathname}?${next}`, { scroll: false });
  }, [izabrana, q.data, params, pathname, router]);

  const d = q.data;
  const grupe = d ? poProizvodjacu(d.stavke) : [];

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="font-display text-2xl font-bold">Nabavka{d ? ` za sedmicu ${d.sedmica}.` : ""}</h1>
          <p className="text-muted">
            {d ? `Isporuka: ${datumIsporuke(d.isporuka)} — sve što treba kupiti, po proizvođaču.` : "Koliko robe treba kupiti za sedmicu."}
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-3 print:hidden">
          <IzborSedmice s={izbor} />
          {d && d.stavke.length > 0 && (
            <Button variant="secondary" size="sm" onClick={() => window.print()}>
              <Printer className="h-4 w-4" /> Štampaj
            </Button>
          )}
        </div>
      </div>

      {q.isLoading ? (
        <LoadingBlock />
      ) : q.isError || !d ? (
        <EmptyState
          title="Ne mogu da učitam nabavku"
          description="Provjeri konekciju pa pokušaj ponovo."
          action={<Button onClick={() => q.refetch()}>Pokušaj ponovo</Button>}
        />
      ) : (
        <>
          <p
            className={cn(
              "rounded-card px-4 py-3 text-sm font-medium",
              d.zakljucena ? "bg-brand-50 text-brand-800" : "bg-honey-100 text-honey-700",
            )}
          >
            {d.zakljucena
              ? `Sedmica je zaključena (${formatDateTime(d.rok)}) — korpice su zaključane i sve je naručeno. Ovo je konačan spisak.`
              : `Ovo još nije konačno: narudžbe se primaju do ${formatDateTime(d.rok)}, a kupci do tada mijenjaju korpice.`}
          </p>

          {d.stavke.length === 0 ? (
            <EmptyState
              title="Nema šta da se nabavi"
              description={d.brojNarudzbi === 0 ? "Za ovu sedmicu nema nijedne narudžbe." : "Narudžbe ove sedmice nemaju robe za nabavku."}
            />
          ) : (
            <>
              <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
                <StatCard
                  label="Ukupno nabavno"
                  value={formatPrice(d.nabavnaVrijednost)}
                  hint={d.bezNabavneCijene > 0 ? `${d.bezNabavneCijene} bez nabavne cijene` : undefined}
                  icon={<Banknote className="h-4 w-4" />}
                  accent="honey"
                />
                <StatCard label="Artikala" value={d.stavke.length} icon={<ClipboardList className="h-4 w-4" />} accent="brand" />
                <StatCard label="Proizvođača" value={grupe.length} icon={<Tractor className="h-4 w-4" />} accent="purple" />
                <StatCard
                  label="Narudžbi"
                  value={d.brojNarudzbi}
                  icon={<ShoppingBag className="h-4 w-4" />}
                  accent="blue"
                />
              </div>

              <div className="grid gap-4 lg:grid-cols-2 print:grid-cols-1">
                {grupe.map((g) => (
                  <section key={`${g.proizvodjac}|${g.selo}`} className="break-inside-avoid rounded-card bg-white p-5 shadow-card">
                    <header className="mb-3 flex items-start justify-between gap-3 border-b border-line pb-3">
                      <div className="min-w-0">
                        <h2 className="font-display text-lg font-semibold">{g.proizvodjac || "Bez proizvođača"}</h2>
                        <p className="text-sm text-muted">
                          {g.selo}
                          {g.telefon && (
                            <a href={`tel:${g.telefon}`} className="ml-2 inline-flex items-center gap-1 font-medium text-brand-700 hover:underline">
                              <Phone className="h-3.5 w-3.5" /> {g.telefon}
                            </a>
                          )}
                        </p>
                      </div>
                      <p className="shrink-0 font-display text-lg font-bold">{g.trosak > 0 ? formatPrice(g.trosak) : "—"}</p>
                    </header>
                    <ul className="divide-y divide-line">
                      {g.stavke.map((r) => (
                        <li key={r.artikal} className="flex items-center justify-between gap-3 py-2">
                          <div className="min-w-0">
                            <p className="font-medium">
                              {r.artikal}
                              {r.artikalObrisan && (
                                <span className="ml-2 rounded-full bg-red-100 px-2 py-0.5 text-xs font-semibold text-red-700">
                                  Obrisan artikal — provjeriti
                                </span>
                              )}
                            </p>
                            <p className="text-xs text-muted">
                              {[r.uKorpicama > 0 && `korpice ${broj(r.uKorpicama)}`, r.uSpajzu > 0 && `Špajz ${broj(r.uSpajzu)}`]
                                .filter(Boolean)
                                .join(" · ")}
                              {r.fali > 0 && (
                                <span className="text-red-700"> · na stanju samo {broj(r.naStanju)}, fali {broj(r.fali)}</span>
                              )}
                            </p>
                          </div>
                          <p className="shrink-0 text-right font-display text-xl font-bold tabular-nums">
                            {broj(r.kolicina)} <span className="text-sm font-semibold text-muted">{r.jedinica}</span>
                          </p>
                        </li>
                      ))}
                    </ul>
                  </section>
                ))}
              </div>
            </>
          )}
        </>
      )}
    </div>
  );
}
