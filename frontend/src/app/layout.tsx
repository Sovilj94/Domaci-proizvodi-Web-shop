import type { Metadata } from "next";
import { DM_Sans, Onest, Playfair_Display } from "next/font/google";
import "./globals.css";
import { Providers } from "./providers";
import { PISMO_SKRIPTA } from "@/lib/pismo/pismo";

/*
 * latin-ext nije opcion: cijeli sajt je na srpskom, a bez tog subseta č, ć, š, ž i đ
 * padaju na zamjenski font i naslov se raspadne na dva pisma usred riječi.
 */
const dmSans = DM_Sans({
  variable: "--font-dm-sans",
  subsets: ["latin", "latin-ext"],
  weight: ["400", "500", "700"],
  display: "swap",
});

/*
 * Playfair nosi samo naslove. 700 je najteži rez koji ima — na 800 bi ga browser
 * sintetisao razvlačenjem poteza, pa `font-extrabold` na display tekstu ne koristimo.
 */
const playfair = Playfair_Display({
  variable: "--font-playfair",
  // `cyrillic` za P7; subset ne mijenja šta se preload-uje za latinicu.
  subsets: ["latin", "latin-ext", "cyrillic"],
  weight: ["400", "600", "700"],
  display: "swap",
});

/*
 * DM Sans nema ćirilicu (provjereno u next/font podacima). Na ćirilici tijelo prelazi na
 * Onest — sličnog geometrijskog karaktera — preko `--font-sans` u globals.css. Bez
 * preload-a, da kupac na latinici ne plaća njegovo preuzimanje.
 */
const onest = Onest({
  variable: "--font-onest",
  subsets: ["latin", "cyrillic", "cyrillic-ext"],
  weight: ["400", "500", "700"],
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
      className={`${dmSans.variable} ${playfair.variable} ${onest.variable} h-full`}
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
