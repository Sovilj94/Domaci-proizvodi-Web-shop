import { ShopNavbar } from "@/components/layout/shop-navbar";
import { ShopFooter } from "@/components/layout/shop-footer";
import { MobileTabBar } from "@/components/layout/mobile-tab-bar";
import { CartDrawer } from "@/components/shop/cart-drawer";

export default function ShopLayout({ children }: { children: React.ReactNode }) {
  return (
    <>
      <ShopNavbar />
      {/* Bottom padding on phones so content clears the fixed tab bar. */}
      <main className="flex-1 pb-16 md:pb-0">{children}</main>
      <ShopFooter />
      <MobileTabBar />
      <CartDrawer />
    </>
  );
}
