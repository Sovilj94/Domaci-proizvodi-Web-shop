import type { Metadata } from "next";
import { Montserrat, Poppins } from "next/font/google";
import "./globals.css";
import { Providers } from "./providers";
import { PISMO_SKRIPTA } from "@/lib/pismo/pismo";

/*
 * Poppins — font farmit-a (R1), za tekst i naslove. latin-ext nije opcion: bez njega
 * č, ć, š, ž i đ padaju na zamjenski font i riječ se raspadne na dva pisma.
 */
const poppins = Poppins({
  variable: "--font-poppins",
  subsets: ["latin", "latin-ext"],
  weight: ["400", "500", "600", "700"],
  display: "swap",
});

/*
 * Poppins nema ćirilicu (provjereno u next/font podacima). Na ćirilici sve prelazi na
 * Montserrat — najsličniji geometrijski font — preko `--font-sans` u globals.css. Bez
 * preload-a, da kupac na latinici ne plaća njegovo preuzimanje.
 */
const montserrat = Montserrat({
  variable: "--font-montserrat",
  subsets: ["latin", "cyrillic", "cyrillic-ext"],
  weight: ["400", "500", "600", "700"],
  display: "swap",
  preload: false,
});

export const metadata: Metadata = {
  title: {
    default: "E piljara — domaće sa Manjače, na tvoja vrata",
    template: "%s · E piljara",
  },
  description:
    "Svježi domaći proizvodi sa Manjače — spakovani i dostavljeni na kućni prag u Banjoj Luci. Bez pijace, bez gužve, bez kompromisa.",
};

export default function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  return (
    // suppressHydrationWarning: skripta ispod mijenja `lang` i `data-pismo` prije hidratacije.
    <html
      lang="sr-Latn"
      className={`${poppins.variable} ${montserrat.variable} h-full`}
      suppressHydrationWarning
    >
      <head>
        <script dangerouslySetInnerHTML={{ __html: PISMO_SKRIPTA }} />
      </head>
      <body className="min-h-full flex flex-col">
        <Providers>{children}</Providers>
      </body>
    </html>
  );
}
