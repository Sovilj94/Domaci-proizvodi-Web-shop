"use client";

import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Trash2, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Select } from "@/components/ui/field";
import { LoadingBlock } from "@/components/ui/spinner";
import { EmptyState } from "@/components/ui/empty-state";
import { SmartImage } from "@/components/ui/smart-image";
import { useToast } from "@/components/ui/toast";
import { adminKorpiceApi, catalogApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import { formatPrice, unitSuffix } from "@/lib/utils";

type Red = { productId: string; naziv: string; jedinica: string; imageUrl?: string; kolicina: number };

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

/**
 * Bočni panel za uređivanje nacrta — isti obrazac kao `ProductForm` (klizi sa desna,
 * radni alat koji ostaje otvoren uz ostatak ekrana), ne centrirani dijalog kao
 * `SadrzajModal`, koji je samo za pregled.
 *
 * `PUT /admin/korpice/sadrzaj/{id}` zamjenjuje CIJELI sadržaj onim što se pošalje —
 * nema parcijalnog "dodaj/ukloni" na serveru. Zato:
 *   1) nacrt se učitava sa `adminKorpiceApi.sadrzaj(id)` (javni endpoint nacrt odbija)
 *      i tek TADA se lokalni `redovi` popune — prije toga su `null`, ne prazan niz;
 *   2) dugme "Sačuvaj" je onemogućeno dok `redovi` nisu učitani (`spreman === false`),
 *      pa se nikad ne može poslati prazna lista preko postojećeg nacrta samo zato što
 *      je zahtjev za učitavanjem još u letu.
 */
export function SadrzajNacrtEditor({
  sadrzajId,
  korpicaNaziv,
  cilj,
  onClose,
}: {
  sadrzajId: string;
  korpicaNaziv: string;
  /**
   * Ciljna vrijednost iz pregleda sedmice (`ciljGenerisanja`), NE `SadrzajKorpice.ciljnaVrijednost`
   * — potonje polje backend upisuje tek pri objavi (`ObjaviSadrzaj`), pa je za nacrt uvijek 0.
   * Editor se otvara samo nad nacrtima, pa je ciljGenerisanja iz reda sedmice jedini smislen broj.
   */
  cilj: number;
  onClose: () => void;
}) {
  const qc = useQueryClient();
  const { show } = useToast();
  const [redovi, setRedovi] = useState<Red[] | null>(null);
  const [dodajId, setDodajId] = useState("");

  const sadrzajKljuc = ["admin", "korpice", "sadrzaj", sadrzajId] as const;

  const {
    data: sadrzaj,
    isLoading,
    isError,
  } = useQuery({
    queryKey: sadrzajKljuc,
    queryFn: () => adminKorpiceApi.sadrzaj(sadrzajId),
  });

  const { data: artikli } = useQuery({
    queryKey: ["products", "svi"],
    queryFn: () => catalogApi.products({}),
  });

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
  }, [sadrzaj, redovi]);

  const spreman = redovi !== null;

  const sacuvaj = useMutation({
    mutationFn: () => {
      // Odbrana u dubini: i dugme je onemogućeno dok `spreman` nije tačno, ali
      // mutationFn ne vjeruje samo UI-u — baci ako bi se ikad pozvala prije učitavanja.
      if (!redovi) throw new Error("Nacrt još nije učitan.");
      return adminKorpiceApi.izmijeni(sadrzajId, {
        stavke: redovi.map((r) => ({ productId: r.productId, kolicina: r.kolicina })),
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
  }

  const slobodniArtikli = (artikli ?? []).filter(
    (a) => !redovi?.some((r) => r.productId === a.id),
  );

  // Trenutna vrijednost nacrta je procjena iz TRENUTNIH cijena kataloga (SadrzajDto nosi
  // samo količinu, ne cijenu po stavci) — operater je koristi da vidi koliko je nacrt
  // udaljen od cilja dok mijenja količine, ne kao obračunsku vrijednost narudžbe.
  const ukupno = (redovi ?? []).reduce((zbir, r) => {
    const cijena = artikli?.find((a) => a.id === r.productId)?.price ?? 0;
    return zbir + r.kolicina * cijena;
  }, 0);

  return (
    <div className="fixed inset-0 z-50">
      <button
        type="button"
        aria-label="Zatvori"
        onClick={onClose}
        className="absolute inset-0 bg-black/40"
      />
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="nacrt-editor-naslov"
        className="absolute right-0 top-0 flex h-full w-full max-w-lg flex-col bg-cream shadow-xl"
      >
        <div className="flex items-start justify-between gap-4 border-b border-line px-6 py-4">
          <div>
            <h2 id="nacrt-editor-naslov" className="font-display text-lg font-bold text-ink">
              Izmjena nacrta
            </h2>
            <p className="text-sm text-muted">{korpicaNaziv}</p>
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label="Zatvori"
            className="shrink-0 rounded-full p-1.5 text-muted transition hover:bg-sand"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        <div className="flex-1 overflow-y-auto p-6">
          {isLoading || redovi === null ? (
            <LoadingBlock label="Učitavanje nacrta…" />
          ) : isError ? (
            <EmptyState
              title="Nacrt nije učitan"
              description="Pokušajte ponovo malo kasnije."
            />
          ) : (
            <>
              {/* Dok katalog (artikli) nije stigao, `ukupno` računa sa cijenom 0 za svaki
                  red (vidi reduce ispod) — bez ovog gejta bi operater nakratko vidio
                  "~0 KM", baš onaj broj radi kojeg editor postoji. */}
              {artikli ? (
                <p className="mb-4 text-sm">
                  Vrijednost nacrta:{" "}
                  <strong className={ukupno > cilj * 1.05 ? "text-red-700" : "text-ink"}>
                    {formatPrice(ukupno)}
                  </strong>{" "}
                  <span className="text-muted">/ cilj {formatPrice(cilj)}</span>
                </p>
              ) : (
                <p className="mb-4 text-sm text-muted">Računam vrijednost nacrta…</p>
              )}

              {redovi.length === 0 ? (
                <p className="mb-4 text-sm text-muted">
                  Nacrt je trenutno prazan — dodajte artikle ispod.
                </p>
              ) : (
                <ul className="flex flex-col gap-2">
                  {redovi.map((r) => (
                    <li
                      key={r.productId}
                      className="flex items-center gap-3 rounded-card border border-line bg-surface p-3"
                    >
                      <div className="relative h-12 w-12 shrink-0 overflow-hidden rounded-xl bg-sand">
                        <SmartImage src={r.imageUrl} alt={r.naziv} sizes="48px" />
                      </div>
                      <div className="min-w-0 flex-1">
                        <p className="truncate text-sm font-medium text-ink">{r.naziv}</p>
                        <p className="text-xs text-muted">{unitSuffix(r.jedinica)}</p>
                      </div>
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
                    </li>
                  ))}
                </ul>
              )}

              <Select
                className="mt-4"
                value={dodajId}
                onChange={(e) => dodajArtikal(e.target.value)}
              >
                <option value="">+ Dodaj artikal…</option>
                {slobodniArtikli.map((a) => (
                  <option key={a.id} value={a.id}>
                    {a.name} — {formatPrice(a.price)}
                    {unitSuffix(a.unit)}
                  </option>
                ))}
              </Select>
            </>
          )}
        </div>

        <div className="flex gap-3 border-t border-line p-4">
          <Button variant="secondary" className="flex-1" onClick={onClose} type="button">
            Odustani
          </Button>
          <Button
            className="flex-1"
            onClick={() => sacuvaj.mutate()}
            disabled={!spreman || redovi.length === 0 || sacuvaj.isPending}
          >
            {sacuvaj.isPending ? "Čuvam…" : "Sačuvaj"}
          </Button>
        </div>
      </div>
    </div>
  );
}
