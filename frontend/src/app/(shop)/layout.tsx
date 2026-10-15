import { Suspense } from "react";
import { ShopNavbar } from "@/components/layout/shop-navbar";
import { CategoryRail, CategoryRailSkeleton } from "@/components/layout/category-rail";
import { ShopFooter } from "@/components/layout/shop-footer";
import { MobileTabBar } from "@/components/layout/mobile-tab-bar";
import { CartDrawer } from "@/components/shop/cart-drawer";
import { PismoProvider } from "@/components/pismo/pismo-provider";

export default function ShopLayout({ children }: { children: React.ReactNode }) {
  return (
    <>
      <ShopNavbar />
      {/*
       * Traka je NAMJERNO van <header>-a i ne lijepi se. U headeru bi ga podigla sa 64
       * na ~164px, a četiri `sticky top-*` offseta u korpi, checkoutu, računu i na
       * stranici proizvoda kodiraju staru visinu kao broj — svi bi kliznuli ispod.
       * Ovako `top-16` i dalje znači tačno visinu sticky headera.
       */}
      <Suspense fallback={<CategoryRailSkeleton />}>
        <CategoryRail />
      </Suspense>
      <main className="flex-1">{children}</main>
      <ShopFooter />
      <MobileTabBar />
      <CartDrawer />
      <PismoProvider />
    </>
  );
}
