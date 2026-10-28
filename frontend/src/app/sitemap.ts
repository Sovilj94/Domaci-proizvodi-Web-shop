import type { MetadataRoute } from "next";
import { VELICINE, VRSTE } from "@/lib/planovi";
import { seoApi } from "@/lib/seo/api";
import { absoluteUrl } from "@/lib/seo/site";

/**
 * /sitemap.xml: stalne javne stranice + proizvodi, recepti i domaćini iz API-ja. Kad API
 * ne odgovori (Render spava, nema NEXT_PUBLIC_API_URL), mapa ima bar stalne stranice.
 * Osvježava se svakih sat vremena.
 */
export const revalidate = 3600;

type Unos = MetadataRoute.Sitemap[number];

export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  const sada = new Date();
  const stalna = (putanja: string, priority: number, changeFrequency: Unos["changeFrequency"]): Unos => ({
    url: absoluteUrl(putanja),
    lastModified: sada,
    changeFrequency,
    priority,
  });

  const stalne: Unos[] = [
    stalna("/", 1, "daily"),
    // `/korpice` samo preusmjerava na `/korpice/povrce`, pa nije u mapi.
    ...VRSTE.map((v) => stalna(`/korpice/${v.tip}`, 0.9, "weekly")),
    ...VRSTE.flatMap((v) => VELICINE.map((s) => stalna(`/korpice/${v.tip}/${s.velicina}`, 0.8, "weekly"))),
    stalna("/spajz", 0.9, "daily"),
    stalna("/proizvodjaci", 0.7, "weekly"),
    stalna("/recepti", 0.7, "weekly"),
    stalna("/analize", 0.5, "monthly"),
    stalna("/o-nama", 0.5, "monthly"),
    stalna("/postani-partner", 0.4, "monthly"),
  ];

  const [proizvodi, recepti, domacini] = await Promise.all([
    seoApi.products(),
    seoApi.recepti(),
    seoApi.producers(),
  ]);

  return [
    ...stalne,
    ...(proizvodi ?? [])
      .filter((p) => p.isAvailable)
      .map<Unos>((p) => ({
        url: absoluteUrl(`/products/${p.slug}`),
        lastModified: sada,
        changeFrequency: "weekly",
        priority: 0.7,
        images: p.imageUrl ? [absoluteUrl(p.imageUrl)] : undefined,
      })),
    ...(recepti ?? []).map<Unos>((r) => ({
      url: absoluteUrl(`/recepti/${r.slug}`),
      changeFrequency: "monthly",
      priority: 0.6,
      images: r.slikaUrl ? [absoluteUrl(r.slikaUrl)] : undefined,
    })),
    ...(domacini ?? []).map<Unos>((d) => ({
      url: absoluteUrl(`/proizvodjaci/${d.id}`),
      changeFrequency: "monthly",
      priority: 0.6,
      images: d.photoUrl ? [absoluteUrl(d.photoUrl)] : undefined,
    })),
  ];
}
