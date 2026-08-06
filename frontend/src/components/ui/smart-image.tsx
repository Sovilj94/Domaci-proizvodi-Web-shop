"use client";

import Image from "next/image";
import { useState } from "react";
import { Sprout } from "lucide-react";
import { cn } from "@/lib/utils";

/**
 * Fills its (relatively positioned) parent. Falls back to a leafy gradient
 * placeholder when the image is missing or fails to load — so the UI never
 * shows a broken image icon, even offline.
 */
export function SmartImage({
  src,
  alt,
  sizes = "(max-width: 768px) 50vw, 25vw",
  className,
}: {
  src?: string;
  alt: string;
  sizes?: string;
  className?: string;
}) {
  const [failed, setFailed] = useState(false);

  if (!src || failed) {
    return (
      <div className="absolute inset-0 flex items-center justify-center bg-gradient-to-br from-brand-100 to-honey-100">
        <Sprout className="h-10 w-10 text-brand-400" />
      </div>
    );
  }

  return (
    <Image
      src={src}
      alt={alt}
      fill
      sizes={sizes}
      onError={() => setFailed(true)}
      className={cn("object-cover", className)}
      unoptimized
    />
  );
}
