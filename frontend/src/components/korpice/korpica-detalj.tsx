"use client";

import Link from "next/link";
import { useEffect, useRef, useState, type RefObject } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { ArrowLeft, CheckCircle2 } from "lucide-react";
import { KorpicaKartica, OsobineKorpice } from "@/components/korpice/korpica-kartica";
import { UtisciKarusel } from "@/components/pocetna/utisci-karusel";
import { CestaPitanja } from "@/components/pocetna/cesta-pitanja";
import { StavkaSadrzajaKartica } from "@/components/korpice/stavka-sadrzaja";
import { DodajIzSpajza } from "@/components/spajz/dodaj-iz-spajza";
import { PretplataForma } from "@/components/nalog/pretplata-forma";
import { SmartImage } from "@/components/ui/smart-image";
import { buttonVariants } from "@/components/ui/button";
import { LoadingBlock } from "@/components/ui/spinner";
import { useToast } from "@/components/ui/toast";
import { korpiceApi } from "@/lib/api/endpoints";
import { useDeliverySlots, useKorpiceList, useSedmica } from "@/lib/api/hooks";
import { useAuth } from "@/lib/auth/auth-context";
import { useCart } from "@/lib/cart/cart-context";
import { korpicaKaoProizvod } from "@/lib/korpice";
import { cijenaPlana, isporuka, obavezaPlana, planIzUrla, PLANOVI, VELICINE, VRSTE } from "@/lib/planovi";
import { aktivniKrug, daniDostave, daniDostaveTekst } from "@/lib/sedmica";
import { cn, formatPrice } from "@/lib/utils";
import type { VelicinaKorpice } from "@/types/domain";

/**
 * Stranica jedne korpice (R2, po uzoru na farmit /gajbice/{vrsta}/{velicina}): fotografija
 * lijevo; desno veličina, osobine, info o dostavi i izbor kupovine — pretplata po planu ili
 * jednokratno — pa ispod sadržaj sedmice u obojenim karticama.
 *
 * Jednokratna kupovina ide u korpu (isti tok kao do sada). Pretplata ne ide kroz korpu nego
 * kroz `PretplataForma` (adresa i termin), jer pretplatna narudžba nastaje sama svake sedmice.
 */
export function KorpicaDetalj({ vrsta, velicina }: { vrsta: "povrce" | "voce"; velicina: VelicinaKorpice }) {
  const router = useRouter();
  const plan = planIzUrla(useSearchParams().get("plan"));
  const { data: korpice, isLoading } = useKorpiceList();
  const { data: sedmica } = useSedmica();
  const { data: slotovi } = useDeliverySlots();
  const { user } = useAuth();
  const { add } = useCart();
  const { show } = useToast();
  const [izbor, setIzbor] = useState<"pretplata" | "jednom">("pretplata");
  const [pretplataOtvorena, setPretplataOtvorena] = useState(false);
  const kupovina = useRef<HTMLDivElement>(null);

  const korpica = korpice?.find((k) => k.tip === vrsta && k.velicina === velicina && k.dostupna);
  const sadrzaj = useQuery({
    queryKey: ["korpice", "sadrzaj", korpica?.slug],
    queryFn: () => korpiceApi.sadrzaj(korpica!.slug),
    enabled: !!korpica,
    retry: false,
  });

  if (isLoading) return <LoadingBlock />;
  if (!korpica) {
    return (
      <div className="mx-auto max-w-xl px-4 py-16 text-center">
        <h1 className="text-2xl font-bold text-ink">Ova korpica trenutno nije u ponudi</h1>
        <Link href={`/korpice/${vrsta}`} className={cn(buttonVariants(), "mt-6")}>
          Pogledaj korpice
        </Link>
      </div>
    );
  }

  const vel = VELICINE.find((v) => v.velicina === velicina)!;
  const vrstaInfo = VRSTE.find((v) => v.tip === vrsta)!;
  const krug = sedmica ? aktivniKrug(sedmica) : null;
  const dani = krug && slotovi?.length ? daniDostaveTekst(daniDostave(krug.isporuka, slotovi)) : null;
  const cijenaPretplate = cijenaPlana(korpica, plan.plan);
  const obaveza = obavezaPlana(korpica, plan.plan);
  const usteda = Math.max(0, korpica.cijena - cijenaPretplate);

  function potvrdi() {
    if (izbor === "jednom") {
      add(korpicaKaoProizvod(korpica!));
      show(`${korpica!.naziv} je u korpi`);
      return;
    }
    if (!user) {
      router.push(`/login?next=${encodeURIComponent(`/korpice/${vrsta}/${velicina}?plan=${plan.kljuc}`)}`);
      return;
    }
    setPretplataOtvorena(true);
  }

  return (
    <>
      <section className="mx-auto max-w-6xl px-4 py-8 lg:py-12">
        <div className="grid gap-8 lg:grid-cols-[1fr_520px] lg:gap-10">
          <div>
            <Link
              href={`/korpice/${vrsta}?plan=${plan.kljuc}`}
              aria-label="Nazad na korpice"
              className="mb-4 inline-grid h-10 w-10 place-items-center rounded-full bg-white shadow-sm"
            >
              <ArrowLeft className="h-5 w-5" />
            </Link>
            <div className="relative aspect-square overflow-hidden rounded-[26px] bg-sand lg:aspect-[4/3.6]">
              <SmartImage src={korpica.imageUrl} alt={korpica.naziv} sizes="(max-width:1024px) 100vw, 600px" />
            </div>
          </div>

          <div className="space-y-5">
            <div className="rounded-[26px] bg-white p-6 shadow-[0_6px_24px_rgba(64,74,61,0.12)]">
              <h1 className="text-2xl font-semibold text-ink">
                {vel.naziv} korpica {vrstaInfo.mnozina}
              </h1>
              <p className="text-sm text-ink/80">Korpica {vel.clanovi}</p>

              <div className="mt-4 grid grid-cols-3 gap-2">
                {VELICINE.map((v) => (
                  <Link
                    key={v.velicina}
                    href={`/korpice/${vrsta}/${v.velicina}?plan=${plan.kljuc}`}
                    scroll={false}
                    className={cn(
                      "rounded-full border py-2 text-center text-sm font-medium uppercase transition",
                      v.velicina === velicina
                        ? "border-honey-500 bg-honey-100 text-honey-500"
                        : "border-line text-ink hover:border-honey-500",
                    )}
                  >
                    {v.naziv}
                  </Link>
                ))}
              </div>

              <div className="mt-5">
                <OsobineKorpice obaveza={0} />
              </div>

              <div className="mt-5 space-y-1.5 rounded-xl border border-brand-300 bg-brand-50 px-4 py-3 text-sm text-ink">
                {dani && (
                  <p>
                    🚚 Biraš dan dostave: <strong>{dani}</strong>
                  </p>
                )}
                <p className="flex items-center gap-1.5 text-brand-700">
                  <CheckCircle2 className="h-4 w-4" /> Plaćaš pouzećem, pri dostavi
                </p>
              </div>
            </div>

            <div ref={kupovina} className="rounded-[26px] bg-white p-5 shadow-[0_6px_24px_rgba(64,74,61,0.12)]">
              {/* Pretplata */}
              <button
                type="button"
                onClick={() => setIzbor("pretplata")}
                className={cn(
                  "block w-full overflow-hidden rounded-2xl border text-left transition",
                  izbor === "pretplata" ? "border-honey-500 bg-honey-100" : "border-line bg-white",
                )}
                aria-pressed={izbor === "pretplata"}
              >
                {usteda > 0 && (
                  <span className="block bg-honey-500 py-1 text-center text-xs font-semibold uppercase text-white">
                    {formatPrice(usteda)} uštede po isporuci
                  </span>
                )}
                <span className="flex items-start gap-3 p-4">
                  <Radio aktivan={izbor === "pretplata"} />
                  <span className="flex-1">
                    <span className="block text-base font-medium text-ink">{plan.naziv}</span>
                    <span className="block text-xs text-muted">
                      {obaveza > 0 ? `Obaveza ${isporuka(obaveza)} · plaćanje pouzećem` : "Bez obaveze · plaćanje pouzećem"}
                    </span>
                  </span>
                  <span className="text-right">
                    {usteda > 0 && (
                      <span className="block text-xs text-muted line-through">{formatPrice(korpica.cijena)}</span>
                    )}
                    <span className="block text-base font-semibold text-ink">{formatPrice(cijenaPretplate)}</span>
                  </span>
                </span>
                {izbor === "pretplata" && (
                  <span className="flex flex-wrap gap-x-4 gap-y-1 px-4 pb-4 text-xs text-ink">
                    <span>⏸️ Pauziraj kad god zatreba</span>
                    <span>✓ Izmijeni sadržaj do roka</span>
                    {obaveza === 0 && <span>✓ Otkaži u svakom trenutku</span>}
                  </span>
                )}
              </button>

              {/* Planovi, kad je izabrana pretplata */}
              {izbor === "pretplata" && (
                <div className="mt-3 flex flex-wrap gap-2">
                  {PLANOVI.map((p) => (
                    <Link
                      key={p.kljuc}
                      href={`/korpice/${vrsta}/${velicina}?plan=${p.kljuc}`}
                      scroll={false}
                      className={cn(
                        "rounded-full px-3 py-1 text-xs font-medium uppercase tracking-wide",
                        p.kljuc === plan.kljuc ? "bg-brand-600 text-white" : "bg-brand-100 text-brand-700",
                      )}
                    >
                      {p.oznaka} · {formatPrice(cijenaPlana(korpica, p.plan))}
                    </Link>
                  ))}
                </div>
              )}

              {/* Jednokratno */}
              <button
                type="button"
                onClick={() => setIzbor("jednom")}
                className={cn(
                  "mt-3 flex w-full items-center gap-3 rounded-2xl border p-4 text-left transition",
                  izbor === "jednom" ? "border-honey-500 bg-honey-100" : "border-line bg-white",
                )}
                aria-pressed={izbor === "jednom"}
              >
                <Radio aktivan={izbor === "jednom"} />
                <span className="flex-1">
                  <span className="block text-base font-medium text-ink">Jednokratna kupovina</span>
                  <span className="block text-xs text-muted">Plaćanje pouzećem</span>
                </span>
                <span className="text-base font-semibold text-ink">{formatPrice(korpica.cijena)}</span>
              </button>

              <button type="button" onClick={potvrdi} className={cn(buttonVariants({ size: "lg" }), "mt-4 w-full")}>
                {izbor === "jednom" ? "Dodaj u korpu" : "Pretplati se"}
              </button>
              {izbor === "jednom" && krug?.zaSljedecu && (
                <p className="mt-2 text-center text-xs text-muted">Rok za ovu sedmicu je prošao — narudžba ide za narednu isporuku.</p>
              )}
            </div>
          </div>
        </div>
      </section>

      <section id="sadrzaj-sedmice" className="scroll-mt-28 px-4 pb-16">
        <div className="mx-auto max-w-5xl">
          <h2 className="text-center text-2xl font-semibold text-ink sm:text-[32px]">
            Ove sedmice u {vel.lokativ} korpici…
          </h2>
          {sadrzaj.data?.stavke.length ? (
            <div className="mt-8 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
              {sadrzaj.data.stavke.map((s) => (
                <StavkaSadrzajaKartica key={s.productId} s={s} />
              ))}
            </div>
          ) : (
            <p className="mt-6 text-center text-sm text-muted">
              {sadrzaj.isLoading ? "Učitavam…" : "Sadržaj za ovu sedmicu objavljujemo uskoro."}
            </p>
          )}

          {/* Po uzoru na farmit: kulture koje kupac može uzeti umjesto nečega iz kutije, sa domaćinom. */}
          {!!sadrzaj.data?.dodatne?.length && (
            <>
              <h2 className="mt-16 text-center text-2xl font-semibold text-ink sm:text-[32px]">
                Od dodatnih kultura…
              </h2>
              <p className="mx-auto mt-2 max-w-xl text-center text-sm text-ink/80">
                Poslije kupovine možeš ih izabrati umjesto nečega iz korpice, do roka za ovu sedmicu.
              </p>
              <div className="mt-8 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
                {sadrzaj.data.dodatne.map((d) => (
                  <StavkaSadrzajaKartica key={d.productId} s={d} />
                ))}
              </div>
            </>
          )}
        </div>
      </section>

      <UtisciKarusel />

      {/* „Ostali paketi" (farmit): ista korpica u druga dva plana. */}
      <section className="px-4 pb-16 pt-4">
        <div className="mx-auto max-w-6xl">
          <h2 className="text-center text-2xl font-semibold text-ink sm:text-[32px]">Ostali paketi</h2>
          <div className="mt-8 flex flex-col gap-4 lg:flex-row lg:justify-center lg:gap-8">
            {PLANOVI.filter((p) => p.kljuc !== plan.kljuc).map((p) => (
              <KorpicaKartica
                key={p.kljuc}
                korpica={korpica}
                plan={p.plan}
                kljucPlana={p.kljuc}
                onSadrzaj={() => document.getElementById("sadrzaj-sedmice")?.scrollIntoView({ behavior: "smooth" })}
              />
            ))}
          </div>
        </div>
      </section>

      {/* R3: uz korpicu se dodaju sir, suho meso, ajvar i zimnica — ista narudžba, ista dostava. */}
      <div className="bg-beige">
        <DodajIzSpajza naslov="Dodaj uz korpicu iz Špajza" />
      </div>

      <CestaPitanja />

      <LjepljivaKupovina
        sidro={kupovina}
        naziv={`${vel.naziv} korpica`}
        cijena={izbor === "jednom" ? korpica.cijena : cijenaPretplate}
        dugme={izbor === "jednom" ? "Dodaj u korpu" : "Pretplati se"}
        onClick={potvrdi}
      />

      <PretplataForma
        open={pretplataOtvorena}
        onClose={() => setPretplataOtvorena(false)}
        korpicaId={korpica.id}
        plan={plan.plan}
      />
    </>
  );
}

/**
 * Traka „Mala korpica | cijena | Dodaj u korpu" (farmit): pojavi se na vrhu kad okvir za kupovinu
 * ode iznad ekrana, pa se korpica kupuje iz bilo koje sekcije.
 */
function LjepljivaKupovina({
  sidro,
  naziv,
  cijena,
  dugme,
  onClick,
}: {
  sidro: RefObject<HTMLElement | null>;
  naziv: string;
  cijena: number;
  dugme: string;
  onClick: () => void;
}) {
  const [vidljiva, setVidljiva] = useState(false);

  useEffect(() => {
    const el = sidro.current;
    if (!el) return;
    const posmatrac = new IntersectionObserver(([u]) =>
      setVidljiva(!u.isIntersecting && u.boundingClientRect.top < 0),
    );
    posmatrac.observe(el);
    return () => posmatrac.disconnect();
  }, [sidro]);

  return (
    <div
      aria-hidden={!vidljiva}
      className={cn(
        "fixed inset-x-0 top-0 z-[45] px-2 pt-2 transition-transform duration-200 sm:px-4 sm:pt-4",
        vidljiva ? "translate-y-0" : "pointer-events-none -translate-y-[120%]",
      )}
    >
      <div className="mx-auto flex max-w-5xl items-center gap-3 rounded-2xl bg-white px-4 py-3 shadow-[0_6px_20px_rgba(64,74,61,0.18)] sm:px-8">
        <p className="min-w-0 flex-1 truncate text-sm text-ink sm:text-base">
          {naziv} <span className="text-muted">|</span> <strong>{formatPrice(cijena)}</strong>
        </p>
        <button
          type="button"
          onClick={onClick}
          tabIndex={vidljiva ? undefined : -1}
          className={cn(buttonVariants({ size: "md" }), "shrink-0 sm:px-10")}
        >
          {dugme}
        </button>
      </div>
    </div>
  );
}

function Radio({ aktivan }: { aktivan: boolean }) {
  return (
    <span
      className={cn(
        "mt-0.5 grid h-5 w-5 shrink-0 place-items-center rounded-full border-2",
        aktivan ? "border-honey-500" : "border-line",
      )}
      aria-hidden
    >
      {aktivan && <span className="h-2.5 w-2.5 rounded-full bg-honey-500" />}
    </span>
  );
}
