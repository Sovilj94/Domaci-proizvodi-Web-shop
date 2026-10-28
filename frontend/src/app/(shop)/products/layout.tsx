import type { Metadata } from "next";
import { stranica } from "@/lib/seo/metadata";

/** Naslov, opis i kanonska adresa sekcije (SEO). Stranice ispod su klijentske i ne mogu izvoziti metadata. */
export const metadata: Metadata = stranica({
  naslov: "Špajz",
  opis: "Domaći sir, suho meso, ajvar, zimnica, med i sezonsko povrće i voće sa Manjače — naruči uz korpicu, dostava u Banjoj Luci.",
  putanja: "/spajz",
});

export default function Layout({ children }: { children: React.ReactNode }) {
  return children;
}
