import { Suspense } from "react";
import type { Metadata } from "next";
import { Spajz } from "@/components/spajz/spajz";
import { LoadingBlock } from "@/components/ui/spinner";
import { stranica } from "@/lib/seo/metadata";

export const metadata: Metadata = stranica({
  naslov: "Špajz",
  opis: "Domaći sir, suho meso, ajvar, zimnica, med i sezonsko povrće i voće sa Manjače — naruči uz korpicu, dostava u Banjoj Luci.",
  putanja: "/spajz",
});

/** Špajz (R3) — dodaci uz korpicu: sir, suho meso, ajvar, zimnica, med. */
export default function SpajzPage() {
  return (
    <Suspense fallback={<LoadingBlock />}>
      <Spajz />
    </Suspense>
  );
}
