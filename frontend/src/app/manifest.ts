import type { MetadataRoute } from "next";
import { SITE_DESCRIPTION, SITE_NAME } from "@/lib/seo/site";

/** /manifest.webmanifest — ime i boja kad kupac doda prodavnicu na početni ekran telefona. */
export default function manifest(): MetadataRoute.Manifest {
  return {
    name: `${SITE_NAME} — domaće sa Manjače`,
    short_name: SITE_NAME,
    description: SITE_DESCRIPTION,
    lang: "sr-Latn",
    start_url: "/",
    display: "standalone",
    background_color: "#ffffff",
    theme_color: "#228b22",
    icons: [{ src: "/favicon.ico", sizes: "any", type: "image/x-icon" }],
  };
}
