"use client";

import { Suspense, useState } from "react";
import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ChartColumn, ChevronLeft, ChevronRight, Paperclip, Pencil, Plus, Trash2 } from "lucide-react";
import { AdminLadica } from "@/components/admin/admin-ladica";
import { KarticaPoslovanja } from "@/components/admin/poslovanje/kartica-poslovanja";
import { Odjeljak } from "@/components/admin/poslovanje/odjeljak";
import { Tabela } from "@/components/admin/poslovanje/tabela";
import { SlikaPolje } from "@/components/admin/slika-polje";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { Field, Input, Select } from "@/components/ui/field";
import { LoadingBlock } from "@/components/ui/spinner";
import { useToast } from "@/components/ui/toast";
import { ApiRequestError } from "@/lib/api/client";
import { adminApi } from "@/lib/api/endpoints";
import type { CijenaGoriva, PonavljajuciTrosak, Trosak, TroskoviMjeseca, VrstaTroska } from "@/lib/api/contracts";
import { MJESECI, susjedniMjesec } from "@/lib/admin/poslovanje";
import { cn, formatPrice } from "@/lib/utils";

/** "12,5" i "12.5" su oba broj; prazno ili pogrešno je null. */
function broj(v: string): number | null {
  const t = v.trim().replace(",", ".");
  if (!/^\d+(\.\d+)?$/.test(t)) return null;
  return Number(t);
}

const datum = (iso: string) => {
  const [g, m, d] = iso.split("-");
  return `${Number(d)}.${Number(m)}.${g}.`;
};
const mjesecTekst = (g: number, m: number) => `${MJESECI[m - 1]} ${g}.`;
const pad = (n: number) => String(n).padStart(2, "0");

function poruka(err: unknown) {
  return err instanceof ApiRequestError ? err.message : "Čuvanje nije uspjelo. Pokušaj ponovo.";
}

/**
 * Unos troškova poslovanja: jednokratni (računi) za mjesec, ponavljajući, cijene goriva i vrste.
 * Ambalaža i gorivo po rutama računaju se sami — ovdje se vide u sažetku.
 */
export default function TroskoviPage() {
  return (
    <Suspense fallback={<LoadingBlock />}>
      <Troskovi />
    </Suspense>
  );
}

function Troskovi() {
  const params = useSearchParams();
  const router = useRouter();
  const pathname = usePathname();
  const danas = new Date();
  const godina = Number(params.get("godina")) || danas.getFullYear();
  const mjesec = Number(params.get("mjesec")) || danas.getMonth() + 1;

  const q = useQuery({ queryKey: ["admin", "troskovi", godina, mjesec], queryFn: () => adminApi.troskovi(godina, mjesec) });
  const analitika = useQuery({
    queryKey: ["admin", "poslovanje", godina, mjesec],
    queryFn: () => adminApi.poslovanje(godina, mjesec),
  });
  const [ladica, setLadica] = useState<{ trosak: Trosak | null } | null>(null);

  function idi(pomak: number) {
    const s = susjedniMjesec(godina, mjesec, pomak);
    router.replace(`${pathname}?godina=${s.godina}&mjesec=${s.mjesec}`, { scroll: false });
  }

  const d = q.data;
  const t = analitika.data?.troskovi;

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="font-display text-2xl font-bold">Troškovi — {mjesecTekst(godina, mjesec)}</h1>
          <p className="text-muted">Računi i redovni troškovi; ambalaža i gorivo po rutama računaju se sami.</p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <Button variant="secondary" size="sm" onClick={() => idi(-1)} aria-label="Prethodni mjesec">
            <ChevronLeft className="h-4 w-4" />
          </Button>
          <Button variant="secondary" size="sm" onClick={() => idi(1)} aria-label="Sljedeći mjesec">
            <ChevronRight className="h-4 w-4" />
          </Button>
          <Link href={`/admin/poslovanje?godina=${godina}&mjesec=${mjesec}`}>
            <Button variant="secondary" size="sm">
              <ChartColumn className="h-4 w-4" /> Poslovanje mjeseca
            </Button>
          </Link>
          <Button size="sm" onClick={() => setLadica({ trosak: null })} disabled={!d || d.vrste.every((v) => !v.aktivna)}>
            <Plus className="h-4 w-4" /> Dodaj trošak
          </Button>
        </div>
      </div>

      {q.isLoading ? (
        <LoadingBlock />
      ) : q.isError || !d ? (
        <EmptyState
          title="Ne mogu da učitam troškove"
          description={poruka(q.error)}
          action={<Button onClick={() => q.refetch()}>Pokušaj ponovo</Button>}
        />
      ) : (
        <>
          {t && (
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
              <KarticaPoslovanja label="Troškovi ukupno" vrijednost={formatPrice(t.ukupno)} hint="sa ambalažom i gorivom" />
              <KarticaPoslovanja
                label="Ambalaža (uz prodaju)"
                vrijednost={formatPrice(t.ambalaza)}
                hint="iz cijene ambalaže na artiklima i korpicama"
              />
              <KarticaPoslovanja
                label={t.gorivo.izvor === "racuni" ? "Gorivo — po računima" : "Gorivo — po rutama"}
                vrijednost={formatPrice(t.gorivo.uDobiti)}
                hint={`${t.gorivo.km.toLocaleString("sr-Latn")} km u ${t.gorivo.brojRuta} ruta · procjena ${
                  t.gorivo.poRutama == null ? "—" : formatPrice(t.gorivo.poRutama)
                }`}
              />
              <KarticaPoslovanja label="Dobit" vrijednost={formatPrice(t.dobit)} hint="neto − nabavno − troškovi" />
            </div>
          )}
          {d.vrste.length === 0 && (
            <p className="rounded-card bg-honey-100 px-4 py-3 text-sm font-medium text-honey-700">
              Prvo dodaj vrste troškova (dolje, „Vrste troškova“) — npr. Gorivo, Zakup, Ambalaža, Struja.
            </p>
          )}

          <TroskoviMjesecaOdjeljak d={d} onUredi={(trosak) => setLadica({ trosak })} />
          <PonavljajuciOdjeljak d={d} />
          <GorivoOdjeljak d={d} />
          <VrsteOdjeljak d={d} />

          {ladica && (
            <TrosakLadica
              d={d}
              trosak={ladica.trosak}
              onClose={() => setLadica(null)}
            />
          )}
        </>
      )}
    </div>
  );
}

function useOsvjezi() {
  const qc = useQueryClient();
  return () => {
    qc.invalidateQueries({ queryKey: ["admin", "troskovi"] });
    qc.invalidateQueries({ queryKey: ["admin", "poslovanje"] });
  };
}

/** Dugme koje traži drugi klik za potvrdu — bez browser dijaloga. */
function ObrisiDugme({ onObrisi, oznaka }: { onObrisi: () => void; oznaka: string }) {
  const [potvrdi, setPotvrdi] = useState(false);
  return (
    <button
      type="button"
      onClick={() => (potvrdi ? onObrisi() : setPotvrdi(true))}
      onBlur={() => setPotvrdi(false)}
      aria-label={oznaka}
      className={cn(
        "inline-flex items-center gap-1 rounded-full px-2 py-1 text-xs font-semibold",
        potvrdi ? "bg-red-700 text-white" : "text-red-700 hover:bg-red-50",
      )}
    >
      <Trash2 className="h-3.5 w-3.5" />
      {potvrdi ? "Sigurno?" : null}
    </button>
  );
}

function TroskoviMjesecaOdjeljak({ d, onUredi }: { d: TroskoviMjeseca; onUredi: (t: Trosak) => void }) {
  const osvjezi = useOsvjezi();
  const { show } = useToast();
  const obrisi = useMutation({
    mutationFn: adminApi.obrisiTrosak,
    onSuccess: () => {
      osvjezi();
      show("Trošak obrisan");
    },
    onError: (e) => show(poruka(e), "error"),
  });
  const ukupno = d.troskovi.reduce((s, t) => s + t.iznos, 0);
  return (
    <Odjeljak naslov="Troškovi mjeseca" opis={`Računi i uplate u ${mjesecTekst(d.godina, d.mjesec)} — ukupno ${formatPrice(ukupno)}.`}>
      <Tabela
        kljuc={(r) => r.id}
        redovi={d.troskovi}
        prazno="Nema unesenih troškova za ovaj mjesec."
        kolone={[
          { naslov: "Datum", vrijednost: (r) => datum(r.datum) },
          { naslov: "Vrsta", vrijednost: (r) => r.vrsta },
          {
            naslov: "Opis",
            vrijednost: (r) => (
              <span className="inline-flex items-center gap-1.5">
                {r.opis || <span className="text-muted">—</span>}
                {r.racunUrl && (
                  <a href={r.racunUrl} target="_blank" rel="noreferrer" aria-label="Račun" className="text-brand-700">
                    <Paperclip className="h-3.5 w-3.5" />
                  </a>
                )}
              </span>
            ),
          },
          { naslov: "Iznos", vrijednost: (r) => formatPrice(r.iznos), desno: true },
          {
            naslov: "",
            vrijednost: (r) => (
              <span className="inline-flex items-center gap-1">
                <button
                  type="button"
                  onClick={() => onUredi(r)}
                  aria-label={`Izmijeni ${r.opis || r.vrsta}`}
                  className="rounded-full p-1.5 text-muted hover:bg-sand hover:text-ink"
                >
                  <Pencil className="h-3.5 w-3.5" />
                </button>
                <ObrisiDugme oznaka={`Obriši ${r.opis || r.vrsta}`} onObrisi={() => obrisi.mutate(r.id)} />
              </span>
            ),
            desno: true,
          },
        ]}
      />
    </Odjeljak>
  );
}

function TrosakLadica({ d, trosak, onClose }: { d: TroskoviMjeseca; trosak: Trosak | null; onClose: () => void }) {
  const osvjezi = useOsvjezi();
  const { show } = useToast();
  const danas = new Date();
  const uMjesecu = danas.getFullYear() === d.godina && danas.getMonth() + 1 === d.mjesec;
  const pocetni = `${d.godina}-${pad(d.mjesec)}-${pad(uMjesecu ? danas.getDate() : 1)}`;
  const aktivne = d.vrste.filter((v) => v.aktivna || v.id === trosak?.vrstaId);
  const [form, setForm] = useState({
    datum: trosak?.datum ?? pocetni,
    vrstaId: trosak?.vrstaId ?? aktivne[0]?.id ?? "",
    iznos: trosak ? String(trosak.iznos) : "",
    opis: trosak?.opis ?? "",
    racunUrl: trosak?.racunUrl ?? "",
  });
  const [greska, setGreska] = useState<string | null>(null);

  const sacuvaj = useMutation({
    mutationFn: () => {
      const body = {
        datum: form.datum,
        vrstaId: form.vrstaId,
        iznos: broj(form.iznos)!,
        opis: form.opis.trim(),
        racunUrl: form.racunUrl.trim() || null,
      };
      return trosak ? adminApi.izmijeniTrosak(trosak.id, body) : adminApi.dodajTrosak(body);
    },
    onSuccess: () => {
      osvjezi();
      show(trosak ? "Trošak izmijenjen" : "Trošak dodan");
      onClose();
    },
    onError: (e) => setGreska(poruka(e)),
  });

  function submit(e: React.FormEvent) {
    e.preventDefault();
    const iznos = broj(form.iznos);
    if (!iznos || iznos <= 0) return setGreska("Unesi iznos veći od nule.");
    if (!form.datum) return setGreska("Unesi datum.");
    if (!form.vrstaId) return setGreska("Izaberi vrstu troška.");
    setGreska(null);
    sacuvaj.mutate();
  }

  return (
    <AdminLadica naslov={trosak ? "Izmijeni trošak" : "Novi trošak"} onClose={onClose}>
      <form onSubmit={submit} className="flex-1 space-y-4 overflow-y-auto p-6">
        <Field label="Datum" required>
          <Input type="date" value={form.datum} onChange={(e) => setForm({ ...form, datum: e.target.value })} />
        </Field>
        <Field label="Vrsta" required>
          <Select value={form.vrstaId} onChange={(e) => setForm({ ...form, vrstaId: e.target.value })}>
            {aktivne.map((v) => (
              <option key={v.id} value={v.id}>
                {v.naziv}
                {v.jeGorivo ? " (gorivo)" : ""}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Iznos (KM)" required>
          <Input inputMode="decimal" value={form.iznos} onChange={(e) => setForm({ ...form, iznos: e.target.value })} placeholder="npr. 85,40" />
        </Field>
        <Field label="Opis">
          <Input
            value={form.opis}
            maxLength={300}
            onChange={(e) => setForm({ ...form, opis: e.target.value })}
            placeholder="npr. Petrol, 45 l"
          />
        </Field>
        <SlikaPolje label="Slika računa" value={form.racunUrl} onChange={(url) => setForm({ ...form, racunUrl: url })} folder="troskovi" />
        {d.vrste.find((v) => v.id === form.vrstaId)?.jeGorivo && (
          <p className="rounded-card bg-sand px-3 py-2 text-xs text-muted">
            Čim u mjesecu postoji račun za gorivo, dobit koristi račune umjesto procjene po kilometrima ruta.
          </p>
        )}
        {greska && <p className="text-sm font-medium text-red-700">{greska}</p>}
        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="ghost" onClick={onClose}>
            Odustani
          </Button>
          <Button type="submit" disabled={sacuvaj.isPending}>
            {trosak ? "Sačuvaj" : "Dodaj"}
          </Button>
        </div>
      </form>
    </AdminLadica>
  );
}

function PonavljajuciOdjeljak({ d }: { d: TroskoviMjeseca }) {
  const osvjezi = useOsvjezi();
  const { show } = useToast();
  const aktivne = d.vrste.filter((v) => v.aktivna);
  const [form, setForm] = useState({ vrstaId: "", iznos: "", opis: "" });
  const sljedeci = susjedniMjesec(d.godina, d.mjesec, 1);

  const dodaj = useMutation({
    mutationFn: () =>
      adminApi.dodajPonavljajuciTrosak({
        vrstaId: form.vrstaId || aktivne[0]?.id,
        iznos: broj(form.iznos)!,
        opis: form.opis.trim(),
        odGodina: d.godina,
        odMjesec: d.mjesec,
      }),
    onSuccess: () => {
      osvjezi();
      setForm({ vrstaId: form.vrstaId, iznos: "", opis: "" });
      show("Ponavljajući trošak dodan");
    },
    onError: (e) => show(poruka(e), "error"),
  });
  const zaustavi = useMutation({
    mutationFn: (p: PonavljajuciTrosak) => adminApi.zaustaviPonavljajuciTrosak(p.id, sljedeci.godina, sljedeci.mjesec),
    onSuccess: () => {
      osvjezi();
      show(`Zaustavljeno — posljednji mjesec je ${mjesecTekst(d.godina, d.mjesec)}`);
    },
    onError: (e) => show(poruka(e), "error"),
  });
  const obrisi = useMutation({
    mutationFn: adminApi.obrisiPonavljajuciTrosak,
    onSuccess: () => {
      osvjezi();
      show("Ponavljajući trošak obrisan iz svih mjeseci");
    },
    onError: (e) => show(poruka(e), "error"),
  });

  function submit(e: React.FormEvent) {
    e.preventDefault();
    const iznos = broj(form.iznos);
    if (!iznos || iznos <= 0) return show("Unesi mjesečni iznos veći od nule.", "error");
    if (!(form.vrstaId || aktivne[0])) return show("Prvo dodaj vrstu troška.", "error");
    dodaj.mutate();
  }

  const opseg = (p: PonavljajuciTrosak) =>
    `od ${mjesecTekst(p.odGodina, p.odMjesec)}${p.doGodina && p.doMjesec ? ` do ${mjesecTekst(p.doGodina, p.doMjesec)}` : ""}`;

  return (
    <Odjeljak
      naslov="Ponavljajući troškovi"
      opis="Zakup, struja, internet, knjigovođa… Unosiš jednom; važe svaki mjesec dok ih ne zaustaviš."
    >
      <Tabela
        kljuc={(r) => r.id}
        redovi={d.ponavljajuci}
        prazno="Nema ponavljajućih troškova."
        kolone={[
          { naslov: "Vrsta", vrijednost: (r) => r.vrsta },
          { naslov: "Opis", vrijednost: (r) => r.opis || <span className="text-muted">—</span> },
          {
            naslov: "Važi",
            vrijednost: (r) => <span className={cn(!r.vaziUMjesecu && "text-muted")}>{opseg(r)}</span>,
          },
          { naslov: "Mjesečno", vrijednost: (r) => formatPrice(r.iznos), desno: true },
          {
            naslov: "",
            vrijednost: (r) => (
              <span className="inline-flex items-center gap-1">
                {r.vaziUMjesecu && (r.doGodina == null || r.doGodina * 12 + (r.doMjesec ?? 0) > d.godina * 12 + d.mjesec) && (
                  <button
                    type="button"
                    onClick={() => zaustavi.mutate(r)}
                    className="rounded-full px-2 py-1 text-xs font-semibold text-honey-700 hover:bg-honey-100"
                  >
                    Zaustavi poslije ovog mjeseca
                  </button>
                )}
                <ObrisiDugme oznaka={`Obriši ${r.opis || r.vrsta}`} onObrisi={() => obrisi.mutate(r.id)} />
              </span>
            ),
            desno: true,
          },
        ]}
      />
      {aktivne.length > 0 && (
        <form onSubmit={submit} className="flex flex-wrap items-end gap-2 print:hidden">
          <Field label="Vrsta">
            <Select value={form.vrstaId || aktivne[0].id} onChange={(e) => setForm({ ...form, vrstaId: e.target.value })}>
              {aktivne.map((v) => (
                <option key={v.id} value={v.id}>
                  {v.naziv}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Opis">
            <Input value={form.opis} onChange={(e) => setForm({ ...form, opis: e.target.value })} placeholder="npr. zakup magacina" />
          </Field>
          <Field label="Mjesečno (KM)">
            <Input inputMode="decimal" value={form.iznos} onChange={(e) => setForm({ ...form, iznos: e.target.value })} className="w-32" />
          </Field>
          <Button type="submit" size="sm" disabled={dodaj.isPending} className="mb-1">
            <Plus className="h-4 w-4" /> Od {mjesecTekst(d.godina, d.mjesec)}
          </Button>
        </form>
      )}
      <p className="text-xs text-muted">
        „Obriši“ uklanja trošak iz svih mjeseci (za pogrešan unos); kad trošak prestane, koristi „Zaustavi“.
      </p>
    </Odjeljak>
  );
}

function GorivoOdjeljak({ d }: { d: TroskoviMjeseca }) {
  const osvjezi = useOsvjezi();
  const { show } = useToast();
  const zadnja = d.cijeneGoriva[0];
  const [form, setForm] = useState({
    vaziOd: `${d.godina}-${pad(d.mjesec)}-01`,
    cijena: "",
    potrosnja: zadnja ? String(zadnja.potrosnjaL100km) : "",
  });
  const dodaj = useMutation({
    mutationFn: () =>
      adminApi.dodajCijenuGoriva({ vaziOd: form.vaziOd, cijenaPoLitru: broj(form.cijena)!, potrosnjaL100km: broj(form.potrosnja)! }),
    onSuccess: () => {
      osvjezi();
      setForm({ ...form, cijena: "" });
      show("Cijena goriva sačuvana");
    },
    onError: (e) => show(poruka(e), "error"),
  });
  const obrisi = useMutation({
    mutationFn: adminApi.obrisiCijenuGoriva,
    onSuccess: () => {
      osvjezi();
      show("Cijena goriva obrisana");
    },
    onError: (e) => show(poruka(e), "error"),
  });

  function submit(e: React.FormEvent) {
    e.preventDefault();
    if (!form.vaziOd) return show("Unesi datum od kad važi.", "error");
    if (!broj(form.cijena)) return show("Unesi cijenu po litru.", "error");
    if (!broj(form.potrosnja)) return show("Unesi potrošnju vozila (l/100 km).", "error");
    dodaj.mutate();
  }

  return (
    <Odjeljak
      naslov="Gorivo po kilometrima ruta"
      opis="Trošak rute = kilometri cijele ture (magacin → kupci → magacin) × potrošnja × cijena goriva na dan rute. Kad cijena promijeni, dodaj novu — stari mjeseci ostaju po staroj."
    >
      <Tabela<CijenaGoriva>
        kljuc={(r) => r.id}
        redovi={d.cijeneGoriva}
        prazno="Cijena goriva nije unesena — gorivo po rutama se ne računa."
        kolone={[
          { naslov: "Važi od", vrijednost: (r) => datum(r.vaziOd) },
          { naslov: "Cijena po litru", vrijednost: (r) => formatPrice(r.cijenaPoLitru), desno: true },
          { naslov: "Potrošnja", vrijednost: (r) => `${r.potrosnjaL100km.toLocaleString("sr-Latn")} l/100 km`, desno: true },
          {
            naslov: "Po km",
            vrijednost: (r) => formatPrice((r.cijenaPoLitru * r.potrosnjaL100km) / 100),
            desno: true,
          },
          {
            naslov: "",
            vrijednost: (r) => <ObrisiDugme oznaka={`Obriši cijenu od ${datum(r.vaziOd)}`} onObrisi={() => obrisi.mutate(r.id)} />,
            desno: true,
          },
        ]}
      />
      <form onSubmit={submit} className="flex flex-wrap items-end gap-2 print:hidden">
        <Field label="Važi od">
          <Input type="date" value={form.vaziOd} onChange={(e) => setForm({ ...form, vaziOd: e.target.value })} />
        </Field>
        <Field label="KM po litru">
          <Input inputMode="decimal" value={form.cijena} onChange={(e) => setForm({ ...form, cijena: e.target.value })} className="w-28" placeholder="2,65" />
        </Field>
        <Field label="Potrošnja l/100 km">
          <Input inputMode="decimal" value={form.potrosnja} onChange={(e) => setForm({ ...form, potrosnja: e.target.value })} className="w-28" placeholder="9" />
        </Field>
        <Button type="submit" size="sm" disabled={dodaj.isPending} className="mb-1">
          <Plus className="h-4 w-4" /> Dodaj cijenu
        </Button>
      </form>
    </Odjeljak>
  );
}

function VrsteOdjeljak({ d }: { d: TroskoviMjeseca }) {
  const osvjezi = useOsvjezi();
  const { show } = useToast();
  const [naziv, setNaziv] = useState("");
  const sacuvaj = useMutation({
    mutationFn: ({ v, izmjena }: { v: VrstaTroska | null; izmjena: Partial<VrstaTroska> }) => {
      const body = {
        naziv: izmjena.naziv ?? v?.naziv ?? "",
        jeGorivo: izmjena.jeGorivo ?? v?.jeGorivo ?? false,
        redoslijed: izmjena.redoslijed ?? v?.redoslijed ?? d.vrste.length + 1,
        aktivna: izmjena.aktivna ?? v?.aktivna ?? true,
      };
      return v ? adminApi.izmijeniVrstuTroska(v.id, body) : adminApi.dodajVrstuTroska(body);
    },
    onSuccess: (_, { v }) => {
      osvjezi();
      if (!v) setNaziv("");
      show(v ? "Vrsta sačuvana" : "Vrsta dodana");
    },
    onError: (e) => show(poruka(e), "error"),
  });
  const obrisi = useMutation({
    mutationFn: adminApi.obrisiVrstuTroska,
    onSuccess: () => {
      osvjezi();
      show("Vrsta obrisana");
    },
    onError: (e) => show(poruka(e), "error"),
  });

  return (
    <Odjeljak naslov="Vrste troškova" opis="„Gorivo“ označava vrstu čiji računi zamjenjuju procjenu po rutama.">
      <Tabela<VrstaTroska>
        kljuc={(r) => r.id}
        redovi={d.vrste}
        prazno="Još nema vrsta troškova."
        kolone={[
          { naslov: "Naziv", vrijednost: (r) => <span className={cn(!r.aktivna && "text-muted line-through")}>{r.naziv}</span> },
          {
            naslov: "Gorivo",
            vrijednost: (r) => (
              <input
                type="checkbox"
                checked={r.jeGorivo}
                aria-label={`${r.naziv} je gorivo`}
                onChange={(e) => sacuvaj.mutate({ v: r, izmjena: { jeGorivo: e.target.checked } })}
                className="h-4 w-4 accent-brand-600"
              />
            ),
          },
          {
            naslov: "U upotrebi",
            vrijednost: (r) => (
              <input
                type="checkbox"
                checked={r.aktivna}
                aria-label={`${r.naziv} u upotrebi`}
                onChange={(e) => sacuvaj.mutate({ v: r, izmjena: { aktivna: e.target.checked } })}
                className="h-4 w-4 accent-brand-600"
              />
            ),
          },
          {
            naslov: "",
            vrijednost: (r) =>
              r.uUpotrebi ? (
                <span className="text-xs text-muted">ima troškova</span>
              ) : (
                <ObrisiDugme oznaka={`Obriši vrstu ${r.naziv}`} onObrisi={() => obrisi.mutate(r.id)} />
              ),
            desno: true,
          },
        ]}
      />
      <form
        onSubmit={(e) => {
          e.preventDefault();
          if (!naziv.trim()) return;
          sacuvaj.mutate({ v: null, izmjena: { naziv: naziv.trim() } });
        }}
        className="flex flex-wrap items-end gap-2 print:hidden"
      >
        <Field label="Nova vrsta">
          <Input value={naziv} maxLength={80} onChange={(e) => setNaziv(e.target.value)} placeholder="npr. Održavanje vozila" />
        </Field>
        <Button type="submit" size="sm" disabled={sacuvaj.isPending || !naziv.trim()} className="mb-1">
          <Plus className="h-4 w-4" /> Dodaj vrstu
        </Button>
      </form>
    </Odjeljak>
  );
}
