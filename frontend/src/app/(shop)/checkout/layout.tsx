import type { Metadata } from "next";

/** Naslov kartice pregledača za sekciju (A8) — stranice ispod su klijentske i ne mogu izvoziti metadata. */
export const metadata: Metadata = { title: "Dostava i plaćanje" };

export default function Layout({ children }: { children: React.ReactNode }) {
  return children;
}
