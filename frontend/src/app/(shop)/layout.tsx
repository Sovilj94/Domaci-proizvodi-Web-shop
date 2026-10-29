import { ShopNavbar } from "@/components/layout/shop-navbar";
import { TrakaRoka } from "@/components/layout/traka-roka";
import { ShopFooter } from "@/components/layout/shop-footer";
import { CartDrawer } from "@/components/shop/cart-drawer";
import { JezikProvider } from "@/components/jezik/jezik-provider";
import { JsonLd, sajtLd } from "@/lib/seo/json-ld";

/**
 * Raspored po uzoru na farmit (R1): zaglavlje, krem traka roka (osim na početnoj), sadržaj,
 * footer. Traka kategorija i donja mobilna traka su uklonjene — kategorije su u Špajzu,
 * a mobilna navigacija je meni preko cijelog ekrana (MobilniMeni).
 *
 * Traka roka NIJE u <header>-u i ne lijepi se: ljepljivi elementi stranica (`top-[88px]`,
 * `lg:top-28`) računaju samo visinu zaglavlja (72px / 96px).
 */
export default function ShopLayout({ children }: { children: React.ReactNode }) {
  return (
    <>
      {/* schema.org GroceryStore + WebSite (sa pretragom Špajza) na svakoj javnoj stranici. */}
      <JsonLd data={sajtLd()} />
      <ShopNavbar />
      <TrakaRoka />
      <main className="flex-1">{children}</main>
      <ShopFooter />
      <CartDrawer />
      <JezikProvider />
    </>
  );
}
