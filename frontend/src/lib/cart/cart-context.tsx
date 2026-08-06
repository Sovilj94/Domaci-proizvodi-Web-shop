"use client";

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
} from "react";
import type { Product, Unit } from "@/types/domain";

export interface CartItem {
  productId: string;
  slug: string;
  name: string;
  unit: Unit;
  price: number;
  imageUrl?: string;
  stock: number;
  quantity: number;
}

interface CartContextValue {
  items: CartItem[];
  count: number;
  subtotal: number;
  hydrated: boolean;
  add: (product: Product, quantity?: number) => void;
  setQuantity: (productId: string, quantity: number) => void;
  remove: (productId: string) => void;
  clear: () => void;
  /** Drop cart items that no longer exist or aren't available in the current catalog. */
  reconcile: (products: Product[]) => void;
  /** Slide-over cart drawer open state. */
  isOpen: boolean;
  openCart: () => void;
  closeCart: () => void;
}

const CartContext = createContext<CartContextValue | null>(null);
const STORAGE_KEY = "epiljara.cart";

export function CartProvider({ children }: { children: React.ReactNode }) {
  const [items, setItems] = useState<CartItem[]>([]);
  const [hydrated, setHydrated] = useState(false);
  const [isOpen, setIsOpen] = useState(false);

  // Load persisted cart once, on the client.
  useEffect(() => {
    try {
      const raw = window.localStorage.getItem(STORAGE_KEY);
      if (raw) setItems(JSON.parse(raw) as CartItem[]);
    } catch {
      /* ignore corrupt storage */
    }
    setHydrated(true);
  }, []);

  // Persist on change (after hydration, so we never overwrite with []).
  useEffect(() => {
    if (!hydrated) return;
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(items));
  }, [items, hydrated]);

  const add = useCallback((product: Product, quantity = 1) => {
    setItems((prev) => {
      const existing = prev.find((i) => i.productId === product.id);
      if (existing) {
        return prev.map((i) =>
          i.productId === product.id
            ? { ...i, quantity: clamp(i.quantity + quantity, product.stock) }
            : i,
        );
      }
      return [
        ...prev,
        {
          productId: product.id,
          slug: product.slug,
          name: product.name,
          unit: product.unit,
          price: product.price,
          imageUrl: product.imageUrl,
          stock: product.stock,
          quantity: clamp(quantity, product.stock),
        },
      ];
    });
  }, []);

  const setQuantity = useCallback((productId: string, quantity: number) => {
    setItems((prev) =>
      prev
        .map((i) =>
          i.productId === productId ? { ...i, quantity: clamp(quantity, i.stock) } : i,
        )
        .filter((i) => i.quantity > 0),
    );
  }, []);

  const remove = useCallback((productId: string) => {
    setItems((prev) => prev.filter((i) => i.productId !== productId));
  }, []);

  const clear = useCallback(() => setItems([]), []);

  const reconcile = useCallback((products: Product[]) => {
    const available = new Set(products.filter((p) => p.isAvailable).map((p) => p.id));
    setItems((prev) => {
      const next = prev.filter((i) => available.has(i.productId));
      // Same length → nothing pruned → keep the same reference (no re-render / no loop).
      return next.length === prev.length ? prev : next;
    });
  }, []);

  const count = useMemo(() => items.reduce((s, i) => s + i.quantity, 0), [items]);
  const subtotal = useMemo(
    () => Math.round(items.reduce((s, i) => s + i.price * i.quantity, 0) * 100) / 100,
    [items],
  );

  const openCart = useCallback(() => setIsOpen(true), []);
  const closeCart = useCallback(() => setIsOpen(false), []);

  const value = useMemo<CartContextValue>(
    () => ({
      items,
      count,
      subtotal,
      hydrated,
      add,
      setQuantity,
      remove,
      clear,
      reconcile,
      isOpen,
      openCart,
      closeCart,
    }),
    [items, count, subtotal, hydrated, add, setQuantity, remove, clear, reconcile, isOpen, openCart, closeCart],
  );

  return <CartContext.Provider value={value}>{children}</CartContext.Provider>;
}

function clamp(quantity: number, stock: number): number {
  const max = stock > 0 ? stock : 0;
  return Math.max(0, Math.min(quantity, max || quantity));
}

export function useCart(): CartContextValue {
  const ctx = useContext(CartContext);
  if (!ctx) throw new Error("useCart must be used within CartProvider");
  return ctx;
}
