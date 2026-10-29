"use client";

import Image from "next/image";
import { useState } from "react";
import { cn } from "@/lib/utils";

/**
 * Slika namirnice na obojenoj pločici, kao na farmitu: izrez bez pozadine (`izrezUrl`) stoji
 * „na stolu" sa mekom sjenkom ispod. Bez izreza ide obična fotografija, zaobljena. Slika koja
 * ne postoji se sakrije — ostaje pločica bez polomljene ikone.
 */
export function SlikaKulture({
  izrezUrl,
  imageUrl,
  sizes,
  className,
}: {
  izrezUrl?: string;
  imageUrl?: string;
  sizes: string;
  className?: string;
}) {
  const [palIzrez, setPalIzrez] = useState(false);
  const [palaSlika, setPalaSlika] = useState(false);
  const izrez = izrezUrl && !palIzrez ? izrezUrl : null;

  if (izrez) {
    return (
      <div className={cn("relative", className)}>
        <span
          className="absolute bottom-1 left-1/2 h-3 w-9/12 -translate-x-1/2 rounded-[50%] bg-black/25 blur-[6px]"
          aria-hidden
        />
        <Image
          src={izrez}
          alt=""
          fill
          sizes={sizes}
          className="relative object-contain drop-shadow-[0_6px_6px_rgba(0,0,0,0.18)]"
          onError={() => setPalIzrez(true)}
        />
      </div>
    );
  }
  if (!imageUrl || palaSlika) return null;
  return (
    <div className={cn("relative overflow-hidden rounded-lg", className)}>
      <Image src={imageUrl} alt="" fill sizes={sizes} className="object-cover" onError={() => setPalaSlika(true)} />
    </div>
  );
}
