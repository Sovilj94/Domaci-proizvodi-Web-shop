import type { Metadata } from "next";
import { stranica } from "@/lib/seo/metadata";

/** Naslov, opis i kanonska adresa sekcije (SEO). Stranice ispod su klijentske i ne mogu izvoziti metadata. */
export const metadata: Metadata = stranica({
  naslov: "Naši domaćini",
  opis: "Upoznaj domaćinstva sa Manjače koja uzgajaju i spremaju ono što stiže u tvoju korpicu — ko su, gdje žive i šta prave.",
  putanja: "/proizvodjaci",
});

export default function Layout({ children }: { children: React.ReactNode }) {
  return children;
}
