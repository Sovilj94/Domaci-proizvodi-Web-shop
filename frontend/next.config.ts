import type { NextConfig } from "next";

// Slike proizvoda, kategorija i domaćina su u bazi i servira ih API na /slike/... Sajt ih traži
// na svom domenu (relativne putanje iz ImageUrl), a Next ih prosljeđuje API-ju.
const API = (process.env.NEXT_PUBLIC_API_URL || process.env.NEXT_PUBLIC_API_BASE_URL || "").replace(/\/$/, "");

const nextConfig: NextConfig = {
  images: {
    // Slike iz baze su već pripremljene (~960px), a optimizer bi ih vukao kroz rewrite i trošio
    // Vercel kvotu — servira ih direktno.
    unoptimized: true,
    remotePatterns: [
      { protocol: "https", hostname: "picsum.photos" },
      { protocol: "https", hostname: "images.unsplash.com" },
      { protocol: "https", hostname: "fastly.picsum.photos" },
      // Cloudflare R2 public bucket used in production.
      { protocol: "https", hostname: "*.r2.dev" },
      { protocol: "https", hostname: "cdn.epiljara.ba" },
      // Render test deploy (API danas ne servira slike — placeholder ako počne).
      { protocol: "https", hostname: "*.onrender.com" },
    ],
  },
  async rewrites() {
    return API.startsWith("http") ? [{ source: "/slike/:path*", destination: `${API}/slike/:path*` }] : [];
  },
};

export default nextConfig;
