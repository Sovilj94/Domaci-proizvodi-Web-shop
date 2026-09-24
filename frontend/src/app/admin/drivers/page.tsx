"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Pencil, Plus, Trash2, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { LoadingBlock } from "@/components/ui/spinner";
import { DriverForm } from "@/components/admin/driver-form";
import { useToast } from "@/components/ui/toast";
import { adminApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import type { Driver } from "@/lib/api/contracts";

export default function AdminDriversPage() {
  const qc = useQueryClient();
  const { show } = useToast();
  const [editing, setEditing] = useState<Driver | null>(null);
  const [open, setOpen] = useState(false);
  const [confirmId, setConfirmId] = useState<string | null>(null);

  const drivers = useQuery({
    queryKey: ["admin", "drivers"],
    queryFn: () => adminApi.drivers(),
  });

  const remove = useMutation({
    mutationFn: (id: string) => adminApi.deleteDriver(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin", "drivers"] });
      setConfirmId(null);
      show("Vozač obrisan");
    },
    onError: (err) => {
      setConfirmId(null);
      // Vozač sa rutama vraća 409 driver_in_use — poruka sa servera to objašnjava.
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
  function openEdit(d: Driver) {
    setEditing(d);
    setOpen(true);
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="font-display text-2xl font-bold">Vozači</h1>
          <p className="text-muted">Ko vozi dostavu i kojim vozilom.</p>
        </div>
        <Button onClick={openNew}>
          <Plus className="h-4 w-4" /> Novi vozač
        </Button>
      </div>

      {drivers.isLoading ? (
        <LoadingBlock />
      ) : (
        <div className="overflow-hidden rounded-card bg-white shadow-card">
          <ul className="divide-y divide-line">
            {drivers.data?.map((d) => {
              const confirming = confirmId === d.id;
              const vozilo = [d.vozilo, d.registracija].filter(Boolean).join(" · ");
              return (
                <li key={d.id} className="flex items-center gap-4 p-3">
                  <div className="min-w-0 flex-1">
                    <p className="flex items-center gap-2 truncate font-medium">
                      {d.name}
                      {!d.aktivan && (
                        <span className="rounded-full bg-sand px-2 py-0.5 text-xs font-normal text-muted">
                          neaktivan
                        </span>
                      )}
                    </p>
                    <p className="truncate text-xs text-muted">
                      {d.phone}
                      {vozilo ? ` · ${vozilo}` : ""}
                    </p>
                  </div>

                  {confirming ? (
                    <div className="flex items-center gap-2">
                      <Button
                        size="sm"
                        variant="danger"
                        onClick={() => remove.mutate(d.id)}
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
                        onClick={() => openEdit(d)}
                        className="grid h-9 w-9 shrink-0 place-items-center rounded-full text-muted transition hover:bg-sand hover:text-ink"
                        aria-label="Uredi"
                      >
                        <Pencil className="h-4 w-4" />
                      </button>
                      <button
                        onClick={() => setConfirmId(d.id)}
                        title="Obriši vozača"
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

      <DriverForm open={open} onClose={() => setOpen(false)} driver={editing} />
    </div>
  );
}
