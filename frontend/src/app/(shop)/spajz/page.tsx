import { Suspense } from "react";
import type { Metadata } from "next";
import { Spajz } from "@/components/spajz/spajz";
import { LoadingBlock } from "@/components/ui/spinner";

export const metadata: Metadata = { title: "Špajz" };

/** Špajz (R3) — dodaci uz korpicu: sir, suho meso, ajvar, zimnica, med. */
export default function SpajzPage() {
  return (
    <Suspense fallback={<LoadingBlock />}>
      <Spajz />
    </Suspense>
  );
}
