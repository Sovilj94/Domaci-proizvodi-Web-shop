"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { ArrowLeft, ShoppingBasket } from "lucide-react";
import { korpiceApi } from "@/lib/api/endpoints";
import { korpicaKaoProizvod } from "@/lib/korpice";
import { ApiRequestError } from "@/lib/api/client";
import { RokTraka } from "@/components/korpice/rok-traka";
import { SmartImage } from "@/components/ui/smart-image";
import { Button } from "@/components/ui/button";
import { LoadingBlock } from "@/components/ui/spinner";
import { EmptyState } from "@/components/ui/empty-state";
import { useCart } from "@/lib/cart/cart-context";
import { useToast } from "@/components/ui/toast";
import { formatPrice } from "@/lib/utils";

/**
 * Detalj jedne korpice — isti "Dodaj u korpu" poziv kao na /korpice (vidi
 * `korpicaKaoProizvod`), plus sadržaj sedmice kad je objavljen. `useParams()`
 * umjesto `params` kao Promise-a, jer tako to radi i `(shop)/products/[slug]`.
 */
export default function KorpicaDetaljPage() {
  const slug = String(useParams().slug);
  const { add } = useCart();
  const { show } = useToast();

  const { data: korpica, isLoading, isError } = useQuery({
    queryKey: ["korpice", "detalj", slug],
    queryFn: () => korpiceApi.korpica(slug),
  });

  const {
    data: sadrzaj,
    isPending: sadrzajUcitava,
    isError: sadrzajGreska,
    error: sadrzajError,
  } = useQuery({
    queryKey: ["korpice", "sadrzaj", slug],
    queryFn: () => korpiceApi.sadrzaj(slug),
    retry: false,
  });

  // Isti ključ kao u RokTraci, pa se keš deli — ovaj upit ne duplira zahtjev dok je
  // RokTraka na stranici. Treba nam i ovdje, ne samo za prikaz trake: dugme "Naruči
  // korpicu" se gasi kad je rok prošao (vidi `rokIstekao` ispod), ne samo kad je
  // korpica rasprodata.
  const { data: sedmica } = useQuery({
    queryKey: ["korpice", "sedmica"],
    queryFn: korpiceApi.sedmica,
    staleTime: 5 * 60 * 1000,
  });

  // Backend baca NotFoundException("sadrzaj_nije_objavljen") kad sadržaj za tekuću
  // sedmicu još nije objavljen — isti obrazac kao u SadrzajModal.
  const nijeObjavljen =
    sadrzajError instanceof ApiRequestError &&
    sadrzajError.status === 404 &&
    sadrzajError.message === "sadrzaj_nije_objavljen";

  if (isLoading) return <LoadingBlock />;
  if (isError || !korpica) {
    return (
      <div className="mx-auto max-w-6xl px-4 py-12">
        <EmptyState
          title="Korpica nije pronađena"
          description="Možda je uklonjena ili je link neispravan."
          action={
            <Link href="/korpice">
              <Button variant="secondary">Nazad na korpice</Button>
            </Link>
          }
        />
      </div>
    );
  }

  const rokIstekao = sedmica?.rokIstekao ?? false;
  const nedostupna = !korpica.dostupna || rokIstekao;

  return (
    <>
      <RokTraka />

      <div className="mx-auto max-w-6xl px-4 py-8">
        <Link
          href="/korpice"
          className="inline-flex items-center gap-1.5 text-sm font-medium text-muted hover:text-ink"
        >
          <ArrowLeft className="h-4 w-4" /> Sve korpice
        </Link>

        <div className="mt-6 grid gap-8 md:grid-cols-2">
          <div className="relative aspect-square overflow-hidden rounded-card bg-sand shadow-card">
            <SmartImage
              src={korpica.imageUrl}
              alt={korpica.naziv}
              sizes="(max-width:768px) 100vw, 50vw"
            />
          </div>

          <div className="flex flex-col">
            <h1 className="font-display text-3xl font-bold">{korpica.naziv}</h1>

            <p className="mt-4 text-2xl font-bold text-ink">
              {formatPrice(korpica.cijena)}
              <span className="ml-1 text-sm font-normal text-muted">/korpica</span>
            </p>

            <p className="mt-4 text-ink/80">{korpica.opis}</p>

            <div className="mt-6">
              {nedostupna ? (
                <div className="rounded-card bg-sand px-4 py-3 text-sm font-medium text-muted">
                  {!korpica.dostupna
                    ? "Rasprodato ove sedmice — provjerite ponovo sljedeće sedmice."
                    : "Naručivanje za ovu sedmicu je zatvoreno — sadržaj je zaključan za pakovanje. Provjerite ponovo sljedeće sedmice."}
                </div>
              ) : (
                <Button
                  size="lg"
                  onClick={() => {
                    add(korpicaKaoProizvod(korpica));
                    show(`${korpica.naziv} dodana u korpu`);
                  }}
                >
                  <ShoppingBasket className="h-5 w-5" /> Naruči korpicu
                </Button>
              )}
            </div>

            {/* Sadržaj ove sedmice */}
            <div className="mt-8">
              <h2 className="font-display text-lg font-semibold">U korpici ove sedmice</h2>

              {sadrzajUcitava ? (
                <div className="mt-3">
                  <LoadingBlock />
                </div>
              ) : nijeObjavljen ? (
                <p className="mt-2 text-sm text-muted">
                  Sadržaj za ovu sedmicu još nije objavljen. Javljamo se čim bude poznat.
                </p>
              ) : sadrzajGreska ? (
                <p className="mt-2 text-sm text-muted">
                  Sadržaj trenutno nije dostupan. Pokušajte ponovo malo kasnije.
                </p>
              ) : (
                <ul className="mt-3 flex flex-col gap-2">
                  {sadrzaj?.stavke.map((s) => (
                    <li
                      key={s.productId}
                      className="flex items-center gap-3 rounded-card border border-line bg-cream p-3"
                    >
                      <div className="relative h-14 w-14 shrink-0 overflow-hidden rounded-xl bg-sand">
                        <SmartImage src={s.imageUrl} alt={s.naziv} sizes="56px" />
                      </div>
                      <div className="min-w-0 flex-1">
                        <p className="truncate font-medium text-ink">
                          {s.naziv}{" "}
                          <span className="font-normal text-muted">
                            {s.kolicina} {s.jedinica}
                          </span>
                        </p>
                        <p className="truncate text-sm font-medium text-brand-700">
                          {s.proizvodjacNaziv} · {s.proizvodjacSelo}
                        </p>
                      </div>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          </div>
        </div>
      </div>
    </>
  );
}
