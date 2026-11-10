"use client";

import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Eye, RotateCcw, Send, Users } from "lucide-react";
import { AdminLadica } from "@/components/admin/admin-ladica";
import { KarticaPoslovanja } from "@/components/admin/poslovanje/kartica-poslovanja";
import { Odjeljak } from "@/components/admin/poslovanje/odjeljak";
import { Tabela } from "@/components/admin/poslovanje/tabela";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { Field, Input, Select, Textarea } from "@/components/ui/field";
import { LoadingBlock } from "@/components/ui/spinner";
import { useToast } from "@/components/ui/toast";
import { ApiRequestError } from "@/lib/api/client";
import { adminApi } from "@/lib/api/endpoints";
import { useSedmica } from "@/lib/api/hooks";
import type {
  EmailRed,
  GrupaKampanje,
  Kampanja,
  KampanjaPregled,
  ObavjestenjaPodesavanja,
  StatusEmaila,
} from "@/lib/api/contracts";
import { cn } from "@/lib/utils";

const vrijeme = (s?: string | null) =>
  s ? new Date(s).toLocaleString("sr-Latn", { day: "numeric", month: "numeric", hour: "2-digit", minute: "2-digit" }) : "—";
const greskaPoruka = (e: unknown) => (e instanceof ApiRequestError ? e.message : "Akcija nije uspjela. Pokušaj ponovo.");

const STATUS: Record<StatusEmaila, { tekst: string; klasa: string }> = {
  Poslato: { tekst: "Poslato", klasa: "bg-brand-100 text-brand-800" },
  NaCekanju: { tekst: "Na čekanju", klasa: "bg-honey-100 text-honey-700" },
  Greska: { tekst: "Greška", klasa: "bg-red-50 text-red-700" },
};

/** Čitljiv naziv vrste mejla; nepoznata vrsta ostaje kakva jeste. */
const VRSTE: Record<string, string> = {
  potvrda_narudzbe: "Potvrda narudžbe",
  potvrda_adrese: "Potvrda adrese",
  zahtjev_gosta: "Link gostu",
  preskocena_isporuka: "Preskočena isporuka",
  status_confirmed: "Narudžba potvrđena",
  status_delivered: "Narudžba isporučena",
  status_faileddelivery: "Nije uručena",
  status_cancelled: "Otkazana",
  reset_lozinke: "Nova lozinka",
  nova_narudzba: "Radnici: nova narudžba",
  odbijene_narudzbe: "Radnici: odbijene",
  fiskalna_greska: "Radnici: fiskalni račun",
  odstupanje_goriva: "Radnici: gorivo",
  alarm_sadrzaja: "Radnici: sadržaj korpica",
  plan_vozaca: "Vozač: plan za sutra",
  obavjestenje: "Ručno obavještenje",
  probni: "Probni",
};
const vrstaTekst = (v: string) => VRSTE[v] ?? v;

const GRUPE: { id: GrupaKampanje; tekst: string; opis: string }[] = [
  { id: "sedmica", tekst: "Kupci sa narudžbom za sedmicu", opis: "Servisno — npr. kašnjenje ili promjena termina dostave." },
  { id: "pretplatnici", tekst: "Aktivni pretplatnici", opis: "Servisno — promjene u pretplati ili korpici." },
  { id: "kupci", tekst: "Svi kupci sa nalogom", opis: "Informativno — uz link za odjavu; odjavljeni se preskaču." },
  { id: "novosti", tekst: "Prijavljeni na novosti", opis: "Informativno — uz link za odjavu." },
  { id: "vozaci", tekst: "Vozači", opis: "Aktivni vozači sa nalogom." },
  { id: "radnici", tekst: "Radnici", opis: "Primaoci obavještenja iz podešavanja ispod." },
];

/**
 * Mejlovi: svaki mejl prvo ide u red, pa ga pozadinski posao šalje (odmah, a pri grešci ponovo za 1–60 min).
 * Ovdje se vidi šta je otišlo, šta čeka i zašto nije prošlo; šalje se obavještenje grupi i bira koja
 * automatska obavještenja idu kupcima, radnicima i vozačima.
 */
export default function MejloviPage() {
  const [status, setStatus] = useState("");
  const [vrsta, setVrsta] = useState("");
  const [trazi, setTrazi] = useState("");
  const [otvoren, setOtvoren] = useState<string | null>(null);
  const q = useQuery({
    queryKey: ["admin", "mejlovi", status, vrsta, trazi],
    queryFn: () => adminApi.mejlovi({ status: status || undefined, vrsta: vrsta || undefined, trazi: trazi || undefined }),
    refetchInterval: 15_000,
  });
  const d = q.data;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="font-display text-2xl font-bold">Mejlovi</h1>
        <p className="text-muted">Šta je poslato kupcima, radnicima i vozačima, ručna obavještenja i podešavanja.</p>
      </div>

      {d?.transport === "smtp" && (
        <p className="rounded-card bg-honey-100 px-4 py-3 text-sm font-medium text-honey-700">
          Mejlovi idu preko SMTP-a (lokalno Mailpit). U produkciji postavi Brevo — koraci su u DEPLOY.md, „Mejlovi u
          produkciji“.
        </p>
      )}

      {q.isLoading ? (
        <LoadingBlock />
      ) : q.isError || !d ? (
        <EmptyState
          title="Ne mogu da učitam mejlove"
          description={greskaPoruka(q.error)}
          action={<Button onClick={() => q.refetch()}>Pokušaj ponovo</Button>}
        />
      ) : (
        <>
          <div className="grid grid-cols-2 gap-3 sm:gap-4 lg:grid-cols-4">
            <KarticaPoslovanja label="Poslato danas" vrijednost={String(d.poslatoDanas)} hint={`dobavljač: ${d.transport}`} />
            <KarticaPoslovanja label="Poslato ovog mjeseca" vrijednost={String(d.poslatoMjesec)} />
            <KarticaPoslovanja label="Na čekanju" vrijednost={String(d.naCekanju)} hint="šalju se odmah, pri grešci ponovo" />
            <KarticaPoslovanja label="Greške" vrijednost={String(d.greske)} hint={d.greske ? "otvori mejl za razlog" : "nema"} />
          </div>

          <Kampanje />

          <Odjeljak naslov="Poslati mejlovi" opis="Posljednjih 200, najnoviji prvi. Klik na red otvara mejl kako ga je primalac vidio.">
            <div className="flex flex-wrap items-end gap-2">
              <Field label="Status">
                <Select value={status} onChange={(e) => setStatus(e.target.value)}>
                  <option value="">Svi</option>
                  <option value="Poslato">Poslato</option>
                  <option value="NaCekanju">Na čekanju</option>
                  <option value="Greska">Greška</option>
                </Select>
              </Field>
              <Field label="Vrsta">
                <Select value={vrsta} onChange={(e) => setVrsta(e.target.value)}>
                  <option value="">Sve</option>
                  {d.vrste.map((v) => (
                    <option key={v} value={v}>
                      {vrstaTekst(v)}
                    </option>
                  ))}
                </Select>
              </Field>
              <Field label="Traži">
                <Input value={trazi} onChange={(e) => setTrazi(e.target.value)} placeholder="adresa ili naslov" className="w-full sm:w-56" />
              </Field>
            </div>
            <Tabela<EmailRed>
              kljuc={(r) => r.id}
              redovi={d.poruke}
              prazno="Nema mejlova za ovaj izbor."
              kolone={[
                { naslov: "Nastao", vrijednost: (r) => vrijeme(r.kreirana) },
                { naslov: "Primalac", vrijednost: (r) => r.primalacIme ? `${r.primalacIme} · ${r.primalac}` : r.primalac },
                { naslov: "Naslov", vrijednost: (r) => <span className="line-clamp-1">{r.naslov}</span> },
                { naslov: "Vrsta", vrijednost: (r) => vrstaTekst(r.vrsta) },
                {
                  naslov: "Status",
                  vrijednost: (r) => (
                    <span
                      title={r.zadnjaGreska ?? undefined}
                      className={cn("whitespace-nowrap rounded-full px-2 py-0.5 text-xs font-semibold", STATUS[r.status].klasa)}
                    >
                      {STATUS[r.status].tekst}
                      {r.status !== "Poslato" && r.pokusaja > 0 && ` · ${r.pokusaja}×`}
                    </span>
                  ),
                },
                {
                  naslov: "",
                  vrijednost: (r) => (
                    <button
                      type="button"
                      onClick={() => setOtvoren(r.id)}
                      aria-label={`Otvori mejl ${r.naslov}`}
                      className="rounded-full p-1.5 text-muted hover:bg-sand hover:text-ink"
                    >
                      <Eye className="h-3.5 w-3.5" />
                    </button>
                  ),
                  desno: true,
                },
              ]}
            />
          </Odjeljak>

          <Podesavanja />
        </>
      )}

      {otvoren && <MejlLadica id={otvoren} onClose={() => setOtvoren(null)} />}
    </div>
  );
}

function MejlLadica({ id, onClose }: { id: string; onClose: () => void }) {
  const qc = useQueryClient();
  const { show } = useToast();
  const q = useQuery({ queryKey: ["admin", "mejl", id], queryFn: () => adminApi.mejl(id) });
  const ponovi = useMutation({
    mutationFn: () => adminApi.ponoviMejl(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin", "mejlovi"] });
      qc.invalidateQueries({ queryKey: ["admin", "mejl", id] });
      show("Mejl je vraćen u red — šalje se odmah");
    },
    onError: (e) => show(greskaPoruka(e), "error"),
  });
  const m = q.data;

  return (
    <AdminLadica naslov={m?.naslov ?? "Mejl"} podnaslov={m ? `${m.primalac} · ${vrstaTekst(m.vrsta)}` : undefined} onClose={onClose} sirina="max-w-2xl">
      {q.isLoading || !m ? (
        <LoadingBlock />
      ) : (
        <div className="space-y-4">
          <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-sm">
            <dt className="text-muted">Status</dt>
            <dd>
              <span className={cn("rounded-full px-2 py-0.5 text-xs font-semibold", STATUS[m.status].klasa)}>{STATUS[m.status].tekst}</span>
              {m.pokusaja > 0 && <span className="ml-2 text-muted">{m.pokusaja} pokušaja</span>}
            </dd>
            <dt className="text-muted">Nastao</dt>
            <dd>{vrijeme(m.kreirana)}</dd>
            <dt className="text-muted">Poslat</dt>
            <dd>{vrijeme(m.poslata)}{m.transport && <span className="text-muted"> · {m.transport}</span>}</dd>
            {m.sljedeciPokusaj && m.status === "NaCekanju" && (
              <>
                <dt className="text-muted">Sljedeći pokušaj</dt>
                <dd>{vrijeme(m.sljedeciPokusaj)}</dd>
              </>
            )}
            {m.dobavljacId && (
              <>
                <dt className="text-muted">Id kod dobavljača</dt>
                <dd className="break-all font-mono text-xs">{m.dobavljacId}</dd>
              </>
            )}
          </dl>
          {m.zadnjaGreska && (
            <p className="rounded-card bg-red-50 px-3 py-2 text-sm text-red-700">
              <b>Zadnja greška:</b> {m.zadnjaGreska}
            </p>
          )}
          {m.status !== "Poslato" && (
            <Button size="sm" onClick={() => ponovi.mutate()} disabled={ponovi.isPending}>
              <RotateCcw className="h-4 w-4" /> Pošalji ponovo
            </Button>
          )}
          {/* sandbox bez dozvola: HTML mejla se prikazuje, ali ne može pokrenuti skriptu ni otvoriti link u adminu. */}
          <iframe title="Pregled mejla" sandbox="" srcDoc={m.html} className="h-[32rem] w-full rounded-card border border-line bg-white" />
        </div>
      )}
    </AdminLadica>
  );
}

function Kampanje() {
  const qc = useQueryClient();
  const { show } = useToast();
  const sedmicaQ = useSedmica();
  const [grupa, setGrupa] = useState<GrupaKampanje>("sedmica");
  const [godina, setGodina] = useState("");
  const [sedmica, setSedmica] = useState("");
  const [naslov, setNaslov] = useState("");
  const [tekst, setTekst] = useState("");
  const [pregled, setPregled] = useState<KampanjaPregled | null>(null);
  const [potvrdi, setPotvrdi] = useState(false);
  const lista = useQuery({ queryKey: ["admin", "kampanje"], queryFn: adminApi.kampanje, refetchInterval: 15_000 });

  useEffect(() => {
    if (sedmicaQ.data && !godina) {
      setGodina(String(sedmicaQ.data.godina));
      setSedmica(String(sedmicaQ.data.sedmica));
    }
  }, [sedmicaQ.data, godina]);

  // Svaka promjena grupe ili sedmice poništava pregled — broj primalaca mora odgovarati onome što se šalje.
  useEffect(() => {
    setPregled(null);
    setPotvrdi(false);
  }, [grupa, godina, sedmica]);

  const filter = () => ({
    grupa,
    godina: grupa === "sedmica" ? Number(godina) || null : null,
    sedmica: grupa === "sedmica" ? Number(sedmica) || null : null,
  });
  const pregledaj = useMutation({
    mutationFn: () => adminApi.pregledKampanje(filter()),
    onSuccess: setPregled,
    onError: (e) => show(greskaPoruka(e), "error"),
  });
  const posalji = useMutation({
    mutationFn: () => adminApi.posaljiKampanju({ ...filter(), naslov: naslov.trim(), tekst: tekst.trim() }),
    onSuccess: (k) => {
      qc.invalidateQueries({ queryKey: ["admin", "kampanje"] });
      qc.invalidateQueries({ queryKey: ["admin", "mejlovi"] });
      show(`Obavještenje je u redu za slanje: ${k.brojPrimalaca} primalaca`);
      setNaslov("");
      setTekst("");
      setPregled(null);
      setPotvrdi(false);
    },
    onError: (e) => show(greskaPoruka(e), "error"),
  });

  function klikPosalji() {
    if (!naslov.trim() || !tekst.trim()) return show("Upiši naslov i tekst.", "error");
    if (!pregled) return pregledaj.mutate();
    if (!potvrdi) return setPotvrdi(true);
    posalji.mutate();
  }

  const opisGrupe = GRUPE.find((g) => g.id === grupa)?.opis;
  return (
    <Odjeljak
      naslov="Pošalji obavještenje"
      opis="Kratka poruka grupi: npr. kašnjenje dostave, promjena termina, nova korpica. Mejl dobija izgled E piljare; prazan red je novi pasus."
    >
      <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_320px]">
        <div className="space-y-3">
          <div className="flex flex-wrap items-end gap-2">
            <Field label="Kome">
              <Select value={grupa} onChange={(e) => setGrupa(e.target.value as GrupaKampanje)}>
                {GRUPE.map((g) => (
                  <option key={g.id} value={g.id}>
                    {g.tekst}
                  </option>
                ))}
              </Select>
            </Field>
            {grupa === "sedmica" && (
              <>
                <Field label="Godina">
                  <Input inputMode="numeric" value={godina} onChange={(e) => setGodina(e.target.value)} className="w-24" />
                </Field>
                <Field label="Sedmica">
                  <Input inputMode="numeric" value={sedmica} onChange={(e) => setSedmica(e.target.value)} className="w-20" />
                </Field>
              </>
            )}
          </div>
          {opisGrupe && <p className="text-xs text-muted">{opisGrupe}</p>}
          <Field label="Naslov">
            <Input value={naslov} maxLength={200} onChange={(e) => setNaslov(e.target.value)} placeholder="npr. Dostava u četvrtak kasni sat vremena" />
          </Field>
          <Field label="Tekst">
            <Textarea
              value={tekst}
              maxLength={10000}
              onChange={(e) => setTekst(e.target.value)}
              rows={6}
              placeholder={"Zdravo,\n\nzbog radova na putu dostava u četvrtak kreće sat kasnije, od 17:00.\n\nHvala na strpljenju, E piljara"}
            />
          </Field>
          <div className="flex flex-wrap items-center gap-2">
            <Button type="button" variant="secondary" size="sm" onClick={() => pregledaj.mutate()} disabled={pregledaj.isPending}>
              <Users className="h-4 w-4" /> Pregledaj primaoce
            </Button>
            <Button
              type="button"
              size="sm"
              variant={potvrdi ? "danger" : "primary"}
              onClick={klikPosalji}
              onBlur={() => setPotvrdi(false)}
              disabled={posalji.isPending || pregled?.broj === 0}
            >
              <Send className="h-4 w-4" />
              {potvrdi ? `Sigurno? Pošalji ${pregled?.broj ?? ""}` : pregled ? `Pošalji (${pregled.broj})` : "Pošalji"}
            </Button>
          </div>
        </div>
        <div className="rounded-card bg-sand/60 p-4 text-sm">
          {pregled ? (
            <>
              <p className="font-semibold text-ink">
                {pregled.opis}: {pregled.broj} {pregled.broj === 1 ? "primalac" : "primalaca"}
              </p>
              {pregled.saOdjavom && <p className="mt-1 text-xs text-muted">Svaki mejl ima link za odjavu.</p>}
              {pregled.broj > 300 && (
                <p className="mt-2 text-xs font-medium text-honey-700">
                  Brevo besplatno šalje 300 mejlova dnevno — ostatak ide sutra (red ponavlja sam).
                </p>
              )}
              <ul className="mt-2 space-y-0.5 text-xs text-muted">
                {pregled.primjeri.map((p) => (
                  <li key={p} className="truncate">
                    {p}
                  </li>
                ))}
                {pregled.broj > pregled.primjeri.length && <li>… i još {pregled.broj - pregled.primjeri.length}</li>}
              </ul>
            </>
          ) : (
            <p className="text-muted">Klikni „Pregledaj primaoce“ da vidiš koliko ljudi će dobiti mejl, prije slanja.</p>
          )}
        </div>
      </div>
      <Tabela<Kampanja>
        kljuc={(k) => k.id}
        redovi={lista.data ?? []}
        prazno="Još nije poslato nijedno obavještenje."
        kolone={[
          { naslov: "Poslato", vrijednost: (k) => vrijeme(k.kreirana) },
          { naslov: "Naslov", vrijednost: (k) => k.naslov },
          { naslov: "Grupa", vrijednost: (k) => k.grupaOpis },
          {
            naslov: "Primalaca",
            vrijednost: (k) => (
              <span>
                {k.poslato}/{k.brojPrimalaca}
                {k.greske > 0 && <span className="text-red-700"> · {k.greske} greš.</span>}
              </span>
            ),
            desno: true,
          },
        ]}
      />
    </Odjeljak>
  );
}

type Prekidac = { kljuc: keyof ObavjestenjaPodesavanja; tekst: string; opis: string };
const PREKIDACI: { grupa: string; stavke: Prekidac[] }[] = [
  {
    grupa: "Kupcu",
    stavke: [
      { kljuc: "kupacPotvrdjena", tekst: "Narudžba potvrđena", opis: "sa danom i terminom dostave" },
      { kljuc: "kupacIsporucena", tekst: "Narudžba isporučena", opis: "hvala + link za ocjenu" },
      { kljuc: "kupacNeuspjela", tekst: "Nije uručena", opis: "vozač kupca nije zatekao" },
      { kljuc: "kupacOtkazana", tekst: "Otkazana", opis: "admin otkazao ili nije potvrđena do roka" },
    ],
  },
  {
    grupa: "Radnicima",
    stavke: [
      { kljuc: "radnikNovaNarudzba", tekst: "Nova narudžba", opis: "svaka narudžba sa sajta" },
      { kljuc: "radnikOdbijene", tekst: "Automatski odbijene", opis: "sažetak nepotvrđenih poslije roka" },
      { kljuc: "radnikFiskalnaGreska", tekst: "Fiskalni račun nije izdat", opis: "dobavljač ga je odbio" },
      { kljuc: "radnikGorivo", tekst: "Gorivo odstupa", opis: "vozač platio ±20 % i više od procjene" },
    ],
  },
  {
    grupa: "Vozačima",
    stavke: [{ kljuc: "vozacPlan", tekst: "Plan za sutra", opis: "stanice, zone, km i gorivo" }],
  },
];

function Podesavanja() {
  const qc = useQueryClient();
  const { show } = useToast();
  const q = useQuery({ queryKey: ["admin", "obavjestenja"], queryFn: adminApi.obavjestenja });
  const [f, setF] = useState<ObavjestenjaPodesavanja | null>(null);
  const [probna, setProbna] = useState("");
  useEffect(() => {
    if (q.data) setF(q.data);
  }, [q.data]);

  const sacuvaj = useMutation({
    mutationFn: () => {
      const tijelo: Omit<ObavjestenjaPodesavanja, "radniciEfektivno"> & { radniciEfektivno?: string[] } = { ...f! };
      delete tijelo.radniciEfektivno;
      return adminApi.sacuvajObavjestenja(tijelo);
    },
    onSuccess: (d) => {
      qc.setQueryData(["admin", "obavjestenja"], d);
      show("Podešavanja obavještenja sačuvana");
    },
    onError: (e) => show(greskaPoruka(e), "error"),
  });
  const probni = useMutation({
    mutationFn: () => adminApi.probniMejl(probna.trim()),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin", "mejlovi"] });
      show("Probni mejl je u redu — status vidiš u tabeli iznad");
    },
    onError: (e) => show(greskaPoruka(e), "error"),
  });

  if (!f) return <LoadingBlock />;
  return (
    <Odjeljak naslov="Automatska obavještenja" opis="Šta ide samo od sebe. Mejlovi za potvrdu narudžbe, adrese, link gostu i novu lozinku idu uvijek.">
      <div className="grid gap-4 md:grid-cols-3">
        {PREKIDACI.map((g) => (
          <fieldset key={g.grupa} className="space-y-2">
            <legend className="mb-1 text-sm font-semibold text-ink">{g.grupa}</legend>
            {g.stavke.map((p) => (
              <label key={p.kljuc} className="flex cursor-pointer items-start gap-2 text-sm">
                <input
                  type="checkbox"
                  checked={Boolean(f[p.kljuc])}
                  onChange={(e) => setF({ ...f, [p.kljuc]: e.target.checked })}
                  className="mt-0.5 h-4 w-4 accent-brand-600"
                />
                <span>
                  <span className="text-ink">{p.tekst}</span>
                  <span className="block text-xs text-muted">{p.opis}</span>
                </span>
              </label>
            ))}
            {g.grupa === "Vozačima" && (
              <Field label="Plan stiže u (sat)">
                <Input
                  inputMode="numeric"
                  value={String(f.vozacPlanSat)}
                  onChange={(e) => setF({ ...f, vozacPlanSat: Number(e.target.value.replace(/\D/g, "")) || 0 })}
                  className="w-20"
                />
              </Field>
            )}
          </fieldset>
        ))}
      </div>
      <Field label="Primaoci obavještenja radnicima (adrese odvojene zarezom)">
        <Textarea
          value={f.radniciPrimaoci}
          onChange={(e) => setF({ ...f, radniciPrimaoci: e.target.value })}
          rows={2}
          placeholder="prazno = svi admini, npr. vlasnik@epiljara.ba, pakovanje@epiljara.ba"
        />
      </Field>
      <p className="text-xs text-muted">
        Trenutno idu na: {q.data?.radniciEfektivno.length ? q.data.radniciEfektivno.join(", ") : "nikoga (nema admina sa adresom)"}
      </p>
      <div className="flex flex-wrap items-end justify-between gap-3">
        <Button size="sm" onClick={() => sacuvaj.mutate()} disabled={sacuvaj.isPending}>
          Sačuvaj podešavanja
        </Button>
        <div className="flex flex-wrap items-end gap-2">
          <Field label="Probni mejl na">
            <Input value={probna} onChange={(e) => setProbna(e.target.value)} placeholder="prazno = moja adresa" className="w-full sm:w-60" />
          </Field>
          <Button size="sm" variant="secondary" onClick={() => probni.mutate()} disabled={probni.isPending} className="mb-1">
            <Send className="h-4 w-4" /> Pošalji probni mejl
          </Button>
        </div>
      </div>
    </Odjeljak>
  );
}
