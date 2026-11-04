"use client";

import { Suspense } from "react";
import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { CalendarRange, ChevronLeft, ChevronRight, Printer, Receipt, Target } from "lucide-react";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { LoadingBlock } from "@/components/ui/spinner";
import { KarticaPoslovanja } from "@/components/admin/poslovanje/kartica-poslovanja";
import { Odjeljak } from "@/components/admin/poslovanje/odjeljak";
import { Tabela } from "@/components/admin/poslovanje/tabela";
import { PdfPoslovanjaDugme } from "@/components/admin/poslovanje/pdf-dugme";
import { TroskoviOdjeljak } from "@/components/admin/poslovanje/troskovi-odjeljak";
import { adminApi } from "@/lib/api/endpoints";
import type { ArtikalProdaje } from "@/lib/api/contracts";
import { MJESECI, procenat, susjedniMjesec } from "@/lib/admin/poslovanje";
import { formatPrice } from "@/lib/utils";

const broj = (x: number) => Number(x.toFixed(2)).toLocaleString("sr-Latn");
const PLANOVI: Record<string, string> = { Nedjeljna: "Nedjeljna", Mjesecna: "Mjesečna", TriMjeseca: "3 mjeseca" };

/**
 * Analitika poslovanja za mjesec (po datumu isporuke). Bez ?godina=&mjesec= otvara tekući mjesec
 * — server ga bira po Sarajevu, pa URL dobija oba parametra tek na prvi pomak strelicom.
 */
export default function AdminPoslovanjePage() {
  return (
    <Suspense fallback={<LoadingBlock />}>
      <Poslovanje />
    </Suspense>
  );
}

function ArtikliTabela({ naslov, lista }: { naslov: string; lista: ArtikalProdaje[] }) {
  return (
    <div className="space-y-2">
      <h3 className="font-semibold">{naslov}</h3>
      <Tabela
        kljuc={(r) => `${r.naziv}|${r.jedinica}`}
        redovi={lista}
        kolone={[
          { naslov: "Artikal", vrijednost: (r) => r.naziv },
          { naslov: "Količina", vrijednost: (r) => `${broj(r.kolicina)} ${r.jedinica}`, desno: true },
          { naslov: "Prihod", vrijednost: (r) => formatPrice(r.prihod), desno: true },
          { naslov: "Zarada", vrijednost: (r) => formatPrice(r.zarada), desno: true },
        ]}
      />
    </div>
  );
}

function Poslovanje() {
  const params = useSearchParams();
  const router = useRouter();
  const pathname = usePathname();
  const g = Number(params.get("godina")) || undefined;
  const m = Number(params.get("mjesec")) || undefined;

  const q = useQuery({
    queryKey: ["admin", "poslovanje", g ?? "tekuci", m ?? "tekuci"],
    queryFn: () => adminApi.poslovanje(g, m),
  });
  const d = q.data;

  function idi(pomak: number) {
    if (!d) return;
    const s = susjedniMjesec(d.godina, d.mjesec, pomak);
    router.replace(`${pathname}?godina=${s.godina}&mjesec=${s.mjesec}`, { scroll: false });
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="font-display text-2xl font-bold">
            Poslovanje{d ? ` — ${MJESECI[d.mjesec - 1]} ${d.godina}.` : ""}
          </h1>
          <p className="text-muted">Mjesec po datumu isporuke; prihod samo od isporučenih narudžbi.</p>
        </div>
        <div className="flex flex-wrap items-center gap-2 print:hidden">
          <Button variant="secondary" size="sm" onClick={() => idi(-1)} disabled={!d} aria-label="Prethodni mjesec">
            <ChevronLeft className="h-4 w-4" />
          </Button>
          <Button variant="secondary" size="sm" onClick={() => idi(1)} disabled={!d} aria-label="Sljedeći mjesec">
            <ChevronRight className="h-4 w-4" />
          </Button>
          <Link href={`/admin/poslovanje/godina${d ? `?godina=${d.godina}` : ""}`}>
            <Button variant="secondary" size="sm">
              <CalendarRange className="h-4 w-4" /> Godina
            </Button>
          </Link>
          <Link href={`/admin/poslovanje/troskovi${d ? `?godina=${d.godina}&mjesec=${d.mjesec}` : ""}`}>
            <Button variant="secondary" size="sm">
              <Receipt className="h-4 w-4" /> Troškovi
            </Button>
          </Link>
          <Link href="/admin/poslovanje/plan">
            <Button variant="secondary" size="sm">
              <Target className="h-4 w-4" /> Plan
            </Button>
          </Link>
          <Button variant="secondary" size="sm" onClick={() => window.print()} disabled={!d}>
            <Printer className="h-4 w-4" /> Štampaj
          </Button>
          {d && <PdfPoslovanjaDugme d={d} />}
        </div>
      </div>

      {q.isLoading ? (
        <LoadingBlock />
      ) : q.isError || !d ? (
        <EmptyState
          title="Ne mogu da učitam analitiku"
          description="Provjeri konekciju pa pokušaj ponovo."
          action={<Button onClick={() => q.refetch()}>Pokušaj ponovo</Button>}
        />
      ) : (
        <>
          {d.prihod.procjena && (
            <p className="rounded-card bg-honey-100 px-4 py-3 text-sm font-medium text-honey-700">
              Procjena: dio narudžbi je stariji od pamćenja nabavne cijene, pa je zarada računata po današnjim
              nabavnim cijenama.
            </p>
          )}
          {d.prihod.bezNabavneCijene > 0 && (
            <p className="rounded-card bg-red-50 px-4 py-3 text-sm font-medium text-red-700">
              {d.prihod.bezNabavneCijene} stavki nema nabavnu cijenu — nisu u trošku, pa je zarada prikazana veća
              nego što jeste.
            </p>
          )}

          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 2xl:grid-cols-6">
            <KarticaPoslovanja label="Neto prihod" vrijednost={formatPrice(d.prihod.neto)} sada={d.prihod.neto} prije={d.prethodni.neto} />
            <KarticaPoslovanja
              label="Zarada"
              vrijednost={`${formatPrice(d.prihod.zarada)} · ${procenat(d.prihod.marza)}`}
              sada={d.prihod.zarada}
              prije={d.prethodni.zarada}
            />
            <KarticaPoslovanja
              label="Dobit"
              vrijednost={`${formatPrice(d.troskovi.dobit)} · ${procenat(d.troskovi.marzaDobiti)}`}
              sada={d.troskovi.dobit}
              prije={d.prethodniTroskovi.dobit}
            />
            <KarticaPoslovanja
              label="Narudžbe"
              vrijednost={String(d.prihod.brojNarudzbi)}
              sada={d.prihod.brojNarudzbi}
              prije={d.prethodni.brojNarudzbi}
            />
            <KarticaPoslovanja
              label="Prosječna narudžba"
              vrijednost={formatPrice(d.prihod.prosjecnaNarudzba)}
              sada={d.prihod.prosjecnaNarudzba}
              prije={d.prethodni.prosjecnaNarudzba}
            />
            <KarticaPoslovanja
              label="Aktivni kupci"
              vrijednost={String(d.kupci.aktivni)}
              sada={d.kupci.aktivni}
              prije={d.kupci.aktivniPrethodni}
            />
          </div>

          <Odjeljak naslov="Prihod i zarada">
            <Tabela
              kljuc={(r) => r.stavka}
              redovi={[
                { stavka: "Promet robe", iznos: formatPrice(d.prihod.prometRobe) },
                { stavka: "Dostava naplaćena", iznos: formatPrice(d.prihod.dostava) },
                { stavka: "Promo popusti", iznos: `− ${formatPrice(d.prihod.promoPopust)}` },
                { stavka: "Popust od poena", iznos: `− ${formatPrice(d.prihod.poeniPopust)}` },
                { stavka: "Neto prihod", iznos: formatPrice(d.prihod.neto) },
                { stavka: "Nabavni trošak robe", iznos: formatPrice(d.prihod.nabavno) },
                { stavka: "Zarada na robi", iznos: `${formatPrice(d.prihod.zarada)} (${procenat(d.prihod.marza)})` },
                {
                  stavka: "Udio korpica / Špajza u prometu",
                  iznos: `${procenat(d.prihod.udioKorpica)} / ${procenat(d.prihod.udioSpajza)}`,
                },
              ]}
              kolone={[
                { naslov: "Stavka", vrijednost: (r) => r.stavka },
                { naslov: "Iznos", vrijednost: (r) => r.iznos, desno: true },
              ]}
            />
          </Odjeljak>

          <TroskoviOdjeljak d={d} />

          <Odjeljak naslov="Šta se prodaje">
            <h3 className="font-semibold">Korpice</h3>
            <Tabela
              kljuc={(r) => r.naziv}
              redovi={d.prodaja.korpice}
              kolone={[
                { naslov: "Korpica", vrijednost: (r) => r.naziv },
                { naslov: "Komada", vrijednost: (r) => broj(r.komada), desno: true },
                { naslov: "Prihod", vrijednost: (r) => formatPrice(r.prihod), desno: true },
                { naslov: "Nabavno", vrijednost: (r) => formatPrice(r.nabavno), desno: true },
                { naslov: "Marža", vrijednost: (r) => procenat(r.marza), desno: true },
              ]}
            />
            <h3 className="font-semibold">Špajz po kategoriji</h3>
            <Tabela
              kljuc={(r) => r.kategorija}
              redovi={d.prodaja.spajz}
              kolone={[
                { naslov: "Kategorija", vrijednost: (r) => r.kategorija },
                { naslov: "Prihod", vrijednost: (r) => formatPrice(r.prihod), desno: true },
                { naslov: "Nabavno", vrijednost: (r) => formatPrice(r.nabavno), desno: true },
                { naslov: "Marža", vrijednost: (r) => procenat(r.marza), desno: true },
              ]}
            />
            <div className="grid gap-6 lg:grid-cols-2">
              <ArtikliTabela naslov="Top 10 artikala" lista={d.prodaja.topArtikli} />
              <ArtikliTabela naslov="Najslabijih 10" lista={d.prodaja.najslabijiArtikli} />
            </div>
            <p className="text-xs text-muted">Artikli iz korpica su uračunati po cijeni iz korpice, zajedno sa Špajzom.</p>
          </Odjeljak>

          <Odjeljak naslov="Kupci">
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
              <KarticaPoslovanja label="Novi / povratni" vrijednost={`${d.kupci.novi} / ${d.kupci.povratni}`} />
              <KarticaPoslovanja
                label="Zadržavanje"
                vrijednost={procenat(d.kupci.zadrzavanje)}
                hint="Od kupaca prošlog mjeseca, naručili i ovog"
              />
              <KarticaPoslovanja
                label="Potrošnja po kupcu"
                vrijednost={formatPrice(d.kupci.potrosnjaPoKupcu)}
                hint={`${broj(d.kupci.narudzbiPoKupcu)} narudžbi po kupcu`}
              />
              <KarticaPoslovanja
                label="Registrovani / gosti"
                vrijednost={`${d.kupci.registrovani} / ${d.kupci.gosti}`}
                hint={`${d.kupci.noviPrekoPreporuke} novih preko preporuke`}
              />
            </div>
            <h3 className="font-semibold">Top kupci</h3>
            <Tabela
              kljuc={(r, i) => `${r.ime}|${i}`}
              redovi={d.kupci.topKupci}
              kolone={[
                { naslov: "Kupac", vrijednost: (r) => (r.gost ? `${r.ime} (gost)` : r.ime) },
                { naslov: "Narudžbi", vrijednost: (r) => r.narudzbi, desno: true },
                { naslov: "Potrošio", vrijednost: (r) => formatPrice(r.neto), desno: true },
              ]}
            />
          </Odjeljak>

          <Odjeljak naslov="Pretplate">
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
              <KarticaPoslovanja label="Aktivne na kraju mjeseca" vrijednost={String(d.pretplate.aktivneNaKraju)} />
              <KarticaPoslovanja label="Nove / otkazane" vrijednost={`${d.pretplate.nove} / ${d.pretplate.otkazane}`} />
              <KarticaPoslovanja label="Stopa otkazivanja" vrijednost={procenat(d.pretplate.stopaOtkazivanja)} />
              <KarticaPoslovanja label="Udio prihoda iz pretplata" vrijednost={procenat(d.pretplate.udioPrihoda)} />
            </div>
            {d.pretplate.pauzirane != null && (
              <p className="text-sm text-muted">Trenutno pauzirano: {d.pretplate.pauzirane}.</p>
            )}
            <Tabela
              kljuc={(r) => r.plan}
              redovi={d.pretplate.poPlanu}
              kolone={[
                { naslov: "Plan", vrijednost: (r) => PLANOVI[r.plan] ?? r.plan },
                { naslov: "Aktivne", vrijednost: (r) => r.aktivne, desno: true },
              ]}
            />
          </Odjeljak>

          <Odjeljak naslov="Dostava i kvalitet">
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
              <KarticaPoslovanja
                label="Uspješnost isporuke"
                vrijednost={procenat(d.dostava.uspjesnost)}
                hint={`${d.dostava.isporucene} isporučeno, ${d.dostava.neuspjele} neuspjelo`}
              />
              <KarticaPoslovanja
                label="Otkazane"
                vrijednost={`${d.dostava.otkazane} · ${procenat(d.dostava.otkazaneUdio)}`}
                hint={formatPrice(d.dostava.otkazaneVrijednost)}
              />
              <KarticaPoslovanja
                label="Ocjena isporuke"
                vrijednost={d.dostava.prosjecnaOcjena == null ? "—" : `${broj(d.dostava.prosjecnaOcjena)} / 5`}
                hint={`${d.dostava.brojOcjena} ocjena`}
              />
            </div>
            <Tabela
              kljuc={(r) => r.zona}
              redovi={d.dostava.poZoni}
              kolone={[
                { naslov: "Zona", vrijednost: (r) => r.zona },
                { naslov: "Narudžbi", vrijednost: (r) => r.narudzbi, desno: true },
                { naslov: "Neto", vrijednost: (r) => formatPrice(r.neto), desno: true },
              ]}
            />
          </Odjeljak>

          <Odjeljak naslov="Gazdinstva" opis="Koliko je robe uzeto od kog gazdinstva, po nabavnoj cijeni.">
            {d.gazdinstva.length === 0 ? (
              <p className="text-sm text-muted">Nema podataka za ovaj mjesec.</p>
            ) : (
              d.gazdinstva.map((gz) => (
                <div key={`${gz.naziv}|${gz.selo}`} className="space-y-2">
                  <div className="flex items-baseline justify-between gap-3">
                    <h3 className="font-semibold">
                      {gz.naziv}
                      {gz.selo && <span className="font-normal text-muted"> · {gz.selo}</span>}
                    </h3>
                    <p className="font-display font-bold tabular-nums">{formatPrice(gz.nabavno)}</p>
                  </div>
                  <Tabela
                    kljuc={(r) => `${r.naziv}|${r.jedinica}`}
                    redovi={gz.artikli}
                    kolone={[
                      { naslov: "Artikal", vrijednost: (r) => r.naziv },
                      { naslov: "Količina", vrijednost: (r) => `${broj(r.kolicina)} ${r.jedinica}`, desno: true },
                      { naslov: "Nabavno", vrijednost: (r) => formatPrice(r.nabavno), desno: true },
                    ]}
                  />
                </div>
              ))
            )}
          </Odjeljak>
        </>
      )}
    </div>
  );
}
