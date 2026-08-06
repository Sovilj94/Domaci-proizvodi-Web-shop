"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Pencil, Plus, Trash2, X } from "lucide-react";
import { SmartImage } from "@/components/ui/smart-image";
import { Button } from "@/components/ui/button";
import { LoadingBlock } from "@/components/ui/spinner";
import { CategoryForm } from "@/components/admin/category-form";
import { useToast } from "@/components/ui/toast";
import { adminApi, catalogApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import type { Category } from "@/types/domain";

export default function AdminCategoriesPage() {
  const qc = useQueryClient();
  const { show } = useToast();
  const [editing, setEditing] = useState<Category | null>(null);
  const [open, setOpen] = useState(false);
  const [confirmId, setConfirmId] = useState<string | null>(null);

  const categories = useQuery({
    queryKey: ["categories"],
    queryFn: () => catalogApi.categories(),
  });

  const remove = useMutation({
    mutationFn: (id: string) => adminApi.deleteCategory(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["categories"] });
      setConfirmId(null);
      show("Kategorija obrisana");
    },
    onError: (err) => {
      setConfirmId(null);
      show(
        err instanceof ApiRequestError ? err.message : "Brisanje nije uspjelo",
        "error",
      );
    },
  });

  function openNew() {
    setEditing(null);
    setOpen(true);
  }
  function openEdit(c: Category) {
    setEditing(c);
    setOpen(true);
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="font-display text-2xl font-bold">Kategorije</h1>
          <p className="text-muted">Organizacija kataloga i redoslijed prikaza.</p>
        </div>
        <Button onClick={openNew}>
          <Plus className="h-4 w-4" /> Nova kategorija
        </Button>
      </div>

      {categories.isLoading ? (
        <LoadingBlock />
      ) : (
        <div className="overflow-hidden rounded-card bg-white ring-1 ring-black/5">
          <ul className="divide-y divide-black/5">
            {categories.data?.map((c) => {
              const inUse = (c.productCount ?? 0) > 0;
              const confirming = confirmId === c.id;
              return (
                <li key={c.id} className="flex items-center gap-4 p-3">
                  <div className="relative h-14 w-14 shrink-0 overflow-hidden rounded-xl bg-sand">
                    <SmartImage src={c.imageUrl} alt={c.name} sizes="56px" />
                  </div>
                  <div className="min-w-0 flex-1">
                    <p className="truncate font-medium">{c.name}</p>
                    <p className="truncate text-xs text-muted">
                      /{c.slug} · redoslijed {c.sortOrder}
                    </p>
                  </div>
                  <div className="hidden w-24 text-right sm:block">
                    <p className="font-semibold">{c.productCount ?? 0}</p>
                    <p className="text-xs text-muted">proizvoda</p>
                  </div>

                  {confirming ? (
                    <div className="flex items-center gap-2">
                      <Button
                        size="sm"
                        variant="danger"
                        onClick={() => remove.mutate(c.id)}
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
                        onClick={() => openEdit(c)}
                        className="grid h-9 w-9 shrink-0 place-items-center rounded-full text-muted transition hover:bg-sand hover:text-ink"
                        aria-label="Uredi"
                      >
                        <Pencil className="h-4 w-4" />
                      </button>
                      <button
                        onClick={() => setConfirmId(c.id)}
                        disabled={inUse}
                        title={
                          inUse
                            ? "Kategorija sadrži proizvode — ne može se obrisati"
                            : "Obriši kategoriju"
                        }
                        className="grid h-9 w-9 shrink-0 place-items-center rounded-full text-muted transition hover:bg-red-50 hover:text-red-600 disabled:cursor-not-allowed disabled:opacity-30 disabled:hover:bg-transparent disabled:hover:text-muted"
                        aria-label="Obriši"
                      >
                        <Trash2 className="h-4 w-4" />
                      </button>
                    </>
                  )}
                </li>
              );
            })}
          </ul>
        </div>
      )}

      <CategoryForm open={open} onClose={() => setOpen(false)} category={editing} />
    </div>
  );
}
