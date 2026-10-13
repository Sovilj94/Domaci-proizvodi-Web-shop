"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { AlertTriangle, Check, Lock, MapPin, Pencil, Plus, Star, Trash2 } from "lucide-react";
import { AdresaForma } from "@/components/nalog/adresa-forma";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { LoadingBlock } from "@/components/ui/spinner";
import { useToast } from "@/components/ui/toast";
import { queryKeys } from "@/lib/api/hooks";
import { accountApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import type { Address } from "@/types/domain";

/** Isti broj kao `CreateAddressHandler.MaxAdresa` na backendu — UI ovo odražava,
 * ne ponovo odlučuje; backend i dalje vraća `address_limit` ako se ovo ikad razmimoiđe. */
const MAX_ADRESA = 10;

export default function MojNalogAdresePage() {
  const qc = useQueryClient();
  const { show } = useToast();
  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<Address | null>(null);

  const adrese = useQuery({
    queryKey: queryKeys.adrese(),
    queryFn: () => accountApi.adrese(),
  });

  const postaviGlavnuMutacija = useMutation({
    mutationFn: (id: string) => accountApi.postaviGlavnu(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.adrese() });
      show("Glavna adresa postavljena.");
    },
    onError: (err) => {
      show(err instanceof ApiRequestError ? err.message : "Došlo je do greške.", "error");
    },
  });

  // Backend sam bira novu glavnu adresu kad se obriše trenutna glavna (jedinstven
  // indeks, tačno jedna glavna u svakom trenutku) — ovdje se ništa ne dopravljava,
  // samo se upit invalidira da se lista osvježi (vidi task-5-brief.md).
  const obrisiMutacija = useMutation({
    mutationFn: (id: string) => accountApi.obrisiAdresu(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.adrese() });
      show("Adresa obrisana.");
    },
    onError: (err) => {
      show(err instanceof ApiRequestError ? err.message : "Došlo je do greške.", "error");
    },
  });

  function otvoriNovu() {
    setEditing(null);
    setFormOpen(true);
  }

  function otvoriIzmjenu(a: Address) {
    setEditing(a);
    setFormOpen(true);
  }

  const lista = adrese.data ?? [];
  const dostignutLimit = lista.length >= MAX_ADRESA;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="font-display text-2xl font-bold text-ink md:text-3xl">Adrese</h1>
      </div>

      <div className="rounded-card border border-line bg-surface p-5 shadow-card">
        <ol className="list-decimal space-y-1 pl-5 text-sm text-ink">
          <li>Adresu svake dostave biraš posebno, na checkoutu.</li>
          <li>U svakom trenutku možeš izmijeniti podatke svake adrese.</li>
          <li>Adresu koju više ne koristiš možeš obrisati.</li>
        </ol>
        <p className="mt-3 text-sm text-muted">
          Nisi kod kuće u trenutku dostave? Napomenu za vozača ostavljaš pri naručivanju.
        </p>
      </div>

      {adrese.isLoading ? (
        <LoadingBlock />
      ) : adrese.isError ? (
        /*
          Bez ove grane neuspjelo učitavanje padne u prazno stanje ispod, pa kupac sa deset
          adresa pročita „Nemaš sačuvanih adresa" — izgleda kao da su mu podaci nestali.
        */
        <EmptyState
          title="Ne mogu da učitam adrese"
          description="Provjeri konekciju pa pokušaj ponovo."
          icon={<MapPin className="h-7 w-7" />}
          action={<Button onClick={() => adrese.refetch()}>Pokušaj ponovo</Button>}
        />
      ) : lista.length === 0 ? (
        <EmptyState
          title="Nemaš sačuvanih adresa"
          description="Dodaj prvu adresu da ubrzaš naredni checkout."
          icon={<MapPin className="h-7 w-7" />}
          action={<Button onClick={otvoriNovu}>+ Dodaj adresu</Button>}
        />
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {lista.map((a) => (
            <AdresaKartica
              key={a.id}
              adresa={a}
              onIzmijeni={() => otvoriIzmjenu(a)}
              onPostaviGlavnu={() => postaviGlavnuMutacija.mutate(a.id)}
              onObrisi={() => obrisiMutacija.mutateAsync(a.id)}
              glavnaPending={
                postaviGlavnuMutacija.isPending && postaviGlavnuMutacija.variables === a.id
              }
              brisanjePending={obrisiMutacija.isPending && obrisiMutacija.variables === a.id}
            />
          ))}

          {dostignutLimit ? (
            <div className="flex min-h-[180px] flex-col items-center justify-center gap-2 rounded-card border border-dashed border-line bg-sand/40 p-5 text-center">
              <Lock className="h-5 w-5 text-muted" />
              <p className="text-sm text-muted">
                Dostigao si najviše 10 adresa. Obriši jednu da dodaš novu.
              </p>
            </div>
          ) : (
            <button
              type="button"
              onClick={otvoriNovu}
              className="flex min-h-[180px] flex-col items-center justify-center gap-2 rounded-card border border-dashed border-line p-5 text-sm font-medium text-brand-700 transition hover:border-brand-500 hover:bg-sand/50"
            >
              <Plus className="h-5 w-5" />
              Dodaj adresu
            </button>
          )}
        </div>
      )}

      <AdresaForma open={formOpen} onClose={() => setFormOpen(false)} adresa={editing} />
    </div>
  );
}

function AdresaKartica({
  adresa,
  onIzmijeni,
  onPostaviGlavnu,
  onObrisi,
  glavnaPending,
  brisanjePending,
}: {
  adresa: Address;
  onIzmijeni: () => void;
  onPostaviGlavnu: () => void;
  onObrisi: () => Promise<unknown>;
  glavnaPending: boolean;
  brisanjePending: boolean;
}) {
  // Inline potvrda umjesto `window.confirm` — nativni dijalog zamrzava automatizaciju
  // koja provjerava ovaj ekran (vidi task-5-brief.md).
  const [potvrdaBrisanja, setPotvrdaBrisanja] = useState(false);

  return (
    <div className="flex flex-col gap-3 rounded-card border border-line bg-surface p-5 shadow-card">
      <div className="flex items-start justify-between gap-2">
        <div className="flex items-center gap-2">
          <span className="grid h-9 w-9 shrink-0 place-items-center rounded-full bg-sand text-brand-600">
            <MapPin className="h-4 w-4" />
          </span>
          <p className="font-display font-semibold text-ink">{adresa.label || "Adresa"}</p>
        </div>
        {adresa.isDefault && (
          <span className="flex shrink-0 items-center gap-1 rounded-full bg-brand-100 px-2.5 py-1 text-xs font-semibold text-brand-700">
            <Star className="h-3 w-3" /> Glavna
          </span>
        )}
      </div>

      <p className="text-sm text-ink">{adresa.fullAddress}</p>

      {adresa.deliveryZoneName && <p className="text-xs text-muted">Zona: {adresa.deliveryZoneName}</p>}

      {/*
        Namjerno prigušeno, a ne žuto kao upozorenje o zoni: adresa bez tačke je potpuno
        ispravna i checkout je prima — ruta se samo vraća na sortiranje po zonama. Adrese
        prenesene iz starog `User.Address` tačku nikad nisu imale, pa bi upozorenje
        zateklo kupca zbog nečega što nije zgriješio.
      */}
      {adresa.latitude == null && (
        <p className="text-xs text-muted">
          Bez tačke na mapi — otvori „Izmijeni“ i označi mjesto da vozač stigne tačnije.
        </p>
      )}

      {/*
        `deliveryZoneActive` je `a.DeliveryZone?.IsActive ?? false` (MappingExtensions.cs:203),
        pa je `false` i kad zone uopšte NEMA. Zona je u formi opciona, tako da bi adresa
        sačuvana bez zone inače odmah dobila crveno „zona je ugašena" — što nije istina.
        Dva su to različita stanja i čitaju se različito.
      */}
      {!adresa.deliveryZoneId ? (
        <p className="flex items-start gap-1.5 rounded-lg bg-amber-50 px-3 py-2 text-xs font-medium text-amber-800">
          <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
          Bez zone dostave — izaberi zonu da checkout prihvati ovu adresu.
        </p>
      ) : (
        !adresa.deliveryZoneActive && (
          <p className="flex items-start gap-1.5 rounded-lg bg-red-50 px-3 py-2 text-xs font-medium text-red-700">
            <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
            Zona dostave je ugašena — checkout će odbiti ovu adresu dok je ne promijeniš.
          </p>
        )
      )}

      <div className="mt-auto flex flex-wrap items-center gap-2 border-t border-line pt-3">
        {potvrdaBrisanja ? (
          <>
            <span className="text-sm font-medium text-ink">Sigurno?</span>
            {/*
              Na uspjeh kartica nestane sa liste, pa nema šta da se vraća. Na grešku kartica
              ostaje — i bez ovog `catch` zaglavi na „Sigurno?" dok kupac ručno ne otkaže.
              Toast sa porukom o grešci diže roditeljski `onError`.
            */}
            <Button
              variant="danger"
              size="sm"
              onClick={() => void onObrisi().catch(() => setPotvrdaBrisanja(false))}
              disabled={brisanjePending}
            >
              {brisanjePending ? "Brišem…" : "Obriši"}
            </Button>
            <Button variant="secondary" size="sm" onClick={() => setPotvrdaBrisanja(false)}>
              Otkaži
            </Button>
          </>
        ) : (
          <>
            <Button variant="secondary" size="sm" onClick={onIzmijeni}>
              <Pencil className="h-3.5 w-3.5" /> Izmijeni
            </Button>
            {!adresa.isDefault && (
              <Button
                variant="secondary"
                size="sm"
                onClick={onPostaviGlavnu}
                disabled={glavnaPending}
              >
                <Check className="h-3.5 w-3.5" /> {glavnaPending ? "…" : "Postavi kao glavnu"}
              </Button>
            )}
            <Button
              variant="ghost"
              size="sm"
              className="text-red-700 hover:bg-red-50"
              onClick={() => setPotvrdaBrisanja(true)}
            >
              <Trash2 className="h-3.5 w-3.5" /> Obriši
            </Button>
          </>
        )}
      </div>
    </div>
  );
}
