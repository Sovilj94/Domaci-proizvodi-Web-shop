"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { Odbrojavanje } from "@/components/pocetna/odbrojavanje";
import { PlocicaNamirnice } from "@/components/pocetna/plocica-namirnice";
import { Sekcija } from "@/components/pocetna/sekcija";
import { SectionHeader } from "@/components/ui/section-header";
import { buttonVariants } from "@/components/ui/button";
import { korpiceApi } from "@/lib/api/endpoints";
import { useDeliverySlots, useKorpiceList, useSedmica } from "@/lib/api/hooks";
import { aktivniKrug, daniDostave, isporukaTekst } from "@/lib/sedmica";

const VIDLJIVIH = 5;

/**
 * „Dostava ove sedmice" (R1, po uzoru na farmit „Ovonedeljna dostava"): dani isporuke,
 * odbrojavanje do roka i sadržaj srednje korpice povrća kao obojene pločice (+N za ostatak).
 * Kad sadržaj još nije objavljen, kartica to kaže umjesto praznih pločica.
 */
export function OvaSedmica() {
  const { data: sedmica } = useSedmica();
  const { data: slotovi } = useDeliverySlots();
  const { data: korpice } = useKorpiceList();

  const korpica =
    korpice?.find((k) => k.tip === "povrce" && k.velicina === "srednja" && k.dostupna) ??
    korpice?.find((k) => k.tip === "povrce" && k.dostupna);

  const sadrzaj = useQuery({
    queryKey: ["korpice", "sadrzaj", korpica?.slug],
    queryFn: () => korpiceApi.sadrzaj(korpica!.slug),
    enabled: !!korpica,
    retry: false,
  });

  const krug = sedmica ? aktivniKrug(sedmica) : null;
  const dani = krug && slotovi?.length ? daniDostave(krug.isporuka, slotovi) : [];
  const stavke = sadrzaj.data?.stavke ?? [];
  const ostatak = stavke.length - VIDLJIVIH;

  return (
    <Sekcija>
      <SectionHeader
        naslov="Dostava ove sedmice"
        opis={
          dani.length > 0 ? (
            <>
              🚚 <strong>Biraš</strong> dan dostave u{" "}
              {dani.map((d, i) => (
                <span key={d.toISOString()}>
                  {i > 0 && (i === dani.length - 1 ? " ili " : ", ")}
                  <strong>{isporukaTekst(d)}</strong>
                </span>
              ))}
            </>
          ) : undefined
        }
        className="mb-8"
      />

      <div className="relative z-10">
        <Odbrojavanje />
      </div>

      <div className="mx-auto -mt-6 max-w-[560px] rounded-[26px] bg-white px-5 pb-6 pt-12 shadow-kartica sm:px-8">
        <h3 className="text-xl font-semibold text-ink">🥕 Korpica povrća</h3>
        <p className="mt-1 pl-7 text-sm text-ink/80">Sadržaj korpice ove sedmice</p>

        {stavke.length > 0 ? (
          <div className="mt-5 grid grid-cols-3 gap-3 sm:grid-cols-6">
            {stavke.slice(0, VIDLJIVIH).map((s) => (
              <PlocicaNamirnice key={s.productId} naziv={s.naziv} slika={s.imageUrl} boja={s.boja} />
            ))}
            {ostatak > 0 && (
              <Link
                href={korpica ? "/korpice/povrce" : "/korpice"}
                className="grid aspect-square place-items-center rounded-xl border border-honey-500 bg-white text-2xl font-medium text-honey-500"
                aria-label={`Još ${ostatak} namirnica`}
              >
                +{ostatak}
              </Link>
            )}
          </div>
        ) : (
          <p className="mt-5 rounded-xl bg-beige px-4 py-5 text-center text-sm text-ink">
            {sadrzaj.isLoading ? "Učitavam sadržaj…" : "Sadržaj korpice za ovu sedmicu objavljujemo uskoro."}
          </p>
        )}
      </div>

      <div className="mt-6 text-center">
        <Link href="/korpice/povrce" className={buttonVariants({ size: "lg" })}>
          Naruči korpicu
        </Link>
      </div>
    </Sekcija>
  );
}
