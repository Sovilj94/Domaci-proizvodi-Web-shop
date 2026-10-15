import Link from "next/link";
import { Sprout } from "lucide-react";
import { cn } from "@/lib/utils";

/**
 * `tone` postoji jer logo stoji i na kremu i na tamnoj podlozi (footer, hero preko
 * fotografije). Umjesto dva komponente ili `text-white` posutog po pozivaocima,
 * jedna prop bira par boja koji je na toj podlozi provjeren.
 */
export function Logo({
  href = "/",
  tone = "light",
  className,
}: {
  href?: string;
  tone?: "light" | "dark";
  className?: string;
}) {
  return (
    // translate="no": logotip ostaje latinicom i na ćirilici (P7) — tekst je razbijen na
    // dva elementa, pa ga pravilo za naziv u preslovi.ts ne prepoznaje kao cjelinu.
    <Link
      href={href}
      translate="no"
      className={cn("inline-flex items-center gap-2.5", className)}
    >
      <span className="grid h-9 w-9 place-items-center rounded-full bg-brand-600 text-white shadow-sm">
        <Sprout className="h-[18px] w-[18px]" />
      </span>
      <span
        className={cn(
          "font-display text-xl font-bold",
          tone === "dark" ? "text-cream" : "text-ink",
        )}
      >
        E{" "}
        {/*
          Na svijetloj podlozi riječ nosi brand-600 (8.16:1 na cream). Na tamnoj bi
          brand-600 pao, pa ide brand-300 (#8dbda2) koji na night daje 7.69:1.
        */}
        <span className={tone === "dark" ? "text-brand-300" : "text-brand-600"}>
          piljara
        </span>
      </span>
    </Link>
  );
}
