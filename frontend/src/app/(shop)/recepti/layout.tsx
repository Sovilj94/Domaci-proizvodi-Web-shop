import type { Metadata } from "next";
import { stranica } from "@/lib/seo/metadata";

/** Naslov, opis i kanonska adresa sekcije (SEO). Stranice ispod su klijentske i ne mogu izvoziti metadata. */
export const metadata: Metadata = stranica({
  naslov: "Recepti",
  opis: "Recepti od domaćih namirnica sa Manjače — jednim klikom dodaš sve sastojke u korpu i dobiješ ih na kućni prag u Banjoj Luci.",
  putanja: "/recepti",
});

export default function Layout({ children }: { children: React.ReactNode }) {
  return children;
}
