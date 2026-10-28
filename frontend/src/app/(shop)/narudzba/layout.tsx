import type { Metadata } from "next";
import { privatno } from "@/lib/seo/metadata";

/** Naslov kartice (A8) i noindex — privatna/transakciona stranica (lib/seo/metadata). */
export const metadata: Metadata = privatno("Praćenje narudžbe");

export default function Layout({ children }: { children: React.ReactNode }) {
  return children;
}
