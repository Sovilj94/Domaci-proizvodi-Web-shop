"use client";

import { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ArrowLeftRight, Info, Plus, Search, Star, TriangleAlert, X } from "lucide-react";
import { AdminLadica } from "@/components/admin/admin-ladica";
import { Stepper } from "@/components/admin/kutija-editor";
import { Button } from "@/components/ui/button";
import { LoadingBlock } from "@/components/ui/spinner";
import { EmptyState } from "@/components/ui/empty-state";
import { SmartImage } from "@/components/ui/smart-image";
import { useToast } from "@/components/ui/toast";
import { adminApi, adminKorpiceApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import {
  type KutijaRed,
  ekonomicnost,
  granicaKutije,
  kolicinaZaDodavanje,
  mjesecSedmice,
  najmanjaKolicina,
  najvecaKolicina,
  sezonskaPonuda,
  slobodnoZaDodavanje,
  zamijeni,
  zbir,
} from "@/lib/admin/kutija";
import { bezKvacica } from "@/lib/admin/proizvodi-tabela";
import { formatMarza, tonMarze } from "@/lib/marza";
import { cn, formatPrice, unitSuffix } from "@/lib/utils";
import type { Product } from "@/types/domain";

/** Poruka iz ApiRequestError ako je ima; inače rezerva. */
function poruka(err: unknown, rezerva: string): string {
  return err instanceof ApiRequestError && err.message.trim() ? err.message : rezerva;
}

/** Kupčev budžet ide od 95 % objavljene vrijednosti (server: IzmijeniMojuKorpicuHandler.DonjaGranica). */
const DONJA_GRANICA_OBJAVLJENOG = 0.95;
const TON = { nema: "text-muted", losa: "text-red-700", tijesna: "text-honey-700", dobra: "text-brand-700" } as const;
const broj = (n: number) => n.toLocaleString("sr-Latn-BA");

/**
 * Editor sedmičnog sadržaja korpice — prozor na sredini, dvije kolone:
 * lijevo **U korpici** (količine, „Zamijeni", ukloni), desno **Predložene zamjene** i ispod
 * ostala sezonska ponuda te vrste korpice. Zamjena ide u dva klika: „Zamijeni" na artiklu
 * lijevo, pa „Zamijeni" na artiklu desno — novi ulazi u istoj vrijednosti (KM), a kad dolazi
 * sa spiska predloženih zamjena, izbačeni artikal zauzima njegovo mjesto na spisku.
 * Na vrhu stalno stoje vrijednost, marža i upozorenje kad korpica prestane biti ekonomična.
 *
 * Šta se smije zavisi od statusa koji vrati server:
 * - **Nacrt** — do cilja generisanja; na pravila editor upozorava, zaustavlja ih tek objava.
 * - **Objavljen** — 95–100 % vrijednosti sa kojom je objavljen i iznad najmanje marže; artikal
 *   koji ostaje računa se po cijeni snimljenoj pri objavi. Već naručene kutije se ne mijenjaju.
 * - **Zaključan** — samo pregled.
 *
 * `PUT /admin/korpice/sadrzaj/{id}` zamjenjuje CIJELI sadržaj (i spisak zamjena), pa se lokalni
 * redovi popune tek kad sadržaj stigne (do tada `null`) — nikad se ne šalje prazna lista.
 */
export function SadrzajNacrtEditor({
  sadrzajId,
  korpicaNaziv,
  onClose,
}: {
  sadrzajId: string;
  korpicaNaziv: string;
  onClose: () => void;
}) {
  const qc = useQueryClient();
  const { show } = useToast();
  const [redovi, setRedovi] = useState<KutijaRed[] | null>(null);
  const [zamjene, setZamjene] = useState<string[] | null>(null);
  /** Artikal u korpici za koji admin upravo bira zamjenu. */
  const [izabran, setIzabran] = useState<string | null>(null);
  /** Vrijednost sa kojom je kutija otvorena — granica je cilj ili ona, šta je veće. */
  const [pocetna, setPocetna] = useState(0);
  const [upit, setUpit] = useState("");

  const sadrzajKljuc = ["admin", "korpice", "sadrzaj", sadrzajId] as const;
  const { data: sadrzaj, isLoading, isError } = useQuery({
    queryKey: sadrzajKljuc,
    queryFn: () => adminKorpiceApi.sadrzaj(sadrzajId),
  });
  const { data: sviArtikli } = useQuery({ queryKey: ["admin", "products"], queryFn: () => adminApi.products() });
  const poId = useMemo(() => new Map<string, Product>((sviArtikli ?? []).map((a) => [a.id, a])), [sviArtikli]);

  const objavljen = sadrzaj?.status === "Objavljen";
  const samoPregled = sadrzaj?.status === "Zakljucan";

  // Sadržaj se u lokalni state popuni TAČNO JEDNOM (kad stignu i sadržaj i katalog) — refetch
  // sa nekog drugog panela ne smije pregaziti izmjene koje admin još nije sačuvao.
  useEffect(() => {
    if (!sadrzaj || !sviArtikli || redovi !== null) return;
    const snimak = sadrzaj.status !== "Nacrt";
    const cijene = new Map(sadrzaj.cijene.map((c) => [c.productId, c]));
    const novi = sadrzaj.stavke.map((s): KutijaRed => {
      const p = poId.get(s.productId);
      const c = cijene.get(s.productId);
      return {
        productId: s.productId,
        naziv: s.naziv,
        jedinica: s.jedinica,
        kolicina: s.kolicina,
        // Objavljen: cijena snimljena pri objavi (server je zadržava za artikal koji ostaje);
        // nacrt: katalog (server pri čuvanju nacrta uzima katalog).
        cijena: snimak ? (c?.jedinicnaCijena ?? p?.price ?? 0) : (p?.price ?? c?.jedinicnaCijena ?? 0),
        nabavna: p?.nabavnaCijena ?? c?.nabavnaCijena ?? null,
        imageUrl: s.imageUrl,
        boja: p?.boja,
      };
    });
    setPocetna(zbir(novi, sadrzaj.cijenaKorpice).vrijednost);
    setRedovi(novi);
    // Zamjena van kategorija korpice (pavlaka u korpici povrća — ostatak starog generatora) se
    // ne prikazuje: izbacuje se odmah, a čuvanje je briše i iz baze. Zaključan se ne dira.
    const kategorije = new Set(sadrzaj.kategorijeIds);
    const sve = sadrzaj.zamjene.map((z) => z.productId);
    const dobre =
      kategorije.size === 0 || sadrzaj.status === "Zakljucan"
        ? sve
        : sve.filter((id) => {
            const p = poId.get(id);
            return !p || kategorije.has(p.categoryId);
          });
    setZamjene(dobre);
  }, [sadrzaj, sviArtikli, redovi, poId]);

  const spreman = redovi !== null && zamjene !== null && !!sadrzaj;

  const sacuvaj = useMutation({
    mutationFn: () => {
      if (!redovi || !zamjene) throw new Error("Sadržaj još nije učitan.");
      return adminKorpiceApi.izmijeni(sadrzajId, {
        stavke: redovi.map((r) => ({ productId: r.productId, kolicina: r.kolicina })),
        zamjene: zamjene.filter((id) => !redovi.some((r) => r.productId === id)),
      });
    },
    onSuccess: (novo) => {
      qc.setQueryData(sadrzajKljuc, novo);
      qc.invalidateQueries({ queryKey: ["admin", "korpice", "pregled"] });
      qc.invalidateQueries({ queryKey: ["admin", "zadaci"] });
      // Objavljen sadržaj kupci vide: izlog i stranica korpice moraju povući novi.
      if (objavljen) qc.invalidateQueries({ queryKey: ["korpice"] });
      show(objavljen ? "Objavljen sadržaj izmijenjen" : "Nacrt sačuvan");
      onClose();
    },
    onError: (err) => show(poruka(err, "Čuvanje nije uspjelo"), "error"),
  });

  // U korpicu povrća ide samo povrće: ponuda je sezonska roba iz kategorija te vrste korpice
  // (Postavke korpica). Bez podešenih kategorija nema suženja. Server provjerava isto.
  const smijeUKorpicu = (p: Product) => !sadrzaj || sadrzaj.kategorijeIds.length === 0 || sadrzaj.kategorijeIds.includes(p.categoryId);
  const ponuda = useMemo(() => {
    if (!sadrzaj || !sviArtikli) return [];
    const sezona = sezonskaPonuda(sviArtikli, mjesecSedmice(sadrzaj.godina, sadrzaj.sedmica));
    const kategorije = new Set(sadrzaj.kategorijeIds);
    return kategorije.size > 0 ? sezona.filter((p) => kategorije.has(p.categoryId)) : sezona;
  }, [sadrzaj, sviArtikli]);

  const naslov = samoPregled ? "Sadržaj korpice" : objavljen ? "Izmjena objavljenog sadržaja" : "Izmjena sadržaja korpice";
  const podnaslov = sadrzaj ? `${korpicaNaziv} · sedmica ${sadrzaj.sedmica}` : korpicaNaziv;

  if (!spreman) {
    return (
      <AdminLadica naslov={naslov} podnaslov={podnaslov} onClose={onClose} sirina="max-w-5xl" sredina>
        <div className="flex-1 p-6">
          {isError && !isLoading ? (
            <EmptyState title="Sadržaj nije učitan" description="Pokušaj ponovo malo kasnije." />
          ) : (
            <LoadingBlock label="Učitavanje…" />
          )}
        </div>
      </AdminLadica>
    );
  }

  /* ---------- računi ---------- */
  const cijenaKorpice = sadrzaj.cijenaKorpice;
  const minMarza = sadrzaj.minimalnaMarza;
  // Nacrt se puni do cilja generisanja; objavljen i zaključan do vrijednosti sa kojom su objavljeni.
  const cilj = sadrzaj.status === "Nacrt" || !sadrzaj.ciljnaVrijednost ? sadrzaj.ciljGenerisanja : sadrzaj.ciljnaVrijednost;
  const najmanjaVrijednost =
    objavljen && sadrzaj.ciljnaVrijednost > 0 ? Math.round(sadrzaj.ciljnaVrijednost * DONJA_GRANICA_OBJAVLJENOG * 100) / 100 : undefined;
  const z = zbir(redovi, cijenaKorpice);
  const slobodnoKM = slobodnoZaDodavanje(granicaKutije(cilj, pocetna), z.vrijednost);
  const eko = ekonomicnost(redovi, { cijenaKorpice, minimalnaMarza: minMarza, minimalnoArtikala: sadrzaj.minimalanBrojArtikala, najmanjaVrijednost });
  const pct = cilj > 0 ? Math.round((z.vrijednost / cilj) * 100) : 0;
  const uKutiji = new Set(redovi.map((r) => r.productId));
  const redIzabran = redovi.find((r) => r.productId === izabran) ?? null;

  const q = bezKvacica(upit.trim());
  const ostalo = ponuda
    .filter((p) => !uKutiji.has(p.id) && !zamjene.includes(p.id))
    .filter((p) => !q || bezKvacica(`${p.name} ${p.categoryName}`).includes(q));

  // Najmanja smislena količina: pola zadate količine za ovu veličinu korpice (Postavke korpica →
  // Artikli i količine), a bez nje pola vrijednosti jedne kulture. Sve iz baze.
  const poKulturi = sadrzaj.minimalanBrojArtikala > 0 ? sadrzaj.ciljGenerisanja / sadrzaj.minimalanBrojArtikala : 0;
  const poljeKolicine = ({ mala: "kolicinaMala", srednja: "kolicinaSrednja", velika: "kolicinaVelika" } as const)[sadrzaj.velicina];
  const najmanje = (productId: string, cijena: number, jedinica: string) =>
    najmanjaKolicina(poId.get(productId)?.[poljeKolicine], poKulturi, cijena, jedinica);
  const tekstNajmanje = (p: Product) => `najmanje ${broj(najmanje(p.id, p.price, p.unit))} ${p.unit}`;

  const redOd = (p: Product, kolicina: number): KutijaRed => ({
    productId: p.id,
    naziv: p.name,
    jedinica: p.unit,
    kolicina,
    cijena: p.price,
    nabavna: p.nabavnaCijena ?? null,
    imageUrl: p.imageUrl,
    boja: p.boja,
    izmijenjeno: true,
  });

  /** Crvena napomena: marža korpice kad bi `novi` ušao (umjesto reda `umjesto`, ili kao dodatak). */
  function obaraMarzu(novi: KutijaRed | null, umjesto?: string): string | undefined {
    if (!novi) return undefined;
    const poslije = umjesto ? redovi!.map((x) => (x.productId === umjesto ? novi : x)) : [...redovi!, novi];
    const m = zbir(poslije, cijenaKorpice).marza;
    if (m == null || m >= minMarza) return undefined;
    return z.marza != null && m >= z.marza ? `marža ostaje ispod: ${formatMarza(m)}` : `marža pada na ${formatMarza(m)}`;
  }

  /* ---------- izmjene ---------- */
  function promijeni(productId: string, kolicina: number) {
    setRedovi(
      redovi!.map((r) => {
        if (r.productId !== productId) return r;
        // Ne ispod smislene najmanje količine (ni upisom u polje); red koji je već ispod nje smije ostati gdje je.
        const dno = Math.min(r.kolicina, najmanje(r.productId, r.cijena, r.jedinica));
        return { ...r, kolicina: Math.max(dno, Math.min(kolicina, najvecaKolicina(r, slobodnoKM))) };
      }),
    );
  }

  function ukloni(productId: string) {
    setRedovi(redovi!.filter((r) => r.productId !== productId));
    if (izabran === productId) setIzabran(null);
  }

  /** `nov` ulazi umjesto izabranog reda, po vrijednosti. Sa spiska zamjena: izbačeni zauzima njegovo mjesto. */
  function zamijeniSa(nov: Product, saSpiska: boolean) {
    if (!redIzabran) return;
    const n = zamijeni(redIzabran, nov, slobodnoKM);
    if (!n || n.kolicina < najmanje(nov.id, nov.price, nov.unit)) return;
    setRedovi(redovi!.map((r) => (r.productId === redIzabran.productId ? n : r)));
    setZamjene([...zamjene!.filter((id) => id !== nov.id), ...(saSpiska ? [redIzabran.productId] : [])]);
    setIzabran(null);
  }

  function dodaj(p: Product) {
    const k = kolicinaZaDodavanje(slobodnoKM, p.price, p.unit);
    if (k < najmanje(p.id, p.price, p.unit)) return;
    setRedovi([...redovi!, redOd(p, k)]);
    setZamjene(zamjene!.filter((id) => id !== p.id));
  }

  /** Jedan red desne kolone: u načinu zamjene nudi „Zamijeni", inače „U korpicu". */
  function redPonude(p: Product, saSpiska: boolean) {
    const smije = smijeUKorpicu(p);
    const min = najmanje(p.id, p.price, p.unit);
    const nSvaka = redIzabran && smije ? zamijeni(redIzabran, p, slobodnoKM) : null;
    const n = nSvaka && nSvaka.kolicina >= min ? nSvaka : null;
    const kSvaka = !redIzabran && smije ? kolicinaZaDodavanje(slobodnoKM, p.price, p.unit) : 0;
    const k = kSvaka >= min ? kSvaka : 0;
    const upozori = redIzabran ? obaraMarzu(n, redIzabran.productId) : k > 0 ? obaraMarzu(redOd(p, k)) : undefined;
    return (
      <li
        key={p.id}
        className={cn("flex items-center gap-2.5 rounded-xl border bg-white p-2", upozori ? "border-red-200" : "border-line")}
      >
        <span className="relative h-10 w-10 shrink-0 overflow-hidden rounded-lg" style={{ backgroundColor: p.boja ?? "var(--color-sand)" }}>
          <SmartImage src={p.imageUrl} alt={p.name} sizes="40px" />
        </span>
        <div className="min-w-0 flex-1">
          <p className="truncate text-sm font-medium">{p.name}</p>
          <p className="truncate text-xs text-muted">
            {redIzabran && n
              ? `ulazi ${broj(n.kolicina)} ${n.jedinica} = ${formatPrice(n.kolicina * n.cijena)}`
              : !redIzabran && k > 0
                ? `+${broj(k)} ${p.unit} = ${formatPrice(k * p.price)}`
                : `${formatPrice(p.price)}${unitSuffix(p.unit)} · ${tekstNajmanje(p)}`}
            {p.marza != null && <span className={cn("ml-1.5", TON[tonMarze(p.marza, minMarza)])}>marža {formatMarza(p.marza)}</span>}
          </p>
          {upozori && <p className="truncate text-xs font-medium text-red-700">⚠ {upozori}</p>}
        </div>
        {!samoPregled && smije && redIzabran && (
          <Button
            size="sm"
            onClick={() => zamijeniSa(p, saSpiska)}
            disabled={!n}
            title={n ? undefined : `Ne stane smislena količina (${tekstNajmanje(p)})`}
          >
            <ArrowLeftRight className="h-4 w-4" /> Zamijeni
          </Button>
        )}
        {!samoPregled && smije && !redIzabran && (
          <Button
            size="sm"
            variant="secondary"
            onClick={() => dodaj(p)}
            disabled={k <= 0}
            title={
              k > 0
                ? `Dodaj ${broj(k)} ${p.unit} = ${formatPrice(k * p.price)}`
                : `Nema mjesta za smislenu količinu (${tekstNajmanje(p)} = ${formatPrice(min * p.price)}) — prvo izvadi ili smanji neki artikal`
            }
          >
            <Plus className="h-4 w-4" /> U korpicu
          </Button>
        )}
        {!samoPregled &&
          (saSpiska ? (
            <button
              type="button"
              onClick={() => setZamjene(zamjene!.filter((id) => id !== p.id))}
              aria-label={`Skini ${p.name} sa spiska zamjena`}
              title="Skini sa spiska zamjena"
              className="grid h-8 w-8 shrink-0 place-items-center rounded-full text-muted transition hover:bg-red-50 hover:text-red-600"
            >
              <X className="h-4 w-4" />
            </button>
          ) : (
            <button
              type="button"
              onClick={() => setZamjene([...zamjene!, p.id])}
              aria-label={`Predloži ${p.name} kao zamjenu`}
              title="Stavi na spisak predloženih zamjena (kupci je vide)"
              className="grid h-8 w-8 shrink-0 place-items-center rounded-full text-muted transition hover:bg-honey-100 hover:text-honey-700"
            >
              <Star className="h-4 w-4" />
            </button>
          ))}
      </li>
    );
  }

  const predlozene = zamjene.map((id) => poId.get(id)).filter((p): p is Product => !!p);

  return (
    <AdminLadica naslov={naslov} podnaslov={podnaslov} onClose={onClose} sirina="max-w-5xl" sredina>
      {/* Zbir: uvijek vidljiv, iznad obje kolone. */}
      <div className="border-b border-line bg-sand px-4 py-3 sm:px-6">
        <div className="grid gap-x-8 gap-y-2 sm:grid-cols-2">
          <div>
            <div className="flex items-baseline justify-between gap-3 text-sm">
              <span className="text-muted">Vrijednost sadržaja</span>
              <span className={cn("font-semibold tabular-nums", pct < 95 ? "text-honey-700" : pct > 100 ? "text-red-700" : "text-brand-700")}>
                {formatPrice(z.vrijednost)}
                <span className="font-normal text-muted"> / najviše {formatPrice(cilj)}</span>
              </span>
            </div>
            <div className="mt-1.5 h-1.5 overflow-hidden rounded-full bg-white">
              <div
                className={cn("h-full rounded-full transition-all", pct < 95 ? "bg-honey-500" : pct > 100 ? "bg-red-500" : "bg-brand-500")}
                style={{ width: `${Math.min(100, pct)}%` }}
              />
            </div>
            {!samoPregled && (
              <p className="mt-1 text-xs text-muted">
                {slobodnoKM > 0 ? `Slobodno za dodavanje: ${formatPrice(slobodnoKM)}` : "Korpica je puna — za novo prvo izvadi ili smanji nešto."}
              </p>
            )}
          </div>
          <div>
            <div className="flex items-baseline justify-between gap-3 text-sm">
              <span className="text-muted">Marža (cijena korpice {formatPrice(cijenaKorpice)})</span>
              <span className={cn("font-semibold", TON[tonMarze(z.marza, minMarza)])}>{formatMarza(z.marza)}</span>
            </div>
            <p className="mt-1 text-xs text-muted">
              Nabavno {formatPrice(z.nabavno)} od najviše {formatPrice(sadrzaj.nabavniBudzet)} · najmanja marža {formatMarza(minMarza)}
              {z.bezNabavne > 0 && <span className="text-honey-700"> · {z.bezNabavne} bez nabavne cijene</span>}
            </p>
          </div>
        </div>

        {!eko.uRedu && (
          <div role="alert" className="mt-2 flex gap-2 rounded-lg border border-red-200 bg-red-50 p-2 text-xs text-red-800">
            <TriangleAlert className="mt-0.5 h-4 w-4 shrink-0" />
            <div className="space-y-0.5">
              <p className="font-semibold">
                Korpica nije ekonomična{samoPregled ? "" : objavljen ? " — ovako se ne može sačuvati" : " — ovakva se ne može objaviti"}
              </p>
              {eko.marza && (
                <p>
                  Marža {formatMarza(eko.marza.marza)} je ispod najmanje {formatMarza(eko.marza.minimum)}: nabavno{" "}
                  {formatPrice(eko.marza.nabavno)}, a smije najviše {formatPrice(eko.marza.budzet)} (preko za {formatPrice(eko.marza.preko)}).
                </p>
              )}
              {eko.faliArtikala > 0 && (
                <p>
                  U korpici je {redovi.length}, a najmanje je {sadrzaj.minimalanBrojArtikala} kultura — fali još {eko.faliArtikala}.
                </p>
              )}
              {eko.faliVrijednosti > 0 && najmanjaVrijednost != null && (
                <p>
                  Vrijednost je ispod najmanje {formatPrice(najmanjaVrijednost)} — fali još {formatPrice(eko.faliVrijednosti)}.
                </p>
              )}
            </div>
          </div>
        )}

        {objavljen && (
          <p className="mt-2 flex gap-2 text-xs text-muted">
            <Info className="mt-0.5 h-3.5 w-3.5 shrink-0 text-brand-700" />
            <span>
              Kupci ovaj sadržaj već vide; izmjena važi za nove narudžbe
              {sadrzaj.naruceneKutije > 0 && ` (${sadrzaj.naruceneKutije} već naručenih kutija ostaje kako jeste)`}.
            </span>
          </p>
        )}
      </div>

      <div className="min-h-0 flex-1 overflow-y-auto lg:grid lg:grid-cols-2 lg:overflow-hidden">
        {/* ---------- lijevo: u korpici ---------- */}
        <div className="p-4 sm:p-5 lg:overflow-y-auto">
          <h3 className="mb-2 text-sm font-semibold">
            U korpici <span className="font-normal text-muted">· {redovi.length} artikala</span>
          </h3>
          <ul className="flex flex-col gap-1.5">
            {redovi.length === 0 && (
              <li className="rounded-xl border border-dashed border-line p-4 text-center text-sm text-muted">Korpica je prazna — dodaj artikle sa desne strane.</li>
            )}
            {redovi.map((r) => {
              const bira = izabran === r.productId;
              return (
                <li key={r.productId} className={cn("rounded-xl border bg-white p-2", bira ? "border-brand-500 ring-2 ring-brand-100" : "border-line")}>
                  <div className="flex items-center gap-2.5">
                    <span className="relative h-10 w-10 shrink-0 overflow-hidden rounded-lg" style={{ backgroundColor: r.boja ?? "var(--color-sand)" }}>
                      <SmartImage src={r.imageUrl ?? undefined} alt={r.naziv} sizes="40px" />
                    </span>
                    <div className="min-w-0 flex-1">
                      <p className="truncate text-sm font-medium">
                        {r.naziv}
                        {r.izmijenjeno && <span className="ml-1.5 rounded bg-brand-100 px-1 text-[10px] font-semibold text-brand-700">novo</span>}
                      </p>
                      <p className="text-xs text-muted">
                        {formatPrice(r.cijena)}
                        {unitSuffix(r.jedinica)} · {formatPrice(Math.round(r.kolicina * r.cijena * 100) / 100)}
                      </p>
                    </div>
                    {samoPregled ? (
                      <span className="shrink-0 text-sm font-semibold tabular-nums">
                        {broj(r.kolicina)} {r.jedinica}
                      </span>
                    ) : (
                      <>
                        <Stepper
                          red={r}
                          najvise={najvecaKolicina(r, slobodnoKM)}
                          najmanje={najmanje(r.productId, r.cijena, r.jedinica)}
                          onPromijeni={(k) => promijeni(r.productId, k)}
                        />
                        <button
                          type="button"
                          onClick={() => ukloni(r.productId)}
                          aria-label={`Ukloni ${r.naziv}`}
                          title="Izvadi iz korpice"
                          className="grid h-8 w-8 shrink-0 place-items-center rounded-full text-muted transition hover:bg-red-50 hover:text-red-600"
                        >
                          <X className="h-4 w-4" />
                        </button>
                      </>
                    )}
                  </div>
                  {!samoPregled && (
                    <button
                      type="button"
                      onClick={() => setIzabran(bira ? null : r.productId)}
                      aria-pressed={bira}
                      className={cn(
                        "mt-2 inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-medium transition",
                        bira ? "bg-ink text-white" : "bg-sand text-ink hover:bg-brand-100",
                      )}
                    >
                      <ArrowLeftRight className="h-3.5 w-3.5" />
                      {bira ? "Biraš zamjenu desno — klikni da odustaneš" : "Zamijeni drugim artiklom"}
                    </button>
                  )}
                </li>
              );
            })}
          </ul>
        </div>

        {/* ---------- desno: zamjene i ponuda ---------- */}
        <div className="border-t border-line p-4 sm:p-5 lg:overflow-y-auto lg:border-l lg:border-t-0">
          {redIzabran && (
            <p className="mb-3 rounded-xl bg-brand-100 p-3 text-sm text-brand-700">
              Izlazi <b>{redIzabran.naziv}</b> {broj(redIzabran.kolicina)} {redIzabran.jedinica} ={" "}
              <b>{formatPrice(redIzabran.kolicina * redIzabran.cijena)}</b>. Izaberi šta ulazi umjesto njega — u istoj vrijednosti.
            </p>
          )}

          <h3 className="text-sm font-semibold">
            Predložene zamjene <span className="font-normal text-muted">· {predlozene.length}</span>
          </h3>
          <p className="mb-2 text-xs text-muted">
            Kupac koji neće neki artikal bira jedan od ovih. {!samoPregled && "Odavde ih možeš i ubaciti u korpicu."}
          </p>
          <ul className="flex flex-col gap-1.5">
            {predlozene.length === 0 && (
              <li className="rounded-xl border border-dashed border-line p-3 text-center text-sm text-muted">
                Nema predloženih zamjena{!samoPregled && " — dodaj ih zvjezdicom iz ponude ispod"}.
              </li>
            )}
            {predlozene.map((p) => redPonude(p, true))}
          </ul>

          {!samoPregled && (
            <>
              <h3 className="mb-2 mt-6 text-sm font-semibold">
                Ostala sezonska ponuda <span className="font-normal text-muted">· {ostalo.length}</span>
              </h3>
              <label className="relative mb-2 block">
                <Search className="pointer-events-none absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-muted" />
                <input
                  type="search"
                  value={upit}
                  onChange={(e) => setUpit(e.target.value)}
                  placeholder="Traži artikal…"
                  aria-label="Traži artikal u ponudi"
                  className="h-9 w-full rounded-full border border-line bg-white pl-8 pr-3 text-sm outline-none focus:border-brand-500"
                />
              </label>
              <ul className="flex flex-col gap-1.5">
                {ostalo.length === 0 && <li className="text-sm text-muted">{q ? `Ništa za „${upit}“.` : "Nema drugih artikala u sezoni."}</li>}
                {ostalo.map((p) => redPonude(p, false))}
              </ul>
            </>
          )}
        </div>
      </div>

      <div className="flex gap-3 border-t border-line p-4">
        <Button variant="secondary" className="flex-1" onClick={onClose} type="button">
          {samoPregled ? "Zatvori" : "Odustani"}
        </Button>
        {!samoPregled && (
          <Button className="flex-1" onClick={() => sacuvaj.mutate()} disabled={redovi.length === 0 || sacuvaj.isPending}>
            {sacuvaj.isPending ? "Čuvam…" : "Sačuvaj"}
          </Button>
        )}
      </div>
    </AdminLadica>
  );
}
