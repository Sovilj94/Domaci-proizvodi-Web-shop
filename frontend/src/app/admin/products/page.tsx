"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Pencil, Plus, Trash2, X } from "lucide-react";
import { SmartImage } from "@/components/ui/smart-image";
import { Button } from "@/components/ui/button";
import { LoadingBlock } from "@/components/ui/spinner";
import { ProductForm } from "@/components/admin/product-form";
import { useToast } from "@/components/ui/toast";
import { adminApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import type { Product } from "@/types/domain";
import { cn, formatPrice, unitSuffix } from "@/lib/utils";

export default function AdminProductsPage() {
  const qc = useQueryClient();
  const { show } = useToast();
  const [editing, setEditing] = useState<Product | null>(null);
  const [open, setOpen] = useState(false);
  const [confirmId, setConfirmId] = useState<string | null>(null);

  const products = useQuery({
    queryKey: ["admin", "products"],
    queryFn: () => adminApi.products(),
  });

  const toggle = useMutation({
    mutationFn: ({ id, isAvailable }: { id: string; isAvailable: boolean }) =>
      adminApi.toggleProduct(id, isAvailable),
    onSuccess: (p) => {
      qc.invalidateQueries({ queryKey: ["admin", "products"] });
      qc.invalidateQueries({ queryKey: ["products"] });
      show(p.isAvailable ? "Proizvod je dostupan" : "Proizvod je skriven");
    },
    onError: (err) => show(err instanceof ApiRequestError ? err.message : "Promjena nije sačuvana", "error"),
  });

  const remove = useMutation({
    mutationFn: (id: string) => adminApi.deleteProduct(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin", "products"] });
      qc.invalidateQueries({ queryKey: ["products"] });
      qc.invalidateQueries({ queryKey: ["categories"] });
      setConfirmId(null);
      show("Proizvod obrisan");
    },
    onError: (err) => {
      setConfirmId(null);
      show(err instanceof ApiRequestError ? err.message : "Brisanje nije uspjelo", "error");
    },
  });

  function openNew() {
    setEditing(null);
    setOpen(true);
  }
  function openEdit(p: Product) {
    setEditing(p);
    setOpen(true);
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="font-display text-2xl font-bold">Proizvodi</h1>
          <p className="text-muted">Katalog i dostupnost.</p>
        </div>
        <Button onClick={openNew}>
          <Plus className="h-4 w-4" /> Novi proizvod
        </Button>
      </div>

      {products.isLoading ? (
        <LoadingBlock />
      ) : (
        <div className="overflow-hidden rounded-card bg-white shadow-card">
          <ul className="divide-y divide-line">
            {products.data?.map((p) => (
              <li key={p.id} className="flex items-center gap-4 p-3">
                <div className="relative h-14 w-14 shrink-0 overflow-hidden rounded-xl bg-sand">
                  <SmartImage src={p.imageUrl} alt={p.name} sizes="56px" />
                </div>
                <div className="min-w-0 flex-1">
                  <div className="flex items-center gap-2">
                    <p className="truncate font-medium">{p.name}</p>
                    {p.isFeatured && (
                      <span className="rounded bg-honey-100 px-1.5 py-0.5 text-[10px] font-bold text-honey-600">
                        izdvojeno
                      </span>
                    )}
                  </div>
                  <p className="truncate text-xs text-muted">
                    {p.categoryName} · {p.producer?.name}
                  </p>
                </div>
                <div className="hidden w-24 text-right sm:block">
                  <p className="font-semibold">{formatPrice(p.price)}</p>
                  <p className="text-xs text-muted">{unitSuffix(p.unit)} · {p.stock} kom</p>
                </div>

                <button
                  onClick={() => toggle.mutate({ id: p.id, isAvailable: !p.isAvailable })}
                  disabled={toggle.isPending}
                  className={cn(
                    "relative h-6 w-11 shrink-0 rounded-full transition disabled:opacity-50",
                    p.isAvailable ? "bg-brand-500" : "bg-zinc-300",
                  )}
                  aria-label="Prebaci dostupnost"
                >
                  <span
                    className={cn(
                      "absolute top-0.5 h-5 w-5 rounded-full bg-white transition",
                      p.isAvailable ? "left-[22px]" : "left-0.5",
                    )}
                  />
                </button>

                {confirmId === p.id ? (
                  <div className="flex items-center gap-2">
                    <Button
                      size="sm"
                      variant="danger"
                      onClick={() => remove.mutate(p.id)}
                      disabled={remove.isPending}
                    >
                      Obriši
                    </Button>
                    <button
                      onClick={() => setConfirmId(null)}
                      className="grid h-9 w-9 shrink-0 place-items-center rounded-full text-muted transition hover:bg-sand hover:text-ink"
                      aria-label="Otkaži"
                    >
                      <X className="h-4 w-4" />
                    </button>
                  </div>
                ) : (
                  <>
                    <button
                      onClick={() => openEdit(p)}
                      className="grid h-9 w-9 shrink-0 place-items-center rounded-full text-muted transition hover:bg-sand hover:text-ink"
                      aria-label="Uredi"
                    >
                      <Pencil className="h-4 w-4" />
                    </button>
                    <button
                      onClick={() => setConfirmId(p.id)}
                      title="Obriši proizvod"
                      className="grid h-9 w-9 shrink-0 place-items-center rounded-full text-muted transition hover:bg-red-50 hover:text-red-600"
                      aria-label="Obriši"
                    >
                      <Trash2 className="h-4 w-4" />
                    </button>
                  </>
                )}
              </li>
            ))}
          </ul>
        </div>
      )}

      <ProductForm open={open} onClose={() => setOpen(false)} product={editing} />
    </div>
  );
}
