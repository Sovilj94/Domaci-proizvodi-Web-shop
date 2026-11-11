"use client";

import { useCallback, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Plus } from "lucide-react";
import { NaseljaMapa } from "@/components/admin/naselja-mapa";
import { Button } from "@/components/ui/button";
import { Field, Input } from "@/components/ui/field";
import { LoadingBlock } from "@/components/ui/spinner";
import { useToast } from "@/components/ui/toast";
import { ApiRequestError } from "@/lib/api/client";
import { adminApi } from "@/lib/api/endpoints";
import {
  procitajGranicu,
  prviPrsten,
  zamijeniPrviPrsten,
  zapisiGranicu,
  type Tacka,
} from "@/lib/naselja-granica";
import { cn } from "@/lib/utils";
import type { NaseljeAdmin, UticajNaselja } from "@/types/domain";

const KLJUC = ["admin", "naselja"] as const;

/**
 * Naselja u kojima dostavljamo. Kupac naselje ne bira: sistem ga određuje iz tačke adrese, pa je
 * granica ono što odlučuje gdje se dostavlja. Početne granice su procjena iz centara naselja —
 * admin ih provjerava na mapi i označava kao potvrđene.
 */
export default function AdminNaseljaPage() {
  const naselja = useQuery({
    queryKey: KLJUC,
    queryFn: () => adminApi.naselja(),
  });
  const [izabranoId, setIzabranoId] = useState<string | null>(null);
  const [novo, setNovo] = useState(false);

  const izabrano = naselja.data?.find((n) => n.id === izabranoId) ?? null;
  const otvoreno = novo || !!izabrano;
  // Prsten koji se uređuje živi ovdje, jer ga dijele forma (čuvanje) i mapa (crtanje).
  const [prsten, setPrsten] = useState<Tacka[] | null>(null);

  const izaberi = useCallback(
    (id: string) => {
      setNovo(false);
      setIzabranoId(id);
      setPrsten(
        prviPrsten(
          procitajGranicu(naselja.data?.find((n) => n.id === id)?.granica),
        ),
      );
    },
    [naselja.data],
  );
  const zatvori = () => {
    setNovo(false);
    setIzabranoId(null);
    setPrsten(null);
  };
  const granicaIzabranog = useMemo(
    () =>
      prsten
        ? zapisiGranicu(
            zamijeniPrviPrsten(procitajGranicu(izabrano?.granica), prsten),
          )
        : null,
    [prsten, izabrano?.granica],
  );
  const mapaNaselja = useMemo(
    () =>
      (naselja.data ?? []).map((n) =>
        n.id === izabranoId && prsten ? { ...n, granica: granicaIzabranog } : n,
      ),
    [naselja.data, izabranoId, prsten, granicaIzabranog],
  );

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="font-display text-2xl font-bold text-ink">Naselja</h1>
          <p className="text-sm text-muted">
            Granice naselja u kojima dostavljamo. Adresa van aktivnog naselja se
            odbija.
          </p>
        </div>
        <Button
          size="sm"
          onClick={() => {
            setIzabranoId(null);
            setNovo(true);
            setPrsten([]);
          }}
        >
          <Plus className="h-4 w-4" /> Novo naselje
        </Button>
      </div>

      {naselja.isLoading ? (
        <LoadingBlock />
      ) : (
        <div className="grid gap-6 lg:grid-cols-[320px_minmax(0,1fr)]">
          <div className="space-y-4">
            {otvoreno && prsten && (
              <UrediNaselje
                key={novo ? "novo" : izabrano!.id}
                naselje={novo ? null : izabrano}
                prsten={prsten}
                onZatvori={zatvori}
                onSacuvano={(id) => {
                  setNovo(false);
                  setIzabranoId(id);
                }}
              />
            )}
            <Spisak
              naselja={naselja.data ?? []}
              izabranoId={izabranoId}
              onIzaberi={izaberi}
            />
          </div>
          <div className="lg:sticky lg:top-24 lg:self-start">
            <NaseljaMapa
              naselja={mapaNaselja}
              izabranoId={izabranoId}
              prsten={otvoreno ? prsten : null}
              onIzaberi={izaberi}
              onPromjena={setPrsten}
            />
          </div>
        </div>
      )}
    </div>
  );
}

function Spisak({
  naselja,
  izabranoId,
  onIzaberi,
}: {
  naselja: NaseljeAdmin[];
  izabranoId: string | null;
  onIzaberi: (id: string) => void;
}) {
  return (
    <ul className="divide-y divide-line rounded-card border border-line bg-surface">
      {naselja.map((n) => (
        <li key={n.id}>
          <button
            type="button"
            onClick={() => onIzaberi(n.id)}
            className={cn(
              "flex w-full flex-col gap-0.5 px-4 py-2.5 text-left text-sm hover:bg-sand",
              n.id === izabranoId && "bg-sand",
            )}
          >
            <span
              className={cn(
                "font-medium",
                !n.isActive && "text-muted line-through",
              )}
            >
              {n.name}
            </span>
            <span className="text-xs text-muted">
              {!n.granica
                ? "bez granice"
                : n.izvorGranice === "potvrdjeno"
                  ? "potvrđeno"
                  : "procjena"}
              {!n.isActive && " · isključeno"}
              {n.otvorenihNarudzbi > 0 && ` · ${n.otvorenihNarudzbi} otvorenih`}
              {n.vanGranica > 0 && (
                <span className="font-medium text-red-700">
                  {" "}
                  · {n.vanGranica} van granica
                </span>
              )}
            </span>
          </button>
        </li>
      ))}
    </ul>
  );
}

function UrediNaselje({
  naselje,
  prsten,
  onZatvori,
  onSacuvano,
}: {
  naselje: NaseljeAdmin | null;
  prsten: Tacka[];
  onZatvori: () => void;
  onSacuvano: (id: string) => void;
}) {
  const qc = useQueryClient();
  const { show } = useToast();
  const pocetna = useMemo(
    () => procitajGranicu(naselje?.granica),
    [naselje?.granica],
  );
  const [ime, setIme] = useState(naselje?.name ?? "");
  const [aktivno, setAktivno] = useState(naselje?.isActive ?? true);
  const [potvrdjeno, setPotvrdjeno] = useState(
    naselje?.izvorGranice === "potvrdjeno",
  );
  const [uticaj, setUticaj] = useState<UticajNaselja | null>(null);
  const [greska, setGreska] = useState<string | null>(null);

  const granica = zapisiGranicu(zamijeniPrviPrsten(pocetna, prsten));

  const sacuvaj = useMutation({
    mutationFn: (potvrdiUticaj: boolean) =>
      adminApi.sacuvajNaselje({
        id: naselje?.id ?? null,
        name: ime,
        isActive: aktivno,
        granica,
        potvrdjeno,
        potvrdiUticaj,
      }),
    onSuccess: (r) => {
      if (!r.sacuvano) {
        setUticaj(r.uticaj ?? null);
        return;
      }
      setUticaj(null);
      qc.invalidateQueries({ queryKey: KLJUC });
      show(naselje ? "Naselje sačuvano" : "Naselje dodano");
      if (r.naselje) onSacuvano(r.naselje.id);
    },
    onError: (e) =>
      setGreska(
        e instanceof ApiRequestError ? e.message : "Došlo je do greške.",
      ),
  });

  const obrisi = useMutation({
    mutationFn: () => adminApi.obrisiNaselje(naselje!.id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: KLJUC });
      show("Naselje obrisano");
      onZatvori();
    },
    onError: (e) =>
      setGreska(
        e instanceof ApiRequestError ? e.message : "Došlo je do greške.",
      ),
  });

  return (
    <section className="space-y-4 rounded-card border border-line bg-surface p-4">
      <h2 className="font-display text-lg font-bold">
        {naselje ? `Uredi: ${naselje.name}` : "Novo naselje"}
      </h2>
      <Field label="Naziv" required>
        <Input value={ime} onChange={(e) => setIme(e.target.value)} />
      </Field>
      <label className="flex items-center gap-2 text-sm">
        <input
          type="checkbox"
          checked={aktivno}
          onChange={(e) => setAktivno(e.target.checked)}
        />
        Dostavljamo u ovo naselje
      </label>
      <label className="flex items-center gap-2 text-sm">
        <input
          type="checkbox"
          checked={potvrdjeno}
          onChange={(e) => setPotvrdjeno(e.target.checked)}
        />
        Granica je provjerena na mapi
      </label>
      <p className="text-xs text-muted">
        {prsten.length < 3
          ? "Klikni po mapi da nacrtaš granicu (najmanje 3 tačke)."
          : "Prevuci tačku da pomjeriš granicu. Klik na mapu dodaje tačku, desni klik na tačku je briše."}
      </p>

      {uticaj && (
        <div
          role="alert"
          className="space-y-2 rounded-lg bg-amber-50 px-3 py-2 text-sm text-amber-900"
        >
          <p>
            Ova izmjena mijenja naselje za {uticaj.narudzbe} otvorenih narudžbi,{" "}
            {uticaj.adrese} sačuvanih adresa i {uticaj.pretplate} pretplata.
            Cijene narudžbi se ne mijenjaju.
          </p>
          <div className="flex gap-2">
            <Button
              size="sm"
              onClick={() => sacuvaj.mutate(true)}
              disabled={sacuvaj.isPending}
            >
              Potvrdi
            </Button>
            <Button
              size="sm"
              variant="secondary"
              onClick={() => setUticaj(null)}
            >
              Odustani
            </Button>
          </div>
        </div>
      )}
      {greska && (
        <p
          role="alert"
          className="rounded-lg bg-red-50 px-3 py-2 text-sm font-medium text-red-700"
        >
          {greska}
        </p>
      )}

      <div className="flex flex-wrap gap-2">
        <Button
          onClick={() => {
            setGreska(null);
            sacuvaj.mutate(false);
          }}
          disabled={
            !ime.trim() ||
            sacuvaj.isPending ||
            (prsten.length > 0 && prsten.length < 3)
          }
        >
          {sacuvaj.isPending ? "Spremam…" : "Sačuvaj"}
        </Button>
        <Button variant="secondary" onClick={onZatvori}>
          Zatvori
        </Button>
        {naselje && !naselje.imaNarudzbi && (
          <Button
            variant="secondary"
            onClick={() => obrisi.mutate()}
            disabled={obrisi.isPending}
          >
            Obriši
          </Button>
        )}
      </div>
      {naselje?.imaNarudzbi && (
        <p className="text-xs text-muted">
          Naselje ima narudžbe pa se ne briše — isključi ga.
        </p>
      )}
    </section>
  );
}
