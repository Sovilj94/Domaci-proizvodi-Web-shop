import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  images: {
    remotePatterns: [
      { protocol: "https", hostname: "picsum.photos" },
      { protocol: "https", hostname: "images.unsplash.com" },
      { protocol: "https", hostname: "fastly.picsum.photos" },
      // Cloudflare R2 public bucket used in production.
      { protocol: "https", hostname: "*.r2.dev" },
      { protocol: "https", hostname: "cdn.epiljara.ba" },
    ],
  },
};

export default nextConfig;
