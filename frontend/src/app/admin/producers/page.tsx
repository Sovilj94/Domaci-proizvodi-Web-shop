"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Pencil, Plus, Trash2, X } from "lucide-react";
import { SmartImage } from "@/components/ui/smart-image";
import { Button } from "@/components/ui/button";
import { LoadingBlock } from "@/components/ui/spinner";
import { ProducerForm } from "@/components/admin/producer-form";
import { useToast } from "@/components/ui/toast";
import { adminApi, catalogApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import type { Producer } from "@/types/domain";

export default function AdminProducersPage() {
  const qc = useQueryClient();
  const { show } = useToast();
  const [editing, setEditing] = useState<Producer | null>(null);
  const [open, setOpen] = useState(false);
  const [confirmId, setConfirmId] = useState<string | null>(null);

  const producers = useQuery({
    queryKey: ["producers"],
    queryFn: () => catalogApi.producers(),
  });

  const remove = useMutation({
    mutationFn: (id: string) => adminApi.deleteProducer(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["producers"] });
      setConfirmId(null);
      show("Proizvođač obrisan");
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
  function openEdit(p: Producer) {
    setEditing(p);
    setOpen(true);
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="font-display text-2xl font-bold">Proizvođači</h1>
          <p className="text-muted">Domaćinstva i porodice sa Manjače.</p>
        </div>
        <Button onClick={openNew}>
          <Plus className="h-4 w-4" /> Novi proizvođač
        </Button>
      </div>

      {producers.isLoading ? (
        <LoadingBlock />
      ) : (
        <div className="overflow-hidden rounded-card bg-white shadow-card">
          <ul className="divide-y divide-line">
            {producers.data?.map((p) => {
              const confirming = confirmId === p.id;
              return (
                <li key={p.id} className="flex items-center gap-4 p-3">
                  <div className="relative h-14 w-14 shrink-0 overflow-hidden rounded-full bg-sand">
                    <SmartImage src={p.photoUrl} alt={p.name} sizes="56px" />
                  </div>
                  <div className="min-w-0 flex-1">
                    <p className="truncate font-medium">{p.name}</p>
                    <p className="truncate text-xs text-muted">
                      {p.village}
                      {p.phone ? ` · ${p.phone}` : ""}
                    </p>
                  </div>

                  {confirming ? (
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
                        title="Obriši proizvođača"
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

      <ProducerForm open={open} onClose={() => setOpen(false)} producer={editing} />
    </div>
  );
}
