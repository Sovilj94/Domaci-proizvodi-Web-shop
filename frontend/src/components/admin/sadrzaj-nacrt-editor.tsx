"use client";

import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Trash2 } from "lucide-react";
import { AdminLadica } from "@/components/admin/admin-ladica";
import { Button } from "@/components/ui/button";
import { Select } from "@/components/ui/field";
import { LoadingBlock } from "@/components/ui/spinner";
import { EmptyState } from "@/components/ui/empty-state";
import { SmartImage } from "@/components/ui/smart-image";
import { useToast } from "@/components/ui/toast";
import { adminApi, adminKorpiceApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import { formatMarza, tonMarze } from "@/lib/marza";
import { cn, formatPrice, unitSuffix } from "@/lib/utils";
import type { Product } from "@/types/domain";

type Red = { productId: string; naziv: string; jedinica: string; imageUrl?: string; kolicina: number };
type Zamjena = { productId: string; naziv: string; jedinica: string; imageUrl?: string };

/** Poruka iz ApiRequestError ako je ima; inače rezerva. */
function poruka(err: unknown, rezerva: string): string {
  return err instanceof ApiRequestError && err.message.trim() ? err.message : rezerva;
}

/**
 * Korak za unos količine, po jedinici — isto pravilo kao generator na backendu
 * (KoracZaokruzivanja u GenerisiSedmicniSadrzajHandler): "kom" se ne djeli (nema
 * 0,3 jaja), kg/l mogu biti frakcioni. Ranije je cijeli editor koristio step="0.1"
 * za sve jedinice, pa je "0,3 kom" bilo moguće unijeti i sačuvati.
 */
function korakKolicine(jedinica: string): number {
  return jedinica === "kom" ? 1 : 0.1;
}

const TON = {
  nema: "text-muted",
  losa: "text-red-700",
  tijesna: "text-honey-700",
  dobra: "text-brand-700",
} as const;

/**
 * Bočni panel za uređivanje nacrta — isti obrazac kao `ProductForm` (klizi sa desna,
 * radni alat koji ostaje otvoren uz ostatak ekrana), ne centrirani dijalog kao
 * `SadrzajModal`, koji je samo za pregled.
 *
 * `PUT /admin/korpice/sadrzaj/{id}` zamjenjuje CIJELI sadržaj (i spisak zamjena) onim što se
 * pošalje — nema parcijalnog "dodaj/ukloni" na serveru. Zato:
 *   1) nacrt se učitava sa `adminKorpiceApi.sadrzaj(id)` (javni endpoint nacrt odbija)
 *      i tek TADA se lokalni `redovi` popune — prije toga su `null`, ne prazan niz;
 *   2) dugme "Sačuvaj" je onemogućeno dok `redovi` nisu učitani (`spreman === false`),
 *      pa se nikad ne može poslati prazna lista preko postojećeg nacrta samo zato što
 *      je zahtjev za učitavanjem još u letu.
 *
 * Objavljen ili zaključan sadržaj se otvara samo za pregled (`samoPregled`) — da se vidi
 * marža i šta je kupcima ponuđeno kao zamjena.
 *
 * Cijene i nabavne cijene se čitaju iz ADMIN liste proizvoda (javni katalog nabavnu nema),
 * pa su vrijednost i marža procjena po trenutnom katalogu dok operater mijenja količine.
 */
export function SadrzajNacrtEditor({
  sadrzajId,
  korpicaNaziv,
  cilj,
  samoPregled = false,
  onClose,
}: {
  sadrzajId: string;
  korpicaNaziv: string;
  /**
   * Ciljna vrijednost iz pregleda sedmice (`ciljGenerisanja`), NE `SadrzajKorpice.ciljnaVrijednost`
   * — potonje polje backend upisuje tek pri objavi (`ObjaviSadrzaj`), pa je za nacrt uvijek 0.
   */
  cilj: number;
  samoPregled?: boolean;
  onClose: () => void;
}) {
  const qc = useQueryClient();
  const { show } = useToast();
  const [redovi, setRedovi] = useState<Red[] | null>(null);
  const [zamjene, setZamjene] = useState<Zamjena[] | null>(null);
  const [dodajId, setDodajId] = useState("");
  const [dodajZamjenuId, setDodajZamjenuId] = useState("");

  const sadrzajKljuc = ["admin", "korpice", "sadrzaj", sadrzajId] as const;

  const {
    data: sadrzaj,
    isLoading,
    isError,
  } = useQuery({
    queryKey: sadrzajKljuc,
    queryFn: () => adminKorpiceApi.sadrzaj(sadrzajId),
  });

  const { data: sviArtikli } = useQuery({
    queryKey: ["admin", "products"],
    queryFn: () => adminApi.products(),
  });
  // U kutiju i u zamjene idu samo dostupni artikli označeni „može u korpicu" (korpica nikad nije takva).
  const artikli = sviArtikli?.filter((a) => a.isAvailable && a.mozeUKorpicu);

  // Nacrt se u lokalni state popuni TAČNO JEDNOM. `redovi !== null` poslije toga znači
  // "korisnik uređuje svoju lokalnu kopiju" — mora razlikovati "još nije učitano" od
  // "učitano, pa isčišćeno do praznog", inače bi refetch (npr. poslije invalidacije sa
  // nekog drugog panela) pregazio izmjene koje korisnik još nije sačuvao.
  useEffect(() => {
    if (!sadrzaj || redovi !== null) return;
    setRedovi(
      sadrzaj.stavke.map((s) => ({
        productId: s.productId,
        naziv: s.naziv,
        jedinica: s.jedinica,
        imageUrl: s.imageUrl,
        kolicina: s.kolicina,
      })),
    );
    setZamjene(
      sadrzaj.zamjene.map((z) => ({
        productId: z.productId,
        naziv: z.naziv,
        jedinica: z.jedinica,
        imageUrl: z.imageUrl,
      })),
    );
  }, [sadrzaj, redovi]);

  const spreman = redovi !== null && zamjene !== null;

  const sacuvaj = useMutation({
    mutationFn: () => {
      // Odbrana u dubini: i dugme je onemogućeno dok `spreman` nije tačno, ali
      // mutationFn ne vjeruje samo UI-u — baci ako bi se ikad pozvala prije učitavanja.
      if (!redovi || !zamjene) throw new Error("Nacrt još nije učitan.");
      return adminKorpiceApi.izmijeni(sadrzajId, {
        stavke: redovi.map((r) => ({ productId: r.productId, kolicina: r.kolicina })),
        zamjene: zamjene.map((z) => z.productId),
      });
    },
    onSuccess: () => {
      // Nacrt editor ne zna i ne treba da zna za koju sedmicu/godinu — pregled sedmice
      // keširan pod bilo kojim godina/sedmica ključem treba da pokaže novi broj
      // artikala i vrijednost, pa invalidacija ide po prefiksu umjesto po jednom ključu.
      qc.invalidateQueries({ queryKey: ["admin", "korpice", "pregled"] });
      qc.invalidateQueries({ queryKey: sadrzajKljuc });
      show("Nacrt sačuvan");
      onClose();
    },
    onError: (err) => show(poruka(err, "Čuvanje nije uspjelo"), "error"),
  });

  function ukloni(productId: string) {
    setRedovi((red) => red && red.filter((r) => r.productId !== productId));
  }

  function izmijeniKolicinu(productId: string, kolicina: number) {
    setRedovi((red) => red && red.map((r) => (r.productId === productId ? { ...r, kolicina } : r)));
  }

  function dodajArtikal(id: string) {
    const a = artikli?.find((x) => x.id === id);
    setDodajId("");
    if (!a) return;
    setRedovi((red) =>
      red
        ? [...red, { productId: a.id, naziv: a.name, jedinica: a.unit, imageUrl: a.imageUrl, kolicina: 1 }]
        : red,
    );
    // Artikal koji uđe u kutiju više nije zamjena (isto pravilo kao server).
    setZamjene((z) => z && z.filter((x) => x.productId !== id));
  }

  function dodajZamjenu(id: string) {
    const a = artikli?.find((x) => x.id === id);
    setDodajZamjenuId("");
    if (!a) return;
    setZamjene((z) =>
      z ? [...z, { productId: a.id, naziv: a.name, jedinica: a.unit, imageUrl: a.imageUrl }] : z,
    );
  }

  const zauzeti = new Set([...(redovi ?? []), ...(zamjene ?? [])].map((r) => r.productId));
  const slobodniArtikli = (artikli ?? []).filter((a) => !zauzeti.has(a.id));
  const poId = new Map<string, Product>((sviArtikli ?? []).map((a) => [a.id, a]));

  const ukupno = (redovi ?? []).reduce((z, r) => z + r.kolicina * (poId.get(r.productId)?.price ?? 0), 0);
  const nabavno = (redovi ?? []).reduce(
    (z, r) => z + r.kolicina * (poId.get(r.productId)?.nabavnaCijena ?? 0),
    0,
  );
  const bezNabavne = (redovi ?? []).filter((r) => poId.get(r.productId)?.nabavnaCijena == null).length;
  const cijenaKorpice = sadrzaj?.cijenaKorpice ?? 0;
  const marzaKorpice =
    cijenaKorpice > 0 && (redovi?.length ?? 0) > bezNabavne
      ? (cijenaKorpice - nabavno) / cijenaKorpice
      : null;
  const ciljPrikaz = samoPregled && sadrzaj?.ciljnaVrijednost ? sadrzaj.ciljnaVrijednost : cilj;

  return (
    <AdminLadica
      naslov={samoPregled ? "Sadržaj korpice" : "Izmjena nacrta"}
      podnaslov={korpicaNaziv}
      onClose={onClose}
    >
      <div className="flex-1 overflow-y-auto p-6">
        {isLoading || !spreman ? (
          isError ? (
            <EmptyState title="Nacrt nije učitan" description="Pokušaj ponovo malo kasnije." />
          ) : (
            <LoadingBlock label="Učitavanje…" />
          )
        ) : (
          <>
            {/* Dok admin lista proizvoda nije stigla, zbirovi bi računali sa cijenom 0. */}
            {sviArtikli && sadrzaj ? (
              <dl className="mb-5 grid grid-cols-2 gap-2 text-sm">
                <div className="rounded-xl bg-sand px-3 py-2">
                  <dt className="text-xs text-muted">Vrijednost / cilj</dt>
                  <dd className={cn("font-semibold", ukupno > ciljPrikaz * 1.05 ? "text-red-700" : "text-ink")}>
                    {formatPrice(ukupno)} / {formatPrice(ciljPrikaz)}
                  </dd>
                </div>
                <div className="rounded-xl bg-sand px-3 py-2">
                  <dt className="text-xs text-muted">Nabavno / budžet</dt>
                  <dd className={cn("font-semibold", nabavno > sadrzaj.nabavniBudzet ? "text-red-700" : "text-ink")}>
                    {formatPrice(nabavno)} / {formatPrice(sadrzaj.nabavniBudzet)}
                  </dd>
                </div>
                <div className="col-span-2 rounded-xl bg-sand px-3 py-2">
                  <dt className="text-xs text-muted">
                    Marža korpice (cijena {formatPrice(cijenaKorpice)}, minimum {formatMarza(sadrzaj.minimalnaMarza)})
                  </dt>
                  <dd className={cn("font-semibold", TON[tonMarze(marzaKorpice, sadrzaj.minimalnaMarza)])}>
                    {formatMarza(marzaKorpice)}
                    {bezNabavne > 0 && (
                      <span className="ml-2 text-xs font-normal text-honey-700">
                        {bezNabavne} bez nabavne cijene — nije uračunato
                      </span>
                    )}
                  </dd>
                </div>
              </dl>
            ) : (
              <p className="mb-4 text-sm text-muted">Računam vrijednost i maržu…</p>
            )}

            <h3 className="mb-2 text-sm font-semibold text-ink">U kutiji</h3>
            {redovi!.length === 0 ? (
              <p className="mb-4 text-sm text-muted">Nacrt je trenutno prazan — dodajte artikle ispod.</p>
            ) : (
              <ul className="flex flex-col gap-2">
                {redovi!.map((r) => {
                  const p = poId.get(r.productId);
                  return (
                    <li
                      key={r.productId}
                      className="flex items-center gap-3 rounded-card border border-line bg-surface p-3"
                    >
                      <div className="relative h-12 w-12 shrink-0 overflow-hidden rounded-xl bg-sand">
                        <SmartImage src={r.imageUrl} alt={r.naziv} sizes="48px" />
                      </div>
                      <div className="min-w-0 flex-1">
                        <p className="truncate text-sm font-medium text-ink">{r.naziv}</p>
                        <p className="text-xs text-muted">
                          {p ? formatPrice(p.price) : "—"}
                          {unitSuffix(r.jedinica)}
                          {" · "}
                          {p?.nabavnaCijena != null ? `nabavna ${formatPrice(p.nabavnaCijena)}` : "bez nabavne"}
                        </p>
                      </div>
                      {samoPregled ? (
                        <span className="shrink-0 text-sm font-semibold tabular-nums">
                          {r.kolicina} {r.jedinica}
                        </span>
                      ) : (
                        <>
                          <input
                            type="number"
                            step={korakKolicine(r.jedinica)}
                            min={korakKolicine(r.jedinica)}
                            value={r.kolicina}
                            onChange={(e) => izmijeniKolicinu(r.productId, Number(e.target.value))}
                            className="w-20 rounded-xl border border-line bg-sand px-2 py-1.5 text-right text-sm text-ink focus:border-brand-400 focus:bg-white focus:outline-none focus:ring-2 focus:ring-brand-400"
                            aria-label={`Količina za ${r.naziv}`}
                          />
                          <button
                            type="button"
                            onClick={() => ukloni(r.productId)}
                            aria-label={`Ukloni ${r.naziv}`}
                            className="grid h-9 w-9 shrink-0 place-items-center rounded-full text-muted transition hover:bg-red-50 hover:text-red-600"
                          >
                            <Trash2 className="h-4 w-4" />
                          </button>
                        </>
                      )}
                    </li>
                  );
                })}
              </ul>
            )}

            {!samoPregled && (
              <Select className="mt-3" value={dodajId} onChange={(e) => dodajArtikal(e.target.value)}>
                <option value="">+ Dodaj artikal u kutiju…</option>
                {slobodniArtikli.map((a) => (
                  <option key={a.id} value={a.id}>
                    {a.name} — {formatPrice(a.price)}
                    {unitSuffix(a.unit)}
                    {a.marza != null ? ` · marža ${formatMarza(a.marza)}` : ""}
                  </option>
                ))}
              </Select>
            )}

            <h3 className="mb-1 mt-6 text-sm font-semibold text-ink">Predložene zamjene</h3>
            <p className="mb-2 text-xs text-muted">
              Kupac koji neće neki artikal iz kutije bira jedan od ovih; količinu server računa tako da
              vrijednost kutije ostane ista, a zamjena koja bi oborila maržu se odbija.
            </p>
            {zamjene!.length === 0 ? (
              <p className="text-sm text-muted">Nema predloženih zamjena.</p>
            ) : (
              <ul className="flex flex-wrap gap-2">
                {zamjene!.map((z) => {
                  const p = poId.get(z.productId);
                  return (
                    <li
                      key={z.productId}
                      className="flex items-center gap-2 rounded-full border border-line bg-surface py-1 pl-3 pr-1 text-sm"
                    >
                      <span>
                        {z.naziv}
                        <span className="ml-1 text-xs text-muted">
                          {p ? `${formatPrice(p.price)}${unitSuffix(z.jedinica)}` : ""}
                          {p?.marza != null ? ` · ${formatMarza(p.marza)}` : ""}
                        </span>
                      </span>
                      {!samoPregled && (
                        <button
                          type="button"
                          onClick={() => setZamjene((zz) => zz && zz.filter((x) => x.productId !== z.productId))}
                          aria-label={`Ukloni zamjenu ${z.naziv}`}
                          className="grid h-7 w-7 place-items-center rounded-full text-muted hover:bg-red-50 hover:text-red-600"
                        >
                          <Trash2 className="h-3.5 w-3.5" />
                        </button>
                      )}
                    </li>
                  );
                })}
              </ul>
            )}
            {!samoPregled && (
              <Select className="mt-3" value={dodajZamjenuId} onChange={(e) => dodajZamjenu(e.target.value)}>
                <option value="">+ Dodaj zamjenu…</option>
                {slobodniArtikli.map((a) => (
                  <option key={a.id} value={a.id}>
                    {a.name} — {formatPrice(a.price)}
                    {unitSuffix(a.unit)}
                    {a.marza != null ? ` · marža ${formatMarza(a.marza)}` : ""}
                  </option>
                ))}
              </Select>
            )}
          </>
        )}
      </div>

      <div className="flex gap-3 border-t border-line p-4">
        <Button variant="secondary" className="flex-1" onClick={onClose} type="button">
          {samoPregled ? "Zatvori" : "Odustani"}
        </Button>
        {!samoPregled && (
          <Button
            className="flex-1"
            onClick={() => sacuvaj.mutate()}
            disabled={!spreman || redovi!.length === 0 || sacuvaj.isPending}
          >
            {sacuvaj.isPending ? "Čuvam…" : "Sačuvaj"}
          </Button>
        )}
      </div>
    </AdminLadica>
  );
}
