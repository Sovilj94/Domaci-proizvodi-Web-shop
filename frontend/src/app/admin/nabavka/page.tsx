"use client";

import { Suspense, useEffect } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { Banknote, ClipboardList, Printer, ShoppingBag } from "lucide-react";
import { IzborSedmice, useIzabranaSedmica } from "@/components/admin/izbor-sedmice";
import { StatCard } from "@/components/admin/stat-card";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { LoadingBlock } from "@/components/ui/spinner";
import { spisakRobe } from "@/lib/admin/nabavka";
import { PdfNabavkeDugme, SpisakNabavke } from "@/components/admin/spisak-nabavke";
import { adminKorpiceApi } from "@/lib/api/endpoints";
import { cn, formatDateTime, formatPrice } from "@/lib/utils";

const datumIsporuke = (iso: string) =>
  new Date(iso).toLocaleDateString("sr-Latn", { weekday: "long", day: "numeric", month: "numeric", timeZone: "Europe/Sarajevo" });

/**
 * Nabavka sedmice: kad prođe rok, korpice su zaključane i sve je naručeno — ovdje stoji koliko
 * robe treba kupiti, jedan red po artiklu (gazdinstva se ovdje ne vide), uz PDF za nabavljača ili
 * dobavljača. Bez ?datum= otvara sedmicu kojoj je rok upravo prošao (server zna rok); strelicama
 * se ide na ostale, pa i na otvorenu sedmicu kao predračun.
 */
export default function AdminNabavkaPage() {
  return (
    <Suspense fallback={<LoadingBlock />}>
      <Nabavka />
    </Suspense>
  );
}

function Nabavka() {
  const params = useSearchParams();
  const router = useRouter();
  const pathname = usePathname();
  const izbor = useIzabranaSedmica();
  const izabrana = params.has("datum");

  const q = useQuery({
    queryKey: izabrana ? ["admin", "korpice", "pakerska", izbor.godina, izbor.sedmica] : ["admin", "korpice", "pakerska", "nabavka"],
    queryFn: () => (izabrana ? adminKorpiceApi.pakerskaLista(izbor.godina, izbor.sedmica) : adminKorpiceApi.pakerskaLista()),
    refetchInterval: 60_000,
  });

  // Prva posjeta: sedmicu bira server, pa se upiše u URL da strelice kreću od nje.
  useEffect(() => {
    if (izabrana || !q.data) return;
    const next = new URLSearchParams(params.toString());
    next.set("datum", q.data.isporuka.slice(0, 10));
    router.replace(`${pathname}?${next}`, { scroll: false });
  }, [izabrana, q.data, params, pathname, router]);

  const d = q.data;
  const redovi = d ? spisakRobe(d.stavke) : [];

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="font-display text-2xl font-bold">Nabavka{d ? ` za sedmicu ${d.sedmica}.` : ""}</h1>
          <p className="text-muted">
            {d ? `Isporuka: ${datumIsporuke(d.isporuka)} — sve što treba kupiti.` : "Koliko robe treba kupiti za sedmicu."}
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-3 print:hidden">
          <IzborSedmice s={izbor} />
          {d && d.stavke.length > 0 && (
            <>
              <Button variant="secondary" size="sm" onClick={() => window.print()}>
                <Printer className="h-4 w-4" /> Štampaj
              </Button>
              <PdfNabavkeDugme lista={d} redovi={redovi} />
            </>
          )}
        </div>
      </div>

      {q.isLoading ? (
        <LoadingBlock />
      ) : q.isError || !d ? (
        <EmptyState
          title="Ne mogu da učitam nabavku"
          description="Provjeri konekciju pa pokušaj ponovo."
          action={<Button onClick={() => q.refetch()}>Pokušaj ponovo</Button>}
        />
      ) : (
        <>
          <p
            className={cn(
              "rounded-card px-4 py-3 text-sm font-medium",
              d.zakljucena ? "bg-brand-50 text-brand-800" : "bg-honey-100 text-honey-700",
            )}
          >
            {d.zakljucena
              ? `Sedmica je zaključena (${formatDateTime(d.rok)}) — korpice su zaključane i sve je naručeno. Ovo je konačan spisak.`
              : `Ovo još nije konačno: narudžbe se primaju do ${formatDateTime(d.rok)}, a kupci do tada mijenjaju korpice.`}
          </p>

          {d.stavke.length === 0 ? (
            <EmptyState
              title="Nema šta da se nabavi"
              description={d.brojNarudzbi === 0 ? "Za ovu sedmicu nema nijedne narudžbe." : "Narudžbe ove sedmice nemaju robe za nabavku."}
            />
          ) : (
            <>
              <div className="grid gap-4 sm:grid-cols-3 print:hidden">
                <StatCard label="Artikala" value={redovi.length} icon={<ClipboardList className="h-4 w-4" />} accent="brand" />
                <StatCard
                  label="Ukupno nabavno"
                  value={formatPrice(d.nabavnaVrijednost)}
                  hint={d.bezNabavneCijene > 0 ? `${d.bezNabavneCijene} bez nabavne cijene` : undefined}
                  icon={<Banknote className="h-4 w-4" />}
                  accent="honey"
                />
                <StatCard
                  label="Narudžbi"
                  value={d.brojNarudzbi}
                  icon={<ShoppingBag className="h-4 w-4" />}
                  accent="blue"
                />
              </div>

              <SpisakNabavke redovi={redovi} />
            </>
          )}
        </>
      )}
    </div>
  );
}
