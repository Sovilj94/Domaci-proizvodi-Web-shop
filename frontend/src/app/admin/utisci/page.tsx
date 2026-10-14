"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Pencil, Plus, Trash2, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { LoadingBlock } from "@/components/ui/spinner";
import { UtisakForm } from "@/components/admin/utisak-form";
import { useToast } from "@/components/ui/toast";
import { adminApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import { queryKeys } from "@/lib/api/hooks";
import type { Utisak } from "@/types/domain";

export default function AdminUtisciPage() {
  const qc = useQueryClient();
  const { show } = useToast();
  const [editing, setEditing] = useState<Utisak | null>(null);
  const [open, setOpen] = useState(false);
  const [confirmId, setConfirmId] = useState<string | null>(null);

  const utisci = useQuery({
    queryKey: queryKeys.adminUtisci(),
    queryFn: () => adminApi.utisci(),
  });

  const remove = useMutation({
    mutationFn: (id: string) => adminApi.deleteUtisak(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.adminUtisci() });
      qc.invalidateQueries({ queryKey: queryKeys.utisci() });
      setConfirmId(null);
      show("Utisak obrisan");
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
          <h1 className="font-display text-2xl font-bold">Utisci</h1>
          <p className="text-muted">Šta kupci kažu — prikazuje se na početnoj, samo objavljeni.</p>
        </div>
        <Button
          onClick={() => {
            setEditing(null);
            setOpen(true);
          }}
        >
          <Plus className="h-4 w-4" /> Novi utisak
        </Button>
      </div>

      {utisci.isLoading ? (
        <LoadingBlock />
      ) : !utisci.data?.length ? (
        <p className="rounded-card bg-white p-6 text-muted shadow-card">
          Još nema utisaka. Dok nijedan nije objavljen, sekcija se na početnoj ne prikazuje.
        </p>
      ) : (
        <div className="overflow-hidden rounded-card bg-white shadow-card">
          <ul className="divide-y divide-line">
            {utisci.data.map((u) => {
              const confirming = confirmId === u.id;
              return (
                <li key={u.id} className="flex items-start gap-4 p-4">
                  <div className="min-w-0 flex-1">
                    <p className="font-medium">
                      {u.ime}
                      {u.mjesto && <span className="font-normal text-muted"> · {u.mjesto}</span>}
                      <span
                        className={
                          u.objavljen
                            ? "ml-2 rounded-full bg-brand-50 px-2 py-0.5 text-xs font-medium text-brand-700"
                            : "ml-2 rounded-full bg-sand px-2 py-0.5 text-xs font-medium text-ink/80"
                        }
                      >
                        {u.objavljen ? "Objavljen" : "Skriven"}
                      </span>
                    </p>
                    <p className="mt-1 line-clamp-2 text-sm text-muted">{u.tekst}</p>
                    <p className="mt-1 text-xs text-muted">Redoslijed: {u.redoslijed}</p>
                  </div>

                  {confirming ? (
                    <div className="flex items-center gap-2">
                      <Button size="sm" variant="danger" onClick={() => remove.mutate(u.id)} disabled={remove.isPending}>
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
                          setEditing(u);
                          setOpen(true);
                        }}
                        className="grid h-9 w-9 shrink-0 place-items-center rounded-full text-muted transition hover:bg-sand hover:text-ink"
                        aria-label="Uredi"
                      >
                        <Pencil className="h-4 w-4" />
                      </button>
                      <button
                        onClick={() => setConfirmId(u.id)}
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

      <UtisakForm open={open} onClose={() => setOpen(false)} utisak={editing} />
    </div>
  );
}
