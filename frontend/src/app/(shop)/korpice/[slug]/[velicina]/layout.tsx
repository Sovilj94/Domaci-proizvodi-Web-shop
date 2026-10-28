import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { VELICINE, VRSTE } from "@/lib/planovi";
import { stranica } from "@/lib/seo/metadata";
import { seoApi } from "@/lib/seo/api";
import { JsonLd, korpicaLd, mrvicaLd } from "@/lib/seo/json-ld";
import { formatPrice } from "@/lib/utils";

/* SEO stranice jedne korpice `/korpice/{vrsta}/{velicina}`: naslov sa cijenom i schema.org Product. */
type Props = { params: Promise<{ slug: string; velicina: string }> };

async function ucitaj(slug: string, velicina: string) {
  const vrsta = VRSTE.find((v) => v.tip === slug);
  const vel = VELICINE.find((v) => v.velicina === velicina);
  if (!vrsta || !vel) return null;
  const korpica = (await seoApi.korpice())?.find((k) => k.tip === vrsta.tip && k.velicina === vel.velicina);
  return { vrsta, vel, korpica };
}

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const { slug, velicina } = await params;
  const u = await ucitaj(slug, velicina);
  if (!u) return {};
  const { vrsta, vel, korpica } = u;
  const cijena = korpica ? ` — ${formatPrice(korpica.cijena)}` : "";
  return stranica({
    naslov: `${vel.naziv} korpica ${vrsta.mnozina}${cijena}`,
    opis:
      korpica?.opis ||
      `${vel.naziv} korpica domaćeg ${vrsta.mnozina} sa Manjače, ${vel.clanovi}. Sedmična dostava u Banjoj Luci, uz pretplatu jeftinije.`,
    putanja: `/korpice/${vrsta.tip}/${vel.velicina}`,
    slika: korpica?.imageUrl,
  });
}

export default async function Layout({ children, params }: Props & { children: React.ReactNode }) {
  const { slug, velicina } = await params;
  const u = await ucitaj(slug, velicina);
  if (!u) notFound();
  const { vrsta, vel, korpica } = u;
  const putanja = `/korpice/${vrsta.tip}/${vel.velicina}`;

  return (
    <>
      <JsonLd
        data={[
          ...(korpica ? [korpicaLd(korpica, putanja)] : []),
          mrvicaLd([
            { naziv: "Početna", putanja: "/" },
            { naziv: `Korpice ${vrsta.mnozina}`, putanja: `/korpice/${vrsta.tip}` },
            { naziv: `${vel.naziv} korpica`, putanja },
          ]),
        ]}
      />
      {children}
    </>
  );
}
