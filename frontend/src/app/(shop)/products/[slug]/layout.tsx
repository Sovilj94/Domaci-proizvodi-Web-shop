import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { nijePronadjeno, stranica } from "@/lib/seo/metadata";
import { seoApi } from "@/lib/seo/api";
import { JsonLd, mrvicaLd, proizvodLd } from "@/lib/seo/json-ld";
import { formatPrice, unitSuffix } from "@/lib/utils";

/*
 * SEO stranice proizvoda: sama stranica je klijentska (TanStack Query), pa naslov, opis,
 * Open Graph i schema.org Product daje ovaj serverski layout. Proizvod koji API sigurno
 * nema (404) vraća pravi 404 umjesto „meke" prazne stranice sa statusom 200.
 */
type Props = { params: Promise<{ slug: string }> };

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const { slug } = await params;
  const { data: p, nema } = await seoApi.product(slug);
  if (nema) return nijePronadjeno;
  if (!p) return stranica({ naslov: "Proizvod", putanja: `/products/${slug}` });

  const cijena = `${formatPrice(p.price)}${unitSuffix(p.unit)}`;
  const domacin = p.producer ? ` Domaćin: ${p.producer.name}, ${p.producer.village}.` : "";
  return stranica({
    naslov: `${p.name} — ${cijena}`,
    opis: `${p.description || `${p.name} sa Manjače.`}${domacin} Dostava na kućni prag u Banjoj Luci.`,
    putanja: `/products/${p.slug}`,
    slika: p.imageUrl,
  });
}

export default async function Layout({ children, params }: Props & { children: React.ReactNode }) {
  const { slug } = await params;
  const { data: p, nema } = await seoApi.product(slug);
  if (nema) notFound();

  return (
    <>
      {p && (
        <JsonLd
          data={[
            proizvodLd(p),
            mrvicaLd([
              { naziv: "Početna", putanja: "/" },
              { naziv: "Špajz", putanja: "/spajz" },
              ...(p.categorySlug ? [{ naziv: p.categoryName, putanja: `/spajz?category=${p.categorySlug}` }] : []),
              { naziv: p.name, putanja: `/products/${p.slug}` },
            ]),
          ]}
        />
      )}
      {children}
    </>
  );
}
