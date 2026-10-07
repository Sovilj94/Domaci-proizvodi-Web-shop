import type { Korpica, Product } from "@/types/domain";

/**
 * Korpica se u korpu dodaje kao običan proizvod — server-side ona to i jeste
 * (`Product` sa `Vrsta = Korpica`), pa korpa, checkout i narudžba ne znaju i ne
 * treba da znaju da je riječ o korpici. `korpiceApi` vraća olakšani `Korpica`
 * oblik (bez kategorije/proizvođača — oni postoje samo kao placeholder u bazi,
 * da zadovolje kolonu), pa se ovdje popunjava `Product` oblik koji
 * `useCart().add` stvarno traži. Polja kategorije/proizvođača su ovdje praznine:
 * `CartItem` ih ne nosi i korpa/checkout ih nigdje ne čitaju (vidi
 * `lib/cart/cart-context.tsx`) — jedino productId i quantity putuju do narudžbe.
 */
export function korpicaKaoProizvod(korpica: Korpica): Product {
  return {
    id: korpica.id,
    name: korpica.naziv,
    slug: korpica.slug,
    price: korpica.cijena,
    unit: "kom",
    stock: korpica.stock,
    isAvailable: korpica.dostupna,
    categoryId: "",
    categoryName: "",
    categorySlug: "",
    producerId: "",
    imageUrl: korpica.imageUrl,
    description: korpica.opis,
  };
}
