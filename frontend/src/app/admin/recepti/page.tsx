"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Pencil, Plus, Trash2, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { LoadingBlock } from "@/components/ui/spinner";
import { ReceptForm } from "@/components/admin/recept-form";
import { useToast } from "@/components/ui/toast";
import { adminApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import { queryKeys } from "@/lib/api/hooks";
import type { ReceptAdmin } from "@/types/domain";

export default function AdminReceptiPage() {
  const qc = useQueryClient();
  const { show } = useToast();
  const [editing, setEditing] = useState<ReceptAdmin | null>(null);
  const [open, setOpen] = useState(false);
  const [confirmId, setConfirmId] = useState<string | null>(null);

  const recepti = useQuery({
    queryKey: queryKeys.adminRecepti(),
    queryFn: () => adminApi.recepti(),
  });

  const remove = useMutation({
    mutationFn: (id: string) => adminApi.deleteRecept(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.adminRecepti() });
      qc.invalidateQueries({ queryKey: ["recepti"] });
      setConfirmId(null);
      show("Recept obrisan");
    },
    onError: (err) => {
      setConfirmId(null);
      show(err instanceof ApiRequestError ? err.message : "Brisanje nije uspjelo", "error");
    },
  });

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="font-display text-2xl font-bold">Recepti</h1>
          <p className="text-muted">Recepti sa sastojcima vezanim za ponudu — javno se vide samo objavljeni.</p>
        </div>
        <Button
          onClick={() => {
            setEditing(null);
            setOpen(true);
          }}
        >
          <Plus className="h-4 w-4" /> Novi recept
        </Button>
      </div>

      {recepti.isLoading ? (
        <LoadingBlock />
      ) : !recepti.data?.length ? (
        <p className="rounded-card bg-white p-6 text-muted shadow-card">Još nema recepata.</p>
      ) : (
        <div className="overflow-hidden rounded-card bg-white shadow-card">
          <ul className="divide-y divide-line">
            {recepti.data.map((r) => {
              const confirming = confirmId === r.id;
              return (
                <li key={r.id} className="flex items-start gap-4 p-4">
                  <div className="min-w-0 flex-1">
                    <p className="font-medium">
                      {r.naslov}
                      <span
                        className={
                          r.objavljen
                            ? "ml-2 rounded-full bg-brand-50 px-2 py-0.5 text-xs font-medium text-brand-700"
                            : "ml-2 rounded-full bg-sand px-2 py-0.5 text-xs font-medium text-ink/80"
                        }
                      >
                        {r.objavljen ? "Objavljen" : "Skriven"}
                      </span>
                    </p>
                    <p className="mt-1 text-xs text-muted">/recepti/{r.slug}</p>
                    <p className="mt-1 text-xs text-muted">Sastojaka: {r.sastojci.length}</p>
                  </div>

                  {confirming ? (
                    <div className="flex items-center gap-2">
                      <Button size="sm" variant="danger" onClick={() => remove.mutate(r.id)} disabled={remove.isPending}>
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
                        onClick={() => {
                          setEditing(r);
                          setOpen(true);
                        }}
                        className="grid h-9 w-9 shrink-0 place-items-center rounded-full text-muted transition hover:bg-sand hover:text-ink"
                        aria-label="Uredi"
                      >
                        <Pencil className="h-4 w-4" />
                      </button>
                      <button
                        onClick={() => setConfirmId(r.id)}
                        className="grid h-9 w-9 shrink-0 place-items-center rounded-full text-muted transition hover:bg-red-50 hover:text-red-600"
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

      <ReceptForm open={open} onClose={() => setOpen(false)} recept={editing} />
    </div>
  );
}
