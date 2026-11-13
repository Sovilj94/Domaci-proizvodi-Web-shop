"use client";

import { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ArrowLeftRight, Info, X } from "lucide-react";
import { AdminLadica } from "@/components/admin/admin-ladica";
import { KutijaEditor, Plocice } from "@/components/admin/kutija-editor";
import { Button } from "@/components/ui/button";
import { LoadingBlock } from "@/components/ui/spinner";
import { EmptyState } from "@/components/ui/empty-state";
import { useToast } from "@/components/ui/toast";
import { adminApi, adminKorpiceApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import {
  type KutijaRed,
  granicaKutije,
  mjesecSedmice,
  sezonskaPonuda,
  slobodnoZaDodavanje,
  zamijeni,
  zbir,
} from "@/lib/admin/kutija";
import { formatMarza } from "@/lib/marza";
import { cn, formatPrice, unitSuffix } from "@/lib/utils";
import type { Product } from "@/types/domain";

/** Poruka iz ApiRequestError ako je ima; inače rezerva. */
function poruka(err: unknown, rezerva: string): string {
  return err instanceof ApiRequestError && err.message.trim() ? err.message : rezerva;
}

/** Kupčev budžet ide od 95 % objavljene vrijednosti (server: IzmijeniMojuKorpicuHandler.DonjaGranica) — tekst uz granicu koju šalje server. */
const DONJA_GRANICA_OBJAVLJENOG = 0.95;

/**
 * Bočni panel za sedmični sadržaj korpice. Kutija se uređuje zajedničkim `KutijaEditor`-om
 * (+/−, zamjena iste vrijednosti, dodavanje klikom na pločicu sezonske ponude), a ispod su
 * predložene zamjene koje kupci vide — i one se dodaju klikom.
 *
 * Šta se smije zavisi od statusa koji vrati server, ne od onoga ko je panel otvorio:
 * - **Nacrt** — slobodno do cilja generisanja; na pravila (marža, broj kultura) editor upozorava,
 *   a zaustavlja ih tek objava.
 * - **Objavljen** — mijenja se, ali kutija mora ostati ekonomična i u 95–100 % vrijednosti sa
 *   kojom je objavljena (to je budžet kupaca); artikal koji ostaje računa se po cijeni
 *   snimljenoj pri objavi. Već naručene kutije se ne mijenjaju.
 * - **Zaključan** — samo pregled.
 *
 * `PUT /admin/korpice/sadrzaj/{id}` zamjenjuje CIJELI sadržaj (i spisak zamjena) onim što se
 * pošalje, pa se lokalni redovi popune tek kad sadržaj stigne (prije toga su `null`, ne prazan
 * niz) i „Sačuvaj" je do tada onemogućen — nikad se ne šalje prazna lista.
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
  /** Predložena zamjena za koju admin upravo bira umjesto čega ulazi u korpicu. */
  const [ubacuje, setUbacuje] = useState<string | null>(null);
  /** Vrijednost sa kojom je kutija otvorena — granica je cilj ili ona, šta je veće (kao u KutijaEditor). */
  const [pocetna, setPocetna] = useState(0);

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
    setPocetna(
      Math.round(
        sadrzaj.stavke.reduce((z, s) => {
          const c = cijene.get(s.productId);
          const p = poId.get(s.productId);
          return z + s.kolicina * (snimak ? (c?.jedinicnaCijena ?? p?.price ?? 0) : (p?.price ?? c?.jedinicnaCijena ?? 0));
        }, 0) * 100,
      ) / 100,
    );
    setRedovi(
      sadrzaj.stavke.map((s) => {
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
      }),
    );
    setZamjene(sadrzaj.zamjene.map((z) => z.productId));
  }, [sadrzaj, sviArtikli, redovi, poId]);

  const spreman = redovi !== null && zamjene !== null;

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
  const ponuda = useMemo(() => {
    if (!sadrzaj || !sviArtikli) return [];
    const sezona = sezonskaPonuda(sviArtikli, mjesecSedmice(sadrzaj.godina, sadrzaj.sedmica));
    const kategorije = new Set(sadrzaj.kategorijeIds);
    return kategorije.size > 0 ? sezona.filter((p) => kategorije.has(p.categoryId)) : sezona;
  }, [sadrzaj, sviArtikli]);
  // Nacrt se puni do cilja generisanja; objavljen i zaključan do vrijednosti sa kojom su objavljeni.
  const cilj = sadrzaj ? (sadrzaj.status === "Nacrt" || !sadrzaj.ciljnaVrijednost ? sadrzaj.ciljGenerisanja : sadrzaj.ciljnaVrijednost) : 0;
  /** Smije li artikal u ovu korpicu (kategorije vrste korpice) — zamjena van njih ostaje samo kupcu na izbor. */
  const smijeUKorpicu = (p: Product) => !sadrzaj || sadrzaj.kategorijeIds.length === 0 || sadrzaj.kategorijeIds.includes(p.categoryId);
  const zauzeti = new Set([...(redovi ?? []).map((r) => r.productId), ...(zamjene ?? [])]);
  const slobodnoKM = slobodnoZaDodavanje(granicaKutije(cilj, pocetna), zbir(redovi ?? [], sadrzaj?.cijenaKorpice ?? 0).vrijednost);

  /**
   * Predložena zamjena ulazi u korpicu umjesto reda `umjesto` (po vrijednosti, kao ⇄), a
   * izbačeni artikal odlazi na spisak predloženih zamjena — glavna ponuda i zamjene mijenjaju mjesta.
   */
  function ubaciUmjesto(nov: Product, umjesto: KutijaRed) {
    const n = zamijeni(umjesto, nov, slobodnoKM);
    if (!n || !redovi) return;
    setRedovi(redovi.map((r) => (r.productId === umjesto.productId ? n : r)));
    setZamjene((z) => z && [...z.filter((id) => id !== nov.id), umjesto.productId]);
    setUbacuje(null);
  }

  return (
    <AdminLadica
      naslov={samoPregled ? "Sadržaj korpice" : objavljen ? "Izmjena objavljenog sadržaja" : "Izmjena nacrta"}
      podnaslov={sadrzaj ? `${korpicaNaziv} · sedmica ${sadrzaj.sedmica}` : korpicaNaziv}
      onClose={onClose}
      sirina="max-w-2xl"
    >
      <div className="flex-1 overflow-y-auto p-4 sm:p-5">
        {isLoading || !spreman ? (
          isError ? (
            <EmptyState title="Sadržaj nije učitan" description="Pokušaj ponovo malo kasnije." />
          ) : (
            <LoadingBlock label="Učitavanje…" />
          )
        ) : (
          <>
            {objavljen && (
              <p className="mb-3 flex gap-2 rounded-xl border border-line bg-white p-3 text-xs text-muted">
                <Info className="mt-0.5 h-4 w-4 shrink-0 text-brand-700" />
                <span>
                  Kupci ovaj sadržaj već vide. Izmjena važi za <b className="text-ink">nove narudžbe</b>
                  {sadrzaj!.naruceneKutije > 0 ? (
                    <>
                      {" "}
                      — <b className="text-ink">{sadrzaj!.naruceneKutije}</b> već naručenih kutija ostaje sa dosadašnjim sadržajem
                      (pojedinačno se mijenjaju u Narudžbama)
                    </>
                  ) : (
                    " (za ovu sedmicu još nema naručenih kutija)"
                  )}
                  . Kutija mora ostati u 95–100 % vrijednosti sa kojom je objavljena i iznad najmanje marže.
                </span>
              </p>
            )}

            <KutijaEditor
              redovi={redovi!}
              onChange={(novi) => {
                setRedovi(novi);
                // Artikal koji uđe u kutiju više nije zamjena (isto pravilo kao server).
                setZamjene((z) => z && z.filter((id) => !novi.some((r) => r.productId === id)));
              }}
              cilj={cilj}
              cijenaKorpice={sadrzaj!.cijenaKorpice}
              minimalnaMarza={sadrzaj!.minimalnaMarza}
              minimalnoArtikala={sadrzaj!.minimalanBrojArtikala}
              najmanjaVrijednost={
                objavljen && sadrzaj!.ciljnaVrijednost > 0
                  ? Math.round(sadrzaj!.ciljnaVrijednost * DONJA_GRANICA_OBJAVLJENOG * 100) / 100
                  : undefined
              }
              posljedica={samoPregled ? undefined : objavljen ? "ovako se ne može sačuvati" : "ovakva se ne može objaviti"}
              ponuda={ponuda}
              preporucene={new Set(zamjene!)}
              samoPregled={samoPregled}
            />

            <h3 className="mb-1 mt-6 text-sm font-semibold">Predložene zamjene</h3>
            <p className="mb-2 text-xs text-muted">
              Kupac koji neće neki artikal bira jedan od ovih (ili „izaberi umjesto mene“); količinu računa server.
              {!samoPregled && " Klikni ⇄ na zamjeni da je ubaciš u korpicu umjesto nekog artikla — taj artikal tada postaje zamjena."}
            </p>
            <div className="flex flex-wrap gap-1.5">
              {zamjene!.length === 0 && <p className="text-sm text-muted">Nema predloženih zamjena.</p>}
              {zamjene!.map((id) => {
                const p = poId.get(id);
                const z = sadrzaj!.zamjene.find((x) => x.productId === id);
                const naziv = p?.name ?? z?.naziv ?? id;
                return (
                  <span
                    key={id}
                    className={cn(
                      "inline-flex items-center gap-1 rounded-full border bg-white py-1 pl-3 pr-1 text-sm",
                      ubacuje === id ? "border-brand-500" : "border-line",
                    )}
                  >
                    {naziv}
                    <span className="text-xs text-muted">
                      {p ? `${formatPrice(p.price)}${unitSuffix(p.unit)}` : ""}
                      {p?.marza != null ? ` · ${formatMarza(p.marza)}` : ""}
                    </span>
                    {!samoPregled && p && smijeUKorpicu(p) && (
                      <button
                        type="button"
                        onClick={() => setUbacuje(ubacuje === id ? null : id)}
                        aria-label={`Ubaci ${naziv} u korpicu`}
                        title="Ubaci u korpicu umjesto nekog artikla"
                        className={cn(
                          "grid h-6 w-6 place-items-center rounded-full",
                          ubacuje === id ? "bg-ink text-white" : "text-muted hover:bg-sand hover:text-ink",
                        )}
                      >
                        <ArrowLeftRight className="h-3.5 w-3.5" />
                      </button>
                    )}
                    {!samoPregled && (
                      <button
                        type="button"
                        onClick={() => setZamjene((zz) => zz && zz.filter((x) => x !== id))}
                        aria-label={`Ukloni zamjenu ${naziv}`}
                        className="grid h-6 w-6 place-items-center rounded-full text-muted hover:bg-red-50 hover:text-red-600"
                      >
                        <X className="h-3.5 w-3.5" />
                      </button>
                    )}
                  </span>
                );
              })}
            </div>
            {!samoPregled && ubacuje && poId.get(ubacuje) && (
              <div className="mt-3 rounded-xl border border-brand-400 bg-white p-3">
                <p className="mb-2 text-xs text-muted">
                  <b className="text-ink">{poId.get(ubacuje)!.name}</b> ulazi u korpicu — umjesto čega?
                </p>
                <div className="grid grid-cols-1 gap-1.5 sm:grid-cols-2">
                  {redovi!.map((r) => {
                    const n = zamijeni(r, poId.get(ubacuje)!, slobodnoKM);
                    return (
                      <button
                        key={r.productId}
                        type="button"
                        disabled={!n}
                        onClick={() => ubaciUmjesto(poId.get(ubacuje)!, r)}
                        className="rounded-lg border border-line p-2 text-left text-xs transition hover:border-brand-500 disabled:cursor-not-allowed disabled:opacity-45"
                      >
                        <span className="block truncate font-medium">
                          umjesto: {r.naziv} {r.kolicina.toLocaleString("sr-Latn-BA")} {r.jedinica}
                        </span>
                        <span className="block truncate text-muted">
                          {n
                            ? `ulazi ${n.kolicina.toLocaleString("sr-Latn-BA")} ${n.jedinica} = ${formatPrice(n.kolicina * n.cijena)}`
                            : "ne stane"}
                        </span>
                      </button>
                    );
                  })}
                </div>
              </div>
            )}
            {!samoPregled && (
              <div className="mt-3">
                <p className="mb-1.5 text-xs font-semibold">Dodaj na spisak zamjena (ne ulazi u korpicu)</p>
                <Plocice
                  ponuda={ponuda.filter((p) => !zauzeti.has(p.id))}
                  najvise={9}
                  onIzaberi={(p) => setZamjene((z) => (z ? [...z, p.id] : z))}
                />
              </div>
            )}
          </>
        )}
      </div>

      <div className="flex gap-3 border-t border-line p-4">
        <Button variant="secondary" className="flex-1" onClick={onClose} type="button">
          {samoPregled ? "Zatvori" : "Odustani"}
        </Button>
        {!samoPregled && (
          <Button className="flex-1" onClick={() => sacuvaj.mutate()} disabled={!spreman || redovi!.length === 0 || sacuvaj.isPending}>
            {sacuvaj.isPending ? "Čuvam…" : "Sačuvaj"}
          </Button>
        )}
      </div>
    </AdminLadica>
  );
}
