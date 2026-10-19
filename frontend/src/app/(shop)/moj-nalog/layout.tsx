import type { Metadata } from "next";
import { NalogOkvir } from "@/components/nalog/nalog-okvir";

/** Naslov kartice (A8); straža i navigacija naloga su u NalogOkvir (klijentska komponenta). */
export const metadata: Metadata = { title: { default: "Moj nalog", template: "%s · Moj nalog · E piljara" } };

export default function NalogLayout({ children }: { children: React.ReactNode }) {
  return <NalogOkvir>{children}</NalogOkvir>;
}
