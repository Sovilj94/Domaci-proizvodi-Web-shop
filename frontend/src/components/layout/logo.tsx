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
    <Link href={href} className={cn("inline-flex items-center gap-2.5", className)}>
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
          Amber mora da se mijenja sa podlogom: honey-500 na kremu daje 2.97:1 i pada
          i prag za krupan tekst (3:1), a honey-700 na tamnoj je pretaman. Zato 600
          na svijetloj (3.86:1) i 500 na tamnoj (4.91:1).
        */}
        <span className={tone === "dark" ? "text-honey-500" : "text-honey-600"}>
          piljara
        </span>
      </span>
    </Link>
  );
}
