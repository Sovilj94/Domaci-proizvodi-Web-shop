import type { Metadata } from "next";

/** Naslov i opis sekcije (SEO); kanonske adrese daju [slug] i [velicina] layouti. Stranice ispod su klijentske i ne mogu izvoziti metadata. */
export const metadata: Metadata = {
  title: "Korpice",
  description:
    "Sedmične korpice domaćeg povrća i voća sa Manjače u tri veličine, uz pretplatu jeftinije. Dostava na kućni prag u Banjoj Luci.",
};

export default function Layout({ children }: { children: React.ReactNode }) {
  return children;
}
