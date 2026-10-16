"use client";

import { Suspense, useEffect } from "react";
import { useParams, useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { IzborKorpice } from "@/components/korpice/izbor-korpice";
import { LoadingBlock } from "@/components/ui/spinner";
import { korpiceApi } from "@/lib/api/endpoints";
import { jeVrsta } from "@/lib/planovi";

/**
 * `/korpice/povrce` i `/korpice/voce` su izbor korpice (R2). Svaki drugi segment je stari
 * slug korpice (`/korpice/srednja-korpica-povrca`, linkovi iz mejlova i naloga) i preusmjerava
 * se na novu adresu `/korpice/{vrsta}/{velicina}`.
 */
export default function KorpiceSlugPage() {
  const slug = String(useParams().slug);

  if (jeVrsta(slug)) {
    return (
      <Suspense fallback={<LoadingBlock />}>
        <IzborKorpice vrsta={slug} />
      </Suspense>
    );
  }
  return <StariSlug slug={slug} />;
}

function StariSlug({ slug }: { slug: string }) {
  const router = useRouter();
  const { data, isError } = useQuery({
    queryKey: ["korpice", "detalj", slug],
    queryFn: () => korpiceApi.korpica(slug),
    retry: false,
  });

  useEffect(() => {
    if (data) router.replace(`/korpice/${data.tip}/${data.velicina}`);
    else if (isError) router.replace("/korpice/povrce");
  }, [data, isError, router]);

  return <LoadingBlock />;
}
