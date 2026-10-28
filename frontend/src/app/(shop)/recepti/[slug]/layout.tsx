import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { nijePronadjeno, stranica } from "@/lib/seo/metadata";
import { seoApi } from "@/lib/seo/api";
import { JsonLd, mrvicaLd, receptLd } from "@/lib/seo/json-ld";

/* SEO recepta (klijentska stranica): naslov, opis, Open Graph i schema.org Recipe. */
type Props = { params: Promise<{ slug: string }> };

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const { slug } = await params;
  const { data: r, nema } = await seoApi.recept(slug);
  if (nema) return nijePronadjeno;
  if (!r) return stranica({ naslov: "Recept", putanja: `/recepti/${slug}` });
  return stranica({
    naslov: `${r.naslov} — recept`,
    opis: r.opis || `${r.naslov}: recept od domaćih namirnica sa Manjače.`,
    putanja: `/recepti/${r.slug}`,
    slika: r.slikaUrl,
    tip: "article",
  });
}

export default async function Layout({ children, params }: Props & { children: React.ReactNode }) {
  const { slug } = await params;
  const { data: r, nema } = await seoApi.recept(slug);
  if (nema) notFound();

  return (
    <>
      {r && (
        <JsonLd
          data={[
            receptLd(r),
            mrvicaLd([
              { naziv: "Početna", putanja: "/" },
              { naziv: "Recepti", putanja: "/recepti" },
              { naziv: r.naslov, putanja: `/recepti/${r.slug}` },
            ]),
          ]}
        />
      )}
      {children}
    </>
  );
}
