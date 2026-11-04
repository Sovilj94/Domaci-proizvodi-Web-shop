"use client";

import Link from "next/link";
import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { CalendarDays, Search, Sparkles } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Field, Input } from "@/components/ui/field";
import { SmartImage } from "@/components/ui/smart-image";
import { LoadingBlock } from "@/components/ui/spinner";
import { useToast } from "@/components/ui/toast";
import { NAZIVI as NAZIVI_TIPA } from "@/components/korpice/tipovi-tabovi";
import { ApiRequestError } from "@/lib/api/client";
import { adminApi, adminKorpiceApi, type ArtikalKorpiceUnos, type PostavkeKorpica } from "@/lib/api/endpoints";
import { queryKeys, usePodesavanja } from "@/lib/api/hooks";
import {
  VELICINE,
  type Velicina,
  ciljPoKulturi,
  ciljSadrzaja,
  nabavniBudzet,
  ocjenaKolicine,
  predlozenaKolicina,
} from "@/lib/admin/postavke-korpica";
import { formatMarza } from "@/lib/marza";
import { TIPOVI_KORPICE, type Podesavanja, type Product, type TipKorpice } from "@/types/domain";
import { cn, formatPrice, unitSuffix } from "@/lib/utils";

const POSTAVKE_KLJUC = ["admin", "korpice", "postavke"] as const;
const NAZIV_VELICINE: Record<Velicina, string> = { mala: "Mala", srednja: "Srednja", velika: "Velika" };
const POLJE_KOLICINE = { mala: "kolicinaMala", srednja: "kolicinaSrednja", velika: "kolicinaVelika" } as const;
const MJESECI = ["J", "F", "M", "A", "M", "J", "J", "A", "S", "O", "N", "D"];

function poruka(err: unknown, rezerva: string): string {
  return err instanceof ApiRequestError && err.message.trim() ? err.message : rezerva;
}

/**
 * Postavke korpica na jednom mjestu: pravila za sve korpice, pa po vrsti — cijene i ponuda
 * veličina, koje kategorije ulaze, i za svaki artikal da li ide u korpicu, prioritet i koliko
 * ga ide u malu / srednju / veliku. Sve se čita iz baze i upisuje u nju; generator, editori i
 * „Šta dobijaš u korpici" rade po ovome.
 */
export default function PostavkeKorpicaPage() {
  const [tip, setTip] = useState<TipKorpice>("povrce");
  const postavke = useQuery({ queryKey: POSTAVKE_KLJUC, queryFn: () => adminKorpiceApi.postavke() });
  const proizvodi = useQuery({ queryKey: ["admin", "products"], queryFn: () => adminApi.products() });
  const podesavanja = usePodesavanja();

  const vrsta = postavke.data?.tipovi.find((t) => t.tip === tip);

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="font-display text-2xl font-bold">Postavke korpica</h1>
          <p className="max-w-3xl text-muted">
            Šta ulazi u koju korpicu, koliko čega i po kojim pravilima. Po ovome generator puni sedmični sadržaj, admin
            editor dodaje i mijenja artikle, a kupci vide „Šta dobijaš u korpici“.
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Link
            href="/admin/korpice/kalendar"
            className="inline-flex items-center gap-1.5 rounded-xl border border-line bg-white px-3 py-2 text-sm font-medium hover:border-brand-400"
          >
            <CalendarDays className="h-4 w-4" /> Sezonski kalendar
          </Link>
          <Link
            href="/admin/korpice"
            className="inline-flex items-center gap-1.5 rounded-xl border border-line bg-white px-3 py-2 text-sm font-medium hover:border-brand-400"
          >
            Sadržaj sedmice →
          </Link>
        </div>
      </div>

      {podesavanja.data ? <Pravila podesavanja={podesavanja.data} /> : <LoadingBlock />}

      <nav aria-label="Vrsta korpice" className="no-scrollbar flex gap-1 overflow-x-auto rounded-full border border-line bg-surface p-1 sm:w-fit">
        {TIPOVI_KORPICE.map((t) => {
          const { label, icon: Icon } = NAZIVI_TIPA[t];
          const uPonudi = postavke.data?.tipovi.find((x) => x.tip === t)?.korpice.some((k) => k.uProdaji);
          return (
            <button
              key={t}
              type="button"
              onClick={() => setTip(t)}
              aria-pressed={t === tip}
              className={cn(
                "flex shrink-0 items-center gap-1.5 rounded-full px-3.5 py-1.5 text-sm font-medium transition",
                t === tip ? "bg-ink text-white" : "text-muted hover:text-ink",
              )}
            >
              <Icon className="h-4 w-4" /> {label}
              {uPonudi === false && <span className="text-xs opacity-70">(van ponude)</span>}
            </button>
          );
        })}
      </nav>

      {postavke.isLoading || proizvodi.isLoading || !podesavanja.data ? (
        <LoadingBlock />
      ) : postavke.isError || !postavke.data || !vrsta ? (
        <p className="text-sm text-red-700">Postavke nisu učitane — osvježi stranicu.</p>
      ) : (
        <Vrsta
          key={tip}
          tip={tip}
          vrsta={vrsta}
          kategorije={postavke.data.kategorije}
          korpiceIds={new Set(postavke.data.tipovi.flatMap((t) => t.korpice.map((k) => k.id)))}
          proizvodi={proizvodi.data ?? []}
          podesavanja={podesavanja.data}
        />
      )}
    </div>
  );
}

/* ---------- pravila za sve korpice ---------- */

function Pravila({ podesavanja }: { podesavanja: Podesavanja }) {
  const qc = useQueryClient();
  const { show } = useToast();
  const [f, setF] = useState(() => ({
    vrijednost: String(Math.round(podesavanja.koeficijentVrijednosti * 100)),
    marza: String(Math.round(podesavanja.minimalnaMarza * 100)),
    zamjene: String(podesavanja.brojZamjena),
    mala: String(podesavanja.minArtikalaMala),
    srednja: String(podesavanja.minArtikalaSrednja),
    velika: String(podesavanja.minArtikalaVelika),
  }));
  const [greske, setGreske] = useState<Record<string, string[]>>({});

  const sacuvaj = useMutation({
    mutationFn: () =>
      adminApi.sacuvajPodesavanja({
        ...podesavanja,
        koeficijentVrijednosti: Number(f.vrijednost) / 100,
        minimalnaMarza: Number(f.marza) / 100,
        brojZamjena: Number(f.zamjene),
        minArtikalaMala: Number(f.mala),
        minArtikalaSrednja: Number(f.srednja),
        minArtikalaVelika: Number(f.velika),
      }),
    onSuccess: (novo) => {
      qc.setQueryData(queryKeys.podesavanja(), novo);
      setGreske({});
      show("Pravila korpica sačuvana");
    },
    onError: (err) => {
      if (err instanceof ApiRequestError) setGreske(err.fieldErrors);
      show(poruka(err, "Čuvanje nije uspjelo"), "error");
    },
  });

  const g = (k: string) => greske[`podesavanja.${k}`]?.[0];
  const polje = (k: keyof typeof f, label: string, greska?: string, korak = "1") => (
    <Field label={label} error={greska}>
      <Input type="number" inputMode="decimal" step={korak} min="0" value={f[k]} onChange={(e) => setF({ ...f, [k]: e.target.value })} />
    </Field>
  );

  return (
    <section className="rounded-card bg-white p-5 shadow-card">
      <div className="mb-3 flex flex-wrap items-baseline justify-between gap-2">
        <h2 className="font-display text-lg font-semibold">Pravila za sve korpice</h2>
        <p className="text-xs text-muted">Ista su i u „Poslovna pravila“ — mijenjaju se na oba mjesta.</p>
      </div>
      <div className="grid gap-3 sm:grid-cols-3 lg:grid-cols-6">
        {polje("vrijednost", "Vrijednost sadržaja (% cijene)", g("koeficijentVrijednosti"))}
        {polje("marza", "Najmanja marža (%)", g("minimalnaMarza"))}
        {polje("zamjene", "Predloženih zamjena", g("brojZamjena"))}
        {polje("mala", "Najmanje kultura — mala", g("minArtikalaMala"))}
        {polje("srednja", "Najmanje kultura — srednja", g("minArtikalaSrednja"))}
        {polje("velika", "Najmanje kultura — velika", g("minArtikalaVelika"))}
      </div>
      <p className="mt-2 text-xs text-muted">
        Npr. 115 % znači da korpica od 35 KM nosi robe za 40,25 KM. Generator tu vrijednost dijeli na najmanji broj kultura,
        a ne smije preći nabavni trošak koji bi oborio maržu ispod najmanje.
      </p>
      <div className="mt-3 flex justify-end">
        <Button size="sm" onClick={() => sacuvaj.mutate()} disabled={sacuvaj.isPending}>
          {sacuvaj.isPending ? "Čuvam…" : "Sačuvaj pravila"}
        </Button>
      </div>
    </section>
  );
}

/* ---------- jedna vrsta korpice ---------- */

type Vrsta = PostavkeKorpica["tipovi"][number];

function Vrsta({
  tip,
  vrsta,
  kategorije,
  korpiceIds,
  proizvodi,
  podesavanja,
}: {
  tip: TipKorpice;
  vrsta: Vrsta;
  kategorije: PostavkeKorpica["kategorije"];
  korpiceIds: Set<string>;
  proizvodi: Product[];
  podesavanja: Podesavanja;
}) {
  const qc = useQueryClient();
  const { show } = useToast();
  const [izabrane, setIzabrane] = useState(() => new Set(vrsta.kategorijeIds));
  const [korpice, setKorpice] = useState(() =>
    vrsta.korpice.map((k) => ({ ...k, cijenaTekst: String(k.cijena), ambalazaTekst: k.ambalaza != null ? String(k.ambalaza) : "" })),
  );

  const izmijenjeno =
    izabrane.size !== vrsta.kategorijeIds.length ||
    vrsta.kategorijeIds.some((id) => !izabrane.has(id)) ||
    korpice.some(
      (k, i) =>
        Number(k.cijenaTekst) !== vrsta.korpice[i].cijena ||
        k.uProdaji !== vrsta.korpice[i].uProdaji ||
        (k.ambalazaTekst.trim() ? Number(k.ambalazaTekst) : null) !== (vrsta.korpice[i].ambalaza ?? null),
    );

  const sacuvaj = useMutation({
    mutationFn: () =>
      adminKorpiceApi.sacuvajVrstu(tip, {
        kategorijeIds: [...izabrane],
        // Prazna kutija = 0 = ukloni (backend: null ne mijenja, 0 briše).
        korpice: korpice.map((k) => ({
          id: k.id,
          cijena: Number(k.cijenaTekst),
          uProdaji: k.uProdaji,
          ambalaza: k.ambalazaTekst.trim() ? Number(k.ambalazaTekst) : 0,
        })),
      }),
    onSuccess: (novo) => {
      qc.setQueryData(POSTAVKE_KLJUC, novo);
      qc.invalidateQueries({ queryKey: ["admin", "products"] });
      qc.invalidateQueries({ queryKey: ["admin", "korpice"] });
      qc.invalidateQueries({ queryKey: ["korpice"] });
      show(`${NAZIVI_TIPA[tip].label}: sačuvano`);
    },
    onError: (err) => show(poruka(err, "Čuvanje nije uspjelo"), "error"),
  });

  const min: Record<Velicina, number> = {
    mala: podesavanja.minArtikalaMala,
    srednja: podesavanja.minArtikalaSrednja,
    velika: podesavanja.minArtikalaVelika,
  };
  // Vrijednost jedne kulture po veličini — iz cijene koja je trenutno u polju.
  const poKulturi = Object.fromEntries(
    korpice.map((k) => [k.velicina, ciljPoKulturi(Number(k.cijenaTekst) || 0, podesavanja.koeficijentVrijednosti, min[k.velicina as Velicina])]),
  ) as Partial<Record<Velicina, number>>;

  return (
    <div className="space-y-6">
      <section className="rounded-card bg-white p-5 shadow-card">
        <h2 className="mb-3 font-display text-lg font-semibold">Veličine i cijene</h2>
        {korpice.length === 0 ? (
          <p className="text-sm text-muted">Ova vrsta još nema korpica. Dodaj ih kao proizvod vrste „korpica“.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[46rem] text-sm">
              <thead className="text-left text-xs text-muted">
                <tr className="border-b border-line">
                  <th className="py-2 pr-3 font-medium">Korpica</th>
                  <th className="py-2 pr-3 font-medium">Cijena (KM)</th>
                  <th className="py-2 pr-3 font-medium">Kutija (KM)</th>
                  <th className="py-2 pr-3 font-medium">U ponudi</th>
                  <th className="py-2 pr-3 text-right font-medium">Sadržaj vrijedi</th>
                  <th className="py-2 pr-3 text-right font-medium">Kultura</th>
                  <th className="py-2 pr-3 text-right font-medium">Po kulturi</th>
                  <th className="py-2 text-right font-medium">Nabavno najviše</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-line tabular-nums">
                {korpice.map((k, i) => {
                  const cijena = Number(k.cijenaTekst) || 0;
                  const v = k.velicina as Velicina;
                  return (
                    <tr key={k.id}>
                      <td className="py-2 pr-3">
                        <span className="flex items-center gap-2">
                          <span className="relative h-9 w-9 shrink-0 overflow-hidden rounded-lg bg-sand">
                            <SmartImage src={k.imageUrl ?? undefined} alt={k.naziv} sizes="36px" />
                          </span>
                          <span className="font-medium">{k.naziv}</span>
                        </span>
                      </td>
                      <td className="py-2 pr-3">
                        <Input
                          type="number"
                          inputMode="decimal"
                          step="0.5"
                          min="0"
                          aria-label={`Cijena — ${k.naziv}`}
                          value={k.cijenaTekst}
                          onChange={(e) => setKorpice(korpice.map((x, j) => (j === i ? { ...x, cijenaTekst: e.target.value } : x)))}
                          className="w-24"
                        />
                      </td>
                      <td className="py-2 pr-3">
                        <Input
                          type="number"
                          inputMode="decimal"
                          step="0.05"
                          min="0"
                          aria-label={`Cijena kutije — ${k.naziv}`}
                          placeholder="—"
                          value={k.ambalazaTekst}
                          onChange={(e) => setKorpice(korpice.map((x, j) => (j === i ? { ...x, ambalazaTekst: e.target.value } : x)))}
                          className="w-20"
                        />
                      </td>
                      <td className="py-2 pr-3">
                        <label className="inline-flex items-center gap-2">
                          <input
                            type="checkbox"
                            checked={k.uProdaji}
                            onChange={(e) => setKorpice(korpice.map((x, j) => (j === i ? { ...x, uProdaji: e.target.checked } : x)))}
                            className="h-4 w-4 accent-brand-600"
                          />
                          <span className={k.uProdaji ? "text-ink" : "text-muted"}>{k.uProdaji ? "da" : "ne"}</span>
                        </label>
                      </td>
                      <td className="py-2 pr-3 text-right">{formatPrice(ciljSadrzaja(cijena, podesavanja.koeficijentVrijednosti))}</td>
                      <td className="py-2 pr-3 text-right">najmanje {min[v]}</td>
                      <td className="py-2 pr-3 text-right">≈ {formatPrice(poKulturi[v] ?? 0)}</td>
                      <td className="py-2 text-right">{formatPrice(nabavniBudzet(cijena, podesavanja.minimalnaMarza))}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}

        <h3 className="mb-1.5 mt-5 text-sm font-semibold">Kategorije koje ulaze u ovu korpicu</h3>
        <p className="mb-2 text-xs text-muted">Generator puni korpicu samo artiklima iz ovih kategorija; zamjene smiju i iz ostale sezonske ponude.</p>
        <div className="flex flex-wrap gap-1.5">
          {kategorije.map((c) => {
            const da = izabrane.has(c.id);
            return (
              <button
                key={c.id}
                type="button"
                aria-pressed={da}
                onClick={() => {
                  const n = new Set(izabrane);
                  if (da) n.delete(c.id);
                  else n.add(c.id);
                  setIzabrane(n);
                }}
                className={cn(
                  "rounded-full border px-3 py-1 text-sm transition",
                  da ? "border-brand-600 bg-brand-600 text-white" : "border-line bg-white text-ink hover:border-brand-400",
                )}
              >
                {c.naziv}
              </button>
            );
          })}
        </div>

        <div className="mt-4 flex items-center justify-end gap-3">
          {izmijenjeno && <span className="text-xs text-honey-700">Ima nesačuvanih izmjena</span>}
          <Button size="sm" onClick={() => sacuvaj.mutate()} disabled={!izmijenjeno || sacuvaj.isPending}>
            {sacuvaj.isPending ? "Čuvam…" : "Sačuvaj veličine i kategorije"}
          </Button>
        </div>
      </section>

      <Artikli
        proizvodi={proizvodi.filter((p) => izabrane.has(p.categoryId) && !korpiceIds.has(p.id))}
        velicine={VELICINE.filter((v) => korpice.some((k) => k.velicina === v))}
        poKulturi={poKulturi}
      />
    </div>
  );
}

/* ---------- artikli ---------- */

function Artikli({
  proizvodi,
  velicine,
  poKulturi,
}: {
  proizvodi: Product[];
  velicine: Velicina[];
  poKulturi: Partial<Record<Velicina, number>>;
}) {
  const qc = useQueryClient();
  const { show } = useToast();
  const [izmjene, setIzmjene] = useState<Record<string, ArtikalKorpiceUnos>>({});
  const [upit, setUpit] = useState("");
  const [samoUKorpici, setSamoUKorpici] = useState(false);
  const mjesec = new Date().getMonth() + 1;

  const red = (p: Product): ArtikalKorpiceUnos =>
    izmjene[p.id] ?? {
      id: p.id,
      mozeUKorpicu: p.mozeUKorpicu ?? false,
      prioritetUKorpici: p.prioritetUKorpici ?? 0,
      kolicinaMala: p.kolicinaMala ?? null,
      kolicinaSrednja: p.kolicinaSrednja ?? null,
      kolicinaVelika: p.kolicinaVelika ?? null,
      cijenaPiljara: p.cijenaPiljara ?? null,
    };
  const promijeni = (p: Product, izmjena: Partial<ArtikalKorpiceUnos>) =>
    setIzmjene((s) => ({ ...s, [p.id]: { ...red(p), ...izmjena } }));

  const prikaz = useMemo(() => {
    const q = upit.trim().toLowerCase();
    return proizvodi
      .filter((p) => !q || `${p.name} ${p.categoryName}`.toLowerCase().includes(q))
      .filter((p) => !samoUKorpici || (izmjene[p.id]?.mozeUKorpicu ?? p.mozeUKorpicu))
      .sort((a, b) => Number(b.mozeUKorpicu ?? false) - Number(a.mozeUKorpicu ?? false) || a.name.localeCompare(b.name, "sr-Latn"));
  }, [proizvodi, upit, samoUKorpici, izmjene]);

  const brojIzmjena = Object.keys(izmjene).length;

  const sacuvaj = useMutation({
    mutationFn: () => adminKorpiceApi.sacuvajArtikle(Object.values(izmjene)),
    onSuccess: (n) => {
      setIzmjene({});
      qc.invalidateQueries({ queryKey: ["admin", "products"] });
      qc.invalidateQueries({ queryKey: ["admin", "korpice", "kalendar"] });
      qc.invalidateQueries({ queryKey: ["korpice", "sezona"] });
      show(`Sačuvano artikala: ${n}`);
    },
    onError: (err) => show(poruka(err, "Čuvanje nije uspjelo"), "error"),
  });

  function predloziPrazne() {
    const nove = { ...izmjene };
    for (const p of proizvodi) {
      const r = nove[p.id] ?? red(p);
      if (!r.mozeUKorpicu) continue;
      let promjena = false;
      const novi = { ...r };
      for (const v of velicine) {
        const polje = POLJE_KOLICINE[v];
        if (novi[polje] == null && poKulturi[v]) {
          novi[polje] = predlozenaKolicina(poKulturi[v]!, p.price, p.unit);
          promjena = true;
        }
      }
      if (promjena) nove[p.id] = novi;
    }
    setIzmjene(nove);
  }

  return (
    <section className="rounded-card bg-white p-5 shadow-card">
      <div className="mb-3 flex flex-wrap items-end justify-between gap-3">
        <div>
          <h2 className="font-display text-lg font-semibold">Artikli i količine</h2>
          <p className="max-w-2xl text-xs text-muted">
            Artikli iz izabranih kategorija. Količina je ono što ide u kutiju te veličine (u jedinici artikla); prazno =
            računa se iz vrijednosti. Boja upozorava kad je vrijednost daleko od jedne kulture: <span className="text-honey-700">malo</span>{" "}
            ili <span className="text-red-700">previše</span>. Sezonu mijenjaš u sezonskom kalendaru.
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <label className="relative">
            <Search className="pointer-events-none absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-muted" />
            <input
              type="search"
              value={upit}
              onChange={(e) => setUpit(e.target.value)}
              placeholder="Traži artikal…"
              aria-label="Traži artikal"
              className="h-9 w-48 rounded-full border border-line bg-white pl-8 pr-3 text-sm outline-none focus:border-brand-500"
            />
          </label>
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" checked={samoUKorpici} onChange={(e) => setSamoUKorpici(e.target.checked)} className="h-4 w-4 accent-brand-600" />
            Samo u korpici
          </label>
          <Button variant="secondary" size="sm" onClick={predloziPrazne} title="Popuni prazne količine artikala koji idu u korpicu">
            <Sparkles className="h-4 w-4" /> Predloži prazne
          </Button>
        </div>
      </div>

      {proizvodi.length === 0 ? (
        <p className="text-sm text-muted">Nijedna kategorija nije izabrana, ili u njima nema artikala.</p>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full min-w-[46rem] text-sm">
            <thead className="text-left text-xs text-muted">
              <tr className="border-b border-line">
                <th className="py-2 pr-2 font-medium">U korpici</th>
                <th className="py-2 pr-3 font-medium">Artikal</th>
                <th className="py-2 pr-3 font-medium">Sezona</th>
                <th className="py-2 pr-3 font-medium" title="0 = osnovni artikal, veći broj = dopuna">Prioritet</th>
                <th className="py-2 pr-3 font-medium" title="Koliko isti artikal košta u piljari, po jedinici — za obračun cijene korpice">
                  Piljara
                </th>
                {velicine.map((v) => (
                  <th key={v} className="py-2 pr-3 font-medium">
                    {NAZIV_VELICINE[v]}
                    {poKulturi[v] ? <span className="block font-normal">≈ {formatPrice(poKulturi[v]!)} po kulturi</span> : null}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-line">
              {prikaz.map((p) => {
                const r = red(p);
                const uSezoni = !p.mjeseci?.length || p.mjeseci.includes(mjesec);
                return (
                  <tr key={p.id} className={cn(izmjene[p.id] && "bg-brand-50/50", !r.mozeUKorpicu && "text-muted")}>
                    <td className="py-2 pr-2">
                      <input
                        type="checkbox"
                        aria-label={`${p.name} ide u korpicu`}
                        checked={r.mozeUKorpicu}
                        onChange={(e) => promijeni(p, { mozeUKorpicu: e.target.checked })}
                        className="h-4 w-4 accent-brand-600"
                      />
                    </td>
                    <td className="py-2 pr-3">
                      <span className="flex items-center gap-2">
                        <span className="relative h-9 w-9 shrink-0 overflow-hidden rounded-lg" style={{ backgroundColor: p.boja ?? "var(--color-sand)" }}>
                          <SmartImage src={p.izrezUrl ?? p.imageUrl} alt={p.name} sizes="36px" />
                        </span>
                        <span className="min-w-0">
                          <span className="block font-medium text-ink">
                            {p.name}
                            {!p.isAvailable && <span className="ml-1.5 text-xs font-normal text-muted">· nije u prodaji</span>}
                          </span>
                          <span className="block text-xs text-muted">
                            {p.categoryName} · {formatPrice(p.price)}
                            {unitSuffix(p.unit)}
                            {p.marza != null && ` · marža ${formatMarza(p.marza)}`}
                          </span>
                        </span>
                      </span>
                    </td>
                    <td className="py-2 pr-3">
                      <span className="flex gap-px" title={p.mjeseci?.length ? "Mjeseci u sezoni" : "Cijele godine"}>
                        {MJESECI.map((m, i) => {
                          const da = !p.mjeseci?.length || p.mjeseci.includes(i + 1);
                          return (
                            <span
                              key={i}
                              className={cn(
                                "grid h-4 w-3.5 place-items-center rounded-sm text-[9px]",
                                da ? "bg-brand-100 text-brand-800" : "bg-sand text-muted/60",
                                i + 1 === mjesec && "ring-1 ring-ink",
                              )}
                            >
                              {m}
                            </span>
                          );
                        })}
                      </span>
                      {!uSezoni && r.mozeUKorpicu && <span className="text-[11px] text-muted">van sezone sada</span>}
                    </td>
                    <td className="py-2 pr-3">
                      <Input
                        type="number"
                        min="0"
                        step="1"
                        aria-label={`Prioritet — ${p.name}`}
                        value={r.prioritetUKorpici}
                        onChange={(e) => promijeni(p, { prioritetUKorpici: Math.max(0, Math.round(Number(e.target.value) || 0)) })}
                        className="w-16"
                      />
                    </td>
                    <td className="py-2 pr-3">
                      <span className="flex items-center gap-1">
                        <Input
                          type="number"
                          min="0"
                          step="0.1"
                          inputMode="decimal"
                          placeholder="—"
                          aria-label={`Cijena u piljari — ${p.name}`}
                          value={r.cijenaPiljara ?? ""}
                          onChange={(e) => {
                            const t = e.target.value.replace(",", ".");
                            promijeni(p, { cijenaPiljara: t.trim() === "" || Number(t) <= 0 ? null : Number(t) });
                          }}
                          className="w-20"
                        />
                        <span className="text-xs text-muted">KM</span>
                      </span>
                      {r.cijenaPiljara != null && r.cijenaPiljara > 0 && (
                        <span className={cn("block text-[11px] tabular-nums", p.price > r.cijenaPiljara ? "text-red-700" : "text-muted")}>
                          mi {p.price <= r.cijenaPiljara ? "−" : "+"}
                          {Math.abs(Math.round((1 - p.price / r.cijenaPiljara) * 100))} %
                        </span>
                      )}
                    </td>
                    {velicine.map((v) => {
                      const polje = POLJE_KOLICINE[v];
                      const k = r[polje];
                      const ocjena = k != null && poKulturi[v] ? ocjenaKolicine(k, p.price, poKulturi[v]!) : "ok";
                      return (
                        <td key={v} className="py-2 pr-3">
                          <span className="flex items-center gap-1">
                            <Input
                              type="number"
                              min="0"
                              step={p.unit === "kom" ? "1" : "0.1"}
                              inputMode="decimal"
                              placeholder="—"
                              aria-label={`${p.name} — ${NAZIV_VELICINE[v]} korpica`}
                              value={k ?? ""}
                              onChange={(e) => {
                                const t = e.target.value.replace(",", ".");
                                promijeni(p, { [polje]: t.trim() === "" ? null : Number(t) });
                              }}
                              className="w-20"
                            />
                            <span className="text-xs text-muted">{p.unit}</span>
                          </span>
                          {k != null && (
                            <span
                              className={cn(
                                "block text-[11px] tabular-nums",
                                ocjena === "malo" ? "text-honey-700" : ocjena === "puno" ? "text-red-700" : "text-muted",
                              )}
                            >
                              = {formatPrice(k * p.price)}
                              {ocjena === "malo" ? " · malo" : ocjena === "puno" ? " · previše" : ""}
                            </span>
                          )}
                        </td>
                      );
                    })}
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      <div className="sticky bottom-0 mt-4 flex items-center justify-end gap-3 border-t border-line bg-white/95 py-3 backdrop-blur">
        {brojIzmjena > 0 && (
          <>
            <span className="text-sm text-honey-700">Izmijenjeno artikala: {brojIzmjena}</span>
            <Button variant="secondary" size="sm" onClick={() => setIzmjene({})}>
              Poništi
            </Button>
          </>
        )}
        <Button size="sm" onClick={() => sacuvaj.mutate()} disabled={brojIzmjena === 0 || sacuvaj.isPending}>
          {sacuvaj.isPending ? "Čuvam…" : "Sačuvaj artikle"}
        </Button>
      </div>
    </section>
  );
}
