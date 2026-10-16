"use client";

import Link from "next/link";
import { Suspense } from "react";
import { useParams } from "next/navigation";
import { KorpicaDetalj } from "@/components/korpice/korpica-detalj";
import { buttonVariants } from "@/components/ui/button";
import { LoadingBlock } from "@/components/ui/spinner";
import { jeVelicina, jeVrsta } from "@/lib/planovi";

/** Stranica jedne korpice: `/korpice/{povrce|voce}/{mala|srednja|velika}?plan=` (R2). */
export default function KorpicaPage() {
  const params = useParams();
  const vrsta = String(params.slug);
  const velicina = String(params.velicina);

  if (!jeVrsta(vrsta) || !jeVelicina(velicina)) {
    return (
      <div className="mx-auto max-w-xl px-4 py-16 text-center">
        <h1 className="text-2xl font-bold text-ink">Korpica nije pronađena</h1>
        <Link href="/korpice/povrce" className={`${buttonVariants()} mt-6`}>
          Pogledaj korpice
        </Link>
      </div>
    );
  }

  return (
    <Suspense fallback={<LoadingBlock />}>
      <KorpicaDetalj vrsta={vrsta} velicina={velicina} />
    </Suspense>
  );
}
