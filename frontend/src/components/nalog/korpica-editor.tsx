"use client";

import { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Clock, Plus, RotateCcw, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { LoadingBlock } from "@/components/ui/spinner";
import { SmartImage } from "@/components/ui/smart-image";
import { useToast } from "@/components/ui/toast";
import { queryKeys } from "@/lib/api/hooks";
import { accountApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import type { PonudaArtikla, StavkaMojeKorpice } from "@/types/domain";
import { cn, formatDateTime, formatPrice, unitSuffix } from "@/lib/utils";

/**
 * Ispravna srpska množina za "artikal" — koristi se u "fali N artikal/artikla/artikala".
 * Brojevi na ovom ekranu su obično mali (1–5), ali pravilo je opšte (npr. 11 -> "artikala",
 * 21 -> "artikal") da se ne polomi ako `minimalanBrojArtikala` ikad poraste.
 */
function imenicaArtikal(n: number): string {
  const mod100 = n % 100;
  const mod10 = n % 10;
  if (mod10 === 1 && mod100 !== 11) return "artikal";
  if (mod10 >= 2 && mod10 <= 4 && !(mod100 >= 12 && mod100 <= 14)) return "artikla";
  return "artikala";
}

/** Para vrijednost u cente da poređenja (>=, <=) ne budu žrtva binarnog zapisa decimalnih
 * brojeva — na ovoj skali (KM, dva decimalna mjesta) razlika je mikroskopska, ali poređenje
 * granica budžeta je upravo mjesto gdje se takva greška najskuplje plati. */
function uCentima(iznos: number): number {
  return Math.round(iznos * 100);
}

interface Red {
  productId: string;
  naziv: string;
  kolicina: number;
  jedinica: string;
  ukupno: number;
  dodaoKupac: boolean;
  imageUrl?: string;
}

/**
 * Red za jedan izabrani `productId`. Artikal koji je već u `stavke` nosi količinu i
 * jediničnu cijenu koju je server zapamtio (template); artikal koji kupac dodaje a koji
 * (još) nije u `stavke` dolazi iz `ponuda` i uvijek ima količinu 1 u svojoj jedinici —
 * to je pravilo za novododane artikle, ne pretpostavka (vidi task-10-brief.md).
 */
function redZaId(id: string, stavke: StavkaMojeKorpice[], ponuda: PonudaArtikla[]): Red {
  const s = stavke.find((x) => x.productId === id);
  if (s) {
    return {
      productId: id,
      naziv: s.naziv,
      kolicina: s.kolicina,
      jedinica: s.jedinica,
      ukupno: Math.round(s.kolicina * s.jedinicnaCijena * 100) / 100,
      dodaoKupac: s.dodaoKupac,
    };
  }
  const p = ponuda.find((x) => x.productId === id);
  return {
    productId: id,
    naziv: p?.naziv ?? id,
    kolicina: 1,
    jedinica: p?.jedinica ?? "kom",
    ukupno: p?.cijena ?? 0,
    dodaoKupac: true,
    imageUrl: p?.imageUrl,
  };
}

function isteStavke(a: string[], b: string[]): boolean {
  if (a.length !== b.length) return false;
  const skupB = new Set(b);
  return a.every((id) => skupB.has(id));
}

/**
 * Jedna kartica, jedna korpica. Učitava sopstvene detalje preko `orderItemId` i drži
 * izbor lokalno kao spisak `productId` — server je mjerodavan za granice budžeta i broja
 * artikala, ovaj ekran samo sprečava zahtjev koji unaprijed pada (vidi task-10-brief.md).
 */
export function KorpicaEditor({ orderItemId }: { orderItemId: string }) {
  const qc = useQueryClient();
  const { show } = useToast();

  const {
    data: detalji,
    isLoading,
    isError,
    refetch,
  } = useQuery({
    queryKey: queryKeys.mojaKorpica(orderItemId),
    queryFn: () => accountApi.mojaKorpica(orderItemId),
  });

  const [selected, setSelected] = useState<string[]>([]);
  const [errors, setErrors] = useState<Record<string, string[]>>({});
  const [error, setError] = useState<string | null>(null);

  // Potpis skupa stavki koje server trenutno javlja — NE ključamo efekat na `detalji`
  // direktno, jer `useQuery` vrati novi objekat na SVAKI refetch (npr. refetch na fokus
  // prozora, koji je default u TanStack Query-u), pa bi identitet `detalji` promijenio
  // potpis i poslije refetcha koji ništa nije promijenio na serveru — i tiho pobrisao
  // neposačuvan izbor kupca dok je on samo prešao na drugi tab i vratio se. Potpis je
  // stabilan dok se skup productId-ova ne promijeni, bez obzira na to koliko se puta
  // refetch desio.
  const potpisServera = detalji?.stavke.map((s) => s.productId).sort().join("|") ?? "";

  // Resinhronizacija sa serverom — i pri prvom učitavanju i poslije uspješnog čuvanja
  // (onSuccess invalidira ovaj upit, refetch donese novi `detalji` sa DRUGAČIJIM
  // potpisom, a ovaj efekat vrati `selected` na ono što server upravo potvrdio kao
  // istinu). Zato Sačuvaj poslije uspjeha ponovo postane onemogućen — `izmijenjeno`
  // pada na `false`.
  useEffect(() => {
    if (detalji) setSelected(detalji.stavke.map((s) => s.productId));
    // Namjerno ključano na potpis (stabilan skup id-ova), ne na `detalji` (novi objekat
    // na svaki refetch) — vidi komentar iznad efekta.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [potpisServera]);

  const original = useMemo(() => detalji?.stavke.map((s) => s.productId) ?? [], [detalji]);

  const mutation = useMutation({
    mutationFn: (productIds: string[]) => accountApi.izmijeniKorpicu(orderItemId, productIds),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.mojaKorpica(orderItemId) });
      qc.invalidateQueries({ queryKey: queryKeys.mojeKorpice() });
      setErrors({});
      setError(null);
      show("Korpica sačuvana.");
    },
    onError: (err) => {
      // Kućni obrazac (vidi register/page.tsx:70-74): server za budzet_premali,
      // budzet_preveliki, premalo_artikala, duplirani_artikal, artikal_nije_podoban,
      // rok_istekao i korpica_se_pakuje šalje PRAZAN fieldErrors, pa poruka mora
      // stići kroz `err.message` ili ekran izgleda zamrznut.
      if (err instanceof ApiRequestError) {
        setErrors(err.fieldErrors);
        if (Object.keys(err.fieldErrors).length === 0) setError(err.message);
      } else {
        setError("Došlo je do greške.");
      }
    },
  });

  if (isLoading) {
    return (
      <div className="rounded-card border border-line bg-surface p-5 shadow-card">
        <LoadingBlock label="Učitavam korpicu…" />
      </div>
    );
  }

  if (isError || !detalji) {
    return (
      <div className="rounded-card border border-line bg-surface p-5 shadow-card">
        <p className="text-sm font-medium text-red-700">Ne mogu da učitam ovu korpicu.</p>
        <Button variant="secondary" size="sm" className="mt-3" onClick={() => refetch()}>
          Pokušaj ponovo
        </Button>
      </div>
    );
  }

  const { korpica, stavke, ponuda } = detalji;

  function dodaj(id: string) {
    setError(null);
    setErrors({});
    setSelected((prev) => (prev.includes(id) ? prev : [...prev, id]));
  }

  function izbaci(id: string) {
    setError(null);
    setErrors({});
    setSelected((prev) => prev.filter((x) => x !== id));
  }

  function vratiNaPocetno() {
    setError(null);
    setErrors({});
    setSelected(original);
  }

  const redovi = selected.map((id) => redZaId(id, stavke, ponuda));
  const vrijednost = Math.round(redovi.reduce((acc, r) => acc + r.ukupno, 0) * 100) / 100;
  // Granice su inkluzivne: donja je 95% cilja (zaokruženo na 2 decimale), gornja je cilj.
  const donjaGranica = Math.round(korpica.ciljnaVrijednost * 0.95 * 100) / 100;
  const gornjaGranica = korpica.ciljnaVrijednost;
  const uOpsegu = uCentima(vrijednost) >= uCentima(donjaGranica) && uCentima(vrijednost) <= uCentima(gornjaGranica);
  const dovoljnoArtikala = selected.length >= korpica.minimalanBrojArtikala;
  const validno = uOpsegu && dovoljnoArtikala;
  const izmijenjeno = !isteStavke(selected, original);
  const rokIstekao = new Date(korpica.rokIzmjene).getTime() < Date.now();

  // Ispod Sačuvaj: isti redoslijed kao server (provjerava budžet PRIJE broja artikala,
  // vidi task-10-brief.md), da poruka ovdje nikad ne kaže nešto što server poreče.
  let napomena: string | null = null;
  if (rokIstekao) {
    napomena = "Rok za izmjenu ove korpice je istekao.";
  } else if (uCentima(vrijednost) < uCentima(donjaGranica)) {
    napomena = `još ${formatPrice(donjaGranica - vrijednost)} do 95%`;
  } else if (uCentima(vrijednost) > uCentima(gornjaGranica)) {
    napomena = `preko budžeta za ${formatPrice(vrijednost - gornjaGranica)}`;
  } else if (!dovoljnoArtikala) {
    const n = korpica.minimalanBrojArtikala - selected.length;
    napomena = `fali ${n} ${imenicaArtikal(n)}`;
  } else if (!izmijenjeno) {
    napomena = "Nema izmjena za čuvanje.";
  }

  const disabled = rokIstekao || !validno || !izmijenjeno || mutation.isPending;

  // Birač nudi samo ono što još nije izabrano; artikal koji je kupac upravo izbacio
  // se time sam vrati u ponudu (tačno ono što "Vrati na početno" i inače radi).
  const dostupnaPonuda = ponuda.filter((p) => !selected.includes(p.productId));
  const izOveKorpice = dostupnaPonuda.filter((p) => p.izOveKorpice);
  const ostaloIzPonude = dostupnaPonuda.filter((p) => !p.izOveKorpice);

  const pct =
    korpica.ciljnaVrijednost > 0
      ? Math.round((vrijednost / korpica.ciljnaVrijednost) * 100)
      : 0;

  const greska = error ?? Object.values(errors).flat()[0] ?? null;

  return (
    <div className="flex flex-col gap-5 rounded-card border border-line bg-surface p-5 shadow-card">
      <div className="flex items-start gap-3">
        <span className="relative h-14 w-14 shrink-0 overflow-hidden rounded-xl bg-sand">
          <SmartImage src={korpica.imageUrl} alt={korpica.naziv} sizes="56px" />
        </span>
        <div className="min-w-0 flex-1">
          <p className="truncate font-display font-semibold text-ink">{korpica.naziv}</p>
          <p className="truncate text-sm text-muted">
            {korpica.kodNarudzbe} · sedmica {korpica.sedmica}/{korpica.godina}
          </p>
          <p
            className={cn(
              "mt-1 flex items-center gap-1.5 text-sm",
              rokIstekao ? "font-medium text-red-700" : "text-muted",
            )}
          >
            <Clock className="h-3.5 w-3.5 shrink-0" />
            {rokIstekao ? "Rok za izmjenu je istekao: " : "Rok za izmjenu: "}
            {formatDateTime(korpica.rokIzmjene)}
          </p>
        </div>
      </div>

      <div>
        <div className="flex items-center justify-between text-sm">
          <span className="font-medium text-ink">Vrijednost korpice</span>
          <span className={cn("font-semibold", uOpsegu ? "text-brand-700" : "text-red-700")}>
            {formatPrice(vrijednost)} / {formatPrice(korpica.ciljnaVrijednost)} ({pct}%)
          </span>
        </div>
        <div className="mt-1.5 h-2 w-full overflow-hidden rounded-full bg-sand">
          <div
            className={cn("h-full rounded-full transition-all", uOpsegu ? "bg-brand-600" : "bg-red-600")}
            style={{ width: `${Math.min(100, Math.max(0, pct))}%` }}
          />
        </div>
        <p className={cn("mt-1 text-xs", dovoljnoArtikala ? "text-muted" : "font-medium text-red-700")}>
          {selected.length} / {korpica.minimalanBrojArtikala} artikala
        </p>
      </div>

      <ul className="flex flex-col gap-2">
        {redovi.map((r) => (
          <li
            key={r.productId}
            className="flex items-center justify-between gap-3 rounded-card border border-line bg-cream p-3"
          >
            <div className="flex min-w-0 items-center gap-3">
              <span className="relative h-11 w-11 shrink-0 overflow-hidden rounded-lg bg-sand">
                <SmartImage src={r.imageUrl} alt={r.naziv} sizes="44px" />
              </span>
              <div className="min-w-0">
                <p className="truncate font-medium text-ink">
                  {r.naziv}{" "}
                  <span className="font-normal text-muted">
                    {r.kolicina} {r.jedinica}
                  </span>
                  {r.dodaoKupac && (
                    <span className="ml-2 rounded-full bg-sand px-2 py-0.5 text-[10px] font-medium text-muted">
                      tvoj izbor
                    </span>
                  )}
                </p>
                <p className="text-sm text-muted">{formatPrice(r.ukupno)}</p>
              </div>
            </div>
            <button
              type="button"
              aria-label={`Izbaci ${r.naziv}`}
              onClick={() => izbaci(r.productId)}
              disabled={rokIstekao}
              className="shrink-0 rounded-full p-1.5 text-muted transition hover:bg-sand hover:text-red-700 disabled:pointer-events-none disabled:opacity-50"
            >
              <X className="h-4 w-4" />
            </button>
          </li>
        ))}
      </ul>

      {(izOveKorpice.length > 0 || ostaloIzPonude.length > 0) && (
        <div className="flex flex-col gap-3 border-t border-line pt-4">
          {izOveKorpice.length > 0 && (
            <PonudaGrupa
              naslov="Iz ove korpice"
              stavke={izOveKorpice}
              onDodaj={dodaj}
              disabled={rokIstekao}
            />
          )}
          {ostaloIzPonude.length > 0 && (
            <PonudaGrupa
              naslov="Ostalo iz ponude"
              stavke={ostaloIzPonude}
              onDodaj={dodaj}
              disabled={rokIstekao}
            />
          )}
        </div>
      )}

      {greska && (
        <p className="rounded-lg bg-red-50 px-3 py-2 text-sm font-medium text-red-700">{greska}</p>
      )}

      <div className="flex flex-col gap-2 border-t border-line pt-4">
        <div className="flex flex-wrap items-center gap-3">
          <Button type="button" onClick={() => mutation.mutate(selected)} disabled={disabled}>
            {mutation.isPending ? "Čuvam…" : "Sačuvaj"}
          </Button>
          <Button
            type="button"
            variant="secondary"
            onClick={vratiNaPocetno}
            disabled={!izmijenjeno || mutation.isPending}
          >
            <RotateCcw className="h-4 w-4" /> Vrati na početno
          </Button>
        </div>
        {napomena && (
          <p className={cn("text-xs", rokIstekao || !validno ? "text-red-700" : "text-muted")}>
            {napomena}
          </p>
        )}
      </div>
    </div>
  );
}

function PonudaGrupa({
  naslov,
  stavke,
  onDodaj,
  disabled,
}: {
  naslov: string;
  stavke: PonudaArtikla[];
  onDodaj: (id: string) => void;
  disabled: boolean;
}) {
  return (
    <div>
      <p className="mb-1.5 text-xs font-semibold uppercase tracking-wide text-muted">{naslov}</p>
      <div className="grid gap-2 sm:grid-cols-2">
        {stavke.map((p) => (
          <button
            key={p.productId}
            type="button"
            onClick={() => onDodaj(p.productId)}
            disabled={disabled}
            className="flex items-center justify-between gap-2 rounded-lg border border-line bg-cream px-3 py-2 text-left text-sm transition hover:border-brand-500 hover:bg-sand/60 disabled:pointer-events-none disabled:opacity-50"
          >
            <span className="flex min-w-0 items-center gap-2">
              <span className="relative h-9 w-9 shrink-0 overflow-hidden rounded-lg bg-sand">
                <SmartImage src={p.imageUrl} alt={p.naziv} sizes="36px" />
              </span>
              <span className="truncate text-ink">{p.naziv}</span>
            </span>
            <span className="flex shrink-0 items-center gap-1 text-muted">
              {formatPrice(p.cijena)}
              {unitSuffix(p.jedinica)}
              <Plus className="h-4 w-4 text-brand-600" />
            </span>
          </button>
        ))}
      </div>
    </div>
  );
}
