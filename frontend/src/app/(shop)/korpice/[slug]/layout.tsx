import type { Metadata } from "next";
import { VRSTE } from "@/lib/planovi";
import { stranica } from "@/lib/seo/metadata";

/*
 * `/korpice/povrce` i `/korpice/voce` su prave stranice; svaki drugi segment je stari slug
 * korpice koji stranica preusmjerava na novu adresu — taj se ne indeksira.
 * Breadcrumb JSON-LD je samo u `[velicina]` layoutu: ovaj layout obuhvata i tu stranicu, pa
 * bi ona inače nosila dva BreadcrumbList-a.
 */
type Props = { params: Promise<{ slug: string }> };

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const { slug } = await params;
  const vrsta = VRSTE.find((v) => v.tip === slug);
  if (!vrsta) return { robots: { index: false, follow: true } };
  return stranica({
    naslov: `Korpice ${vrsta.mnozina}`,
    opis: `Sedmična korpica domaćeg ${vrsta.mnozina} sa Manjače — mala, srednja ili velika, jednom ili uz pretplatu. Dostava na kućni prag u Banjoj Luci, plaćanje pouzećem.`,
    putanja: `/korpice/${slug}`,
  });
}

export default function Layout({ children }: { children: React.ReactNode }) {
  return children;
}
