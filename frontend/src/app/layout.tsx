import type { Metadata, Viewport } from "next";
import {
  OG_IMAGE,
  SEO_INDEX,
  SITE_DESCRIPTION,
  SITE_KEYWORDS,
  SITE_NAME,
  SITE_URL,
} from "@/lib/seo/site";
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

const NASLOV = "E piljara — domaće sa Manjače, na tvoja vrata";

/*
 * SEO osnova za cijeli sajt (lib/seo/site.ts). Podstranice nasljeđuju Open Graph, a svoje
 * naslove, opise i kanonske adrese daju kroz `stranica()` / `privatno()` iz lib/seo/metadata.
 * `robots` slijedi SEO_INDEX: bez njega (test deploy) ništa se ne indeksira.
 */
export const metadata: Metadata = {
  metadataBase: new URL(SITE_URL),
  applicationName: SITE_NAME,
  title: { default: NASLOV, template: "%s · E piljara" },
  description: SITE_DESCRIPTION,
  keywords: SITE_KEYWORDS,
  // Kanonski URL i og:url namjerno NISU ovdje: svaka podstranica bez svojih bi ih naslijedila
  // i pokazivala na početnu. Daje ih `stranica()` po stranici.
  openGraph: {
    type: "website",
    siteName: SITE_NAME,
    locale: "sr_BA",
    title: NASLOV,
    description: SITE_DESCRIPTION,
    images: [OG_IMAGE],
  },
  twitter: { card: "summary_large_image", title: NASLOV, description: SITE_DESCRIPTION, images: [OG_IMAGE.url] },
  robots: SEO_INDEX
    ? { index: true, follow: true, googleBot: { index: true, follow: true, "max-image-preview": "large" } }
    : { index: false, follow: false },
  formatDetection: { telephone: false, email: false, address: false },
  ...(process.env.GOOGLE_SITE_VERIFICATION
    ? { verification: { google: process.env.GOOGLE_SITE_VERIFICATION } }
    : {}),
};

export const viewport: Viewport = {
  themeColor: "#228b22",
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
