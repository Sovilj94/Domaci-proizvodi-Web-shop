import { useQuery } from "@tanstack/react-query";
import { accountApi, adminApi, analizeApi, catalogApi, deliveryApi, korpiceApi, orderApi, receptiApi, utisciApi, type ProductQuery } from "./endpoints";
import type { SekcijaSadrzaja } from "@/types/domain";

export const queryKeys = {
  products: (q: ProductQuery) => ["products", q] as const,
  product: (slug: string) => ["product", slug] as const,
  categories: () => ["categories"] as const,
  producers: () => ["producers"] as const,
  producer: (id: string) => ["producer", id] as const,
  zones: () => ["delivery", "zones"] as const,
  slots: () => ["delivery", "slots"] as const,
  myOrders: () => ["orders", "mine"] as const,
  adrese: () => ["account", "adrese"] as const,
  order: (id: string) => ["order", id] as const,
  trackOrder: (token: string) => ["order", "track", token] as const,
  korpiceLista: () => ["korpice", "lista", undefined] as const,
  sedmica: () => ["korpice", "sedmica"] as const,
  sadrzajSajta: () => ["sadrzaj-sajta"] as const,
  blokoviSadrzaja: () => ["sadrzaj-sajta", "blokovi"] as const,
  podesavanja: () => ["admin", "podesavanja"] as const,
  mojeKorpice: () => ["account", "korpice"] as const,
  mojaKorpica: (id: string) => ["account", "korpica", id] as const,
  pretplate: () => ["account", "pretplate"] as const,
  mojiPoeni: () => ["account", "poeni"] as const,
  utisci: () => ["utisci"] as const,
  adminUtisci: () => ["admin", "utisci"] as const,
  adminPromoKodovi: () => ["admin", "promo-kodovi"] as const,
  adminOcjene: (ocjena?: number) => ["admin", "ocjene", ocjena ?? "sve"] as const,
  recepti: () => ["recepti"] as const,
  recept: (slug: string) => ["recepti", slug] as const,
  adminRecepti: () => ["admin", "recepti"] as const,
  analize: (producer?: string) => ["analize", producer ?? "sve"] as const,
  adminAnalize: () => ["admin", "analize"] as const,
  adminPartneri: (status?: string) => ["admin", "partneri", status ?? "sve"] as const,
};

export function useProducts(query: ProductQuery = {}) {
  return useQuery({
    queryKey: queryKeys.products(query),
    queryFn: () => catalogApi.products(query),
  });
}

export function useProduct(slug: string) {
  return useQuery({
    queryKey: queryKeys.product(slug),
    queryFn: () => catalogApi.product(slug),
    enabled: !!slug,
  });
}

export function useCategories() {
  return useQuery({
    queryKey: queryKeys.categories(),
    queryFn: () => catalogApi.categories(),
    staleTime: 5 * 60_000,
  });
}

export function useProducers() {
  return useQuery({
    queryKey: queryKeys.producers(),
    queryFn: () => catalogApi.producers(),
    staleTime: 5 * 60_000,
  });
}

export function useProducer(id: string) {
  return useQuery({
    queryKey: queryKeys.producer(id),
    queryFn: () => catalogApi.producer(id),
    enabled: !!id,
    retry: false, // 404 je odgovor, ne kvar mreže — ne ponavljati
  });
}

export function useDeliveryZones() {
  return useQuery({
    queryKey: queryKeys.zones(),
    queryFn: () => deliveryApi.zones(),
    staleTime: 5 * 60_000,
  });
}

export function useDeliverySlots() {
  return useQuery({
    queryKey: queryKeys.slots(),
    queryFn: () => deliveryApi.slots(),
    staleTime: 5 * 60_000,
  });
}

/**
 * Cijela lista korpica (bez filtera po tipu) — za potrošače koji grade spojeni katalog
 * (`products ∪ korpice`), ne za `/korpice` samu stranicu (ona filtrira po tipu, vidi
 * `useQuery(["korpice", "lista", tip])` tamo). Dijeljeno mjesto za ovaj poziv je bitno:
 * `/products` ne sadrži korpice (vidi backend `GetProductsHandler`), pa svaki novi
 * potrošač koji ovo zaboravi da spoji tiho gubi svaku korpicu iz sačuvane korpe
 * (vidi CLAUDE.md) — bio je dupliran u checkout-u i admin "Nova narudžba" ekranu.
 */
export function useKorpiceList() {
  return useQuery({
    queryKey: queryKeys.korpiceLista(),
    queryFn: () => korpiceApi.lista(),
  });
}

export function useMyOrders(enabled: boolean) {
  return useQuery({
    queryKey: queryKeys.myOrders(),
    queryFn: () => orderApi.mine(),
    enabled,
  });
}

export function useMyAddresses(enabled: boolean) {
  return useQuery({
    queryKey: ["orders", "addresses"],
    queryFn: () => orderApi.addresses(),
    enabled,
  });
}

/** Permanent tracking link from the order email — no retry loop on a 404, the token
 * either resolves to an order or it doesn't. */
export function useTrackOrder(token: string) {
  return useQuery({
    queryKey: queryKeys.trackOrder(token),
    queryFn: () => orderApi.track(token),
    enabled: !!token,
    retry: false,
  });
}

/** Objavljeni utisci za početnu. Greška nije kritična — sekcija se tada ne prikazuje. */
export function useUtisci() {
  return useQuery({
    queryKey: queryKeys.utisci(),
    queryFn: () => utisciApi.lista(),
    staleTime: 5 * 60_000,
  });
}

/**
 * Tekuća sedmica i rok. Isti ključ koriste i `/korpice` i `/korpice/[slug]` (svojim
 * inline upitom), pa TanStack spaja zahtjeve u jedan.
 */
export function useSedmica() {
  return useQuery({
    queryKey: queryKeys.sedmica(),
    queryFn: korpiceApi.sedmica,
    staleTime: 5 * 60 * 1000,
  });
}

/** Kontakt i česta pitanja iz admin panela. Polje koje nije uneseno se ne prikazuje. */
export function useSadrzajSajta() {
  return useQuery({
    queryKey: queryKeys.sadrzajSajta(),
    queryFn: korpiceApi.sadrzajSajta,
    staleTime: 10 * 60 * 1000,
  });
}

export function useRecepti() {
  return useQuery({
    queryKey: queryKeys.recepti(),
    queryFn: () => receptiApi.lista(),
  });
}

export function useRecept(slug: string) {
  return useQuery({
    queryKey: queryKeys.recept(slug),
    queryFn: () => receptiApi.recept(slug),
    enabled: !!slug,
    retry: false, // 404 je odgovor, ne kvar mreže
  });
}

/** Objavljene analize; `producer` sužava na jednog domaćina. */
export function useAnalize(producer?: string) {
  return useQuery({
    queryKey: queryKeys.analize(producer),
    queryFn: () => analizeApi.lista(producer),
  });
}

export function useMojiPoeni(enabled: boolean) {
  return useQuery({
    queryKey: queryKeys.mojiPoeni(),
    queryFn: () => accountApi.poeni(),
    enabled,
  });
}

/**
 * Blokovi jedne sekcije sajta, redom. Sve sekcije stižu jednim pozivom i dijele keš; sekcija bez
 * blokova je prazna lista — komponenta se tada ne prikazuje (sajt nema svoj ugrađeni tekst).
 */
export function useBlokovi(sekcija: SekcijaSadrzaja) {
  const q = useQuery({
    queryKey: queryKeys.blokoviSadrzaja(),
    queryFn: () => korpiceApi.blokoviSadrzaja(),
    staleTime: 10 * 60 * 1000,
  });
  return { ...q, blokovi: (q.data ?? []).filter((b) => b.sekcija === sekcija) };
}

/** Poslovna pravila iz baze (samo admin) — npr. minimalna marža za boju marže. */
export function usePodesavanja() {
  return useQuery({ queryKey: queryKeys.podesavanja(), queryFn: adminApi.podesavanja, staleTime: 5 * 60 * 1000 });
}
