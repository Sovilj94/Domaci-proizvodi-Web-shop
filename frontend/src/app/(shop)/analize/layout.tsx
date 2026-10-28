import type { Metadata } from "next";
import { stranica } from "@/lib/seo/metadata";

/** Naslov, opis i kanonska adresa sekcije (SEO). Stranice ispod su klijentske i ne mogu izvoziti metadata. */
export const metadata: Metadata = stranica({
  naslov: "Analize",
  opis: "Laboratorijske analize domaćih proizvoda sa Manjače — rezultati po domaćinu i proizvodu, javno i bez skrivanja.",
  putanja: "/analize",
});

export default function Layout({ children }: { children: React.ReactNode }) {
  return children;
}
