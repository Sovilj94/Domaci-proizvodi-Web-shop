import type { MetadataRoute } from "next";
import { SEO_INDEX, SITE_URL } from "@/lib/seo/site";

/**
 * /robots.txt. Bez SEO_INDEX=true (test deploy, preview) zabranjuje sve. Inače zatvara
 * privatne i transakcione putanje — iste koje u svom layoutu nose `privatno()`.
 */
export default function robots(): MetadataRoute.Robots {
  if (!SEO_INDEX) return { rules: { userAgent: "*", disallow: "/" } };

  return {
    rules: {
      userAgent: "*",
      allow: "/",
      disallow: [
        "/admin",
        "/api/",
        "/moj-nalog",
        "/cart",
        "/checkout",
        "/login",
        "/register",
        "/confirm-email",
        "/potvrda/",
        "/narudzba/",
        "/orders",
        "/*?*search=",
      ],
    },
    sitemap: `${SITE_URL}/sitemap.xml`,
    host: SITE_URL,
  };
}
