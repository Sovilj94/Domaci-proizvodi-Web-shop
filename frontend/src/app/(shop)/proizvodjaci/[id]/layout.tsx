import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { nijePronadjeno, stranica } from "@/lib/seo/metadata";
import { seoApi } from "@/lib/seo/api";
import { domacinLd, JsonLd, mrvicaLd } from "@/lib/seo/json-ld";

/* SEO stranice domaćina (klijentska stranica): naslov, opis, Open Graph i schema.org Organization. */
type Props = { params: Promise<{ id: string }> };

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const { id } = await params;
  const { data: d, nema } = await seoApi.producer(id);
  if (nema) return nijePronadjeno;
  if (!d) return stranica({ naslov: "Domaćin", putanja: `/proizvodjaci/${id}` });
  const artikli = d.artikli.length ? ` Proizvodi: ${d.artikli.join(", ")}.` : "";
  return stranica({
    naslov: `${d.name}, ${d.village}`,
    opis: `${d.description || `Domaćinstvo ${d.name} iz mjesta ${d.village}.`}${artikli}`,
    putanja: `/proizvodjaci/${d.id}`,
    slika: d.photoUrl,
  });
}

export default async function Layout({ children, params }: Props & { children: React.ReactNode }) {
  const { id } = await params;
  const { data: d, nema } = await seoApi.producer(id);
  if (nema) notFound();

  return (
    <>
      {d && (
        <JsonLd
          data={[
            domacinLd(d),
            mrvicaLd([
              { naziv: "Početna", putanja: "/" },
              { naziv: "Naši domaćini", putanja: "/proizvodjaci" },
              { naziv: d.name, putanja: `/proizvodjaci/${d.id}` },
            ]),
          ]}
        />
      )}
      {children}
    </>
  );
}
