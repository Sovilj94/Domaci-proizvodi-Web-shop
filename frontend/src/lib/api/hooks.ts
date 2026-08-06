import { useQuery } from "@tanstack/react-query";
import { catalogApi, deliveryApi, orderApi, type ProductQuery } from "./endpoints";

export const queryKeys = {
  products: (q: ProductQuery) => ["products", q] as const,
  product: (slug: string) => ["product", slug] as const,
  categories: () => ["categories"] as const,
  producers: () => ["producers"] as const,
  producer: (id: string) => ["producer", id] as const,
  zones: () => ["delivery", "zones"] as const,
  slots: () => ["delivery", "slots"] as const,
  myOrders: () => ["orders", "mine"] as const,
  order: (id: string) => ["order", id] as const,
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
