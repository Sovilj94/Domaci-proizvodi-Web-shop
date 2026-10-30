import type { ApiResponse } from "@/lib/api/envelope";
import type { Korpica, Producer, Product, Recept, ReceptKratko, SadrzajSajta } from "@/types/domain";

/**
 * Serverski pozivi API-ja samo za SEO (generateMetadata, sitemap, JSON-LD). Odvojeno od
 * `lib/api/client.ts` jer onaj čita localStorage i pismo pregledača.
 *
 * Svaki poziv je „najbolji pokušaj": kratak timeout i null na bilo kakvu grešku. Render free
 * se budi i do minute, a metadata koja čeka API zadržava cijelu stranicu — bolje je vratiti
 * opšti naslov nego da kupac gleda praznu stranicu.
 */
const API_URL = (process.env.NEXT_PUBLIC_API_URL || process.env.NEXT_PUBLIC_API_BASE_URL || "http://localhost:5080").replace(
  /\/+$/,
  "",
);

const TIMEOUT_MS = 4000;
const REVALIDATE_S = 600;

/**
 * `nema: true` samo kad je API odgovorio 404 — tada stranica smije vratiti pravi 404.
 * Timeout, 5xx ili API koji nije podešen daju `nema: false`: ne znamo, pa ne sudimo.
 */
export type SeoOdgovor<T> = { data: T | null; nema: boolean };

async function seoFetch<T>(path: string): Promise<SeoOdgovor<T>> {
  try {
    const res = await fetch(`${API_URL}${path}`, {
      headers: { Accept: "application/json" },
      signal: AbortSignal.timeout(TIMEOUT_MS),
      next: { revalidate: REVALIDATE_S },
    });
    if (res.status === 404) return { data: null, nema: true };
    if (!res.ok) return { data: null, nema: false };
    const body = (await res.json()) as ApiResponse<T>;
    return { data: body.error ? null : (body.data ?? null), nema: false };
  } catch {
    return { data: null, nema: false };
  }
}

const lista = async <T,>(path: string) => (await seoFetch<T[]>(path)).data;
const enc = encodeURIComponent;

export const seoApi = {
  product: (slug: string) => seoFetch<Product>(`/products/${enc(slug)}`),
  products: () => lista<Product>("/products"),
  producer: (id: string) => seoFetch<Producer>(`/producers/${enc(id)}`),
  producers: () => lista<Producer>("/producers"),
  recept: (slug: string) => seoFetch<Recept>(`/recepti/${enc(slug)}`),
  recepti: () => lista<ReceptKratko>("/recepti"),
  korpice: () => lista<Korpica>("/korpice"),
  sadrzajSajta: async () => (await seoFetch<SadrzajSajta>("/sadrzaj-sajta")).data,
};
