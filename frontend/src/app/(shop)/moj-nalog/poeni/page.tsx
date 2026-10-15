"use client";

import { useQuery } from "@tanstack/react-query";
import { Coins } from "lucide-react";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { LoadingBlock } from "@/components/ui/spinner";
import { useToast } from "@/components/ui/toast";
import { queryKeys } from "@/lib/api/hooks";
import { accountApi } from "@/lib/api/endpoints";
import { cn, formatDate, formatPrice } from "@/lib/utils";

export default function MojNalogPoeniPage() {
  const { show } = useToast();
  const poeni = useQuery({
    queryKey: queryKeys.mojiPoeni(),
    queryFn: () => accountApi.poeni(),
  });

  async function kopiraj(link: string) {
    try {
      await navigator.clipboard.writeText(link);
      show("Link je kopiran");
    } catch {
      show("Ne mogu da kopiram link. Označi ga i kopiraj ručno.", "error");
    }
  }

  if (poeni.isLoading) {
    return (
      <div className="space-y-6">
        <h1 className="font-display text-2xl font-bold text-ink md:text-3xl">Poeni</h1>
        <LoadingBlock />
      </div>
    );
  }

  if (poeni.isError || !poeni.data) {
    return (
      <div className="space-y-6">
        <h1 className="font-display text-2xl font-bold text-ink md:text-3xl">Poeni</h1>
        <EmptyState
          title="Ne mogu da učitam poene"
          description="Provjeri konekciju pa pokušaj ponovo."
          icon={<Coins className="h-7 w-7" />}
          action={<Button onClick={() => poeni.refetch()}>Pokušaj ponovo</Button>}
        />
      </div>
    );
  }

  const p = poeni.data;
  // Brojevi dolaze sa servera; 20 poena = 1 KM je samo izvedeni prikaz vrijednosti poena.
  const poenaZaKM = Math.round(1 / p.vrijednostPoenaKM);
  const link = `${window.location.origin}/register?preporuka=${p.preporukaKod}`;

  return (
    <div className="space-y-6">
      <h1 className="font-display text-2xl font-bold text-ink md:text-3xl">Poeni</h1>

      <div className="rounded-card border border-line bg-surface p-5 shadow-card">
        <p className="text-sm text-muted">Tvoje stanje</p>
        <p className="mt-1 font-display text-4xl font-bold text-ink">{p.stanje}</p>
        <p className="text-sm font-medium text-brand-700">= {formatPrice(p.vrijednostKM)}</p>
        <p className="mt-4 text-sm text-ink">
          1 poen za svaku KM robe na isporučenoj narudžbi · {poenaZaKM} poena = 1 KM popusta
        </p>
        <p className="mt-1 text-sm text-muted">Poene trošiš na checkoutu.</p>
      </div>

      <div className="rounded-card border border-line bg-surface p-5 shadow-card">
        <h2 className="font-display text-lg font-bold text-ink">Preporuči prijatelju</h2>
        <div className="mt-3 flex gap-2">
          <input
            readOnly
            value={link}
            onFocus={(e) => e.currentTarget.select()}
            aria-label="Link za preporuku"
            className="min-w-0 flex-1 rounded-[10px] border border-line bg-sand px-3 py-2 text-sm text-ink"
          />
          <Button type="button" variant="secondary" onClick={() => kopiraj(link)}>
            Kopiraj
          </Button>
        </div>
        <p className="mt-3 text-sm text-ink">
          Kad prijatelj primi prvu isporuku, oboje dobijate po {p.nagradaPreporuka} poena.
        </p>
        <p className="mt-1 text-sm text-muted">Preporučio si: {p.brojPreporuka}</p>
      </div>

      <div className="rounded-card border border-line bg-surface p-5 shadow-card">
        <h2 className="font-display text-lg font-bold text-ink">Istorija</h2>
        {p.istorija.length === 0 ? (
          <p className="mt-3 text-sm text-muted">
            Još nema poena. Prvi stižu sa prvom isporukom.
          </p>
        ) : (
          <ul className="mt-3 divide-y divide-line">
            {p.istorija.map((s, i) => (
              <li key={i} className="flex items-center justify-between gap-3 py-2.5 text-sm">
                <div className="min-w-0">
                  <p className="text-ink">{s.opis}</p>
                  <p className="text-xs text-muted">{formatDate(s.kreirana)}</p>
                </div>
                <span
                  className={cn(
                    "shrink-0 font-semibold",
                    s.iznos > 0 ? "text-brand-700" : "text-muted",
                  )}
                >
                  {s.iznos > 0 ? `+${s.iznos}` : `−${Math.abs(s.iznos)}`}
                </span>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}
