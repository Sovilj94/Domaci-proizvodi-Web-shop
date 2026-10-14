"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Pencil, Plus, Trash2, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { LoadingBlock } from "@/components/ui/spinner";
import { AnalizaForm } from "@/components/admin/analiza-form";
import { formatirajDatumAnalize } from "@/components/analize/analiza-kartica";
import { useToast } from "@/components/ui/toast";
import { adminApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import { queryKeys } from "@/lib/api/hooks";
import { REZULTAT_ANALIZE_TEKST, type Analiza } from "@/types/domain";

export default function AdminAnalizePage() {
  const qc = useQueryClient();
  const { show } = useToast();
  const [editing, setEditing] = useState<Analiza | null>(null);
  const [open, setOpen] = useState(false);
  const [confirmId, setConfirmId] = useState<string | null>(null);

  const analize = useQuery({
    queryKey: queryKeys.adminAnalize(),
    queryFn: () => adminApi.analize(),
  });

  const remove = useMutation({
    mutationFn: (id: string) => adminApi.deleteAnaliza(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.adminAnalize() });
      qc.invalidateQueries({ queryKey: ["analize"] });
      setConfirmId(null);
      show("Analiza obrisana");
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
          <h1 className="font-display text-2xl font-bold">Analize</h1>
          <p className="text-muted">Laboratorijske analize — javno se vide samo objavljene, i usklađene i neusklađene.</p>
        </div>
        <Button
          onClick={() => {
            setEditing(null);
            setOpen(true);
          }}
        >
          <Plus className="h-4 w-4" /> Nova analiza
        </Button>
      </div>

      {analize.isLoading ? (
        <LoadingBlock />
      ) : !analize.data?.length ? (
        <p className="rounded-card bg-white p-6 text-muted shadow-card">Još nema analiza.</p>
      ) : (
        <div className="overflow-hidden rounded-card bg-white shadow-card">
          <ul className="divide-y divide-line">
            {analize.data.map((a) => {
              const confirming = confirmId === a.id;
              const ok = a.rezultat === "Uskladjeno";
              return (
                <li key={a.id} className="flex items-start gap-4 p-4">
                  <div className="min-w-0 flex-1">
                    <p className="font-medium">
                      {a.uzorak}
                      <span
                        className={
                          ok
                            ? "ml-2 rounded-full bg-brand-50 px-2 py-0.5 text-xs font-medium text-brand-700"
                            : "ml-2 rounded-full bg-red-50 px-2 py-0.5 text-xs font-medium text-red-800"
                        }
                      >
                        {REZULTAT_ANALIZE_TEKST[a.rezultat]}
                      </span>
                      <span
                        className={
                          a.objavljena
                            ? "ml-2 rounded-full bg-brand-50 px-2 py-0.5 text-xs font-medium text-brand-700"
                            : "ml-2 rounded-full bg-sand px-2 py-0.5 text-xs font-medium text-ink/80"
                        }
                      >
                        {a.objavljena ? "Objavljena" : "Skrivena"}
                      </span>
                    </p>
                    <p className="mt-1 text-xs text-muted">
                      {a.proizvodjac}
                      {a.proizvod && ` · ${a.proizvod}`} · {a.laboratorija} · {formatirajDatumAnalize(a.datumUzorkovanja)}
                    </p>
                  </div>

                  {confirming ? (
                    <div className="flex items-center gap-2">
                      <Button size="sm" variant="danger" onClick={() => remove.mutate(a.id)} disabled={remove.isPending}>
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
                          setEditing(a);
                          setOpen(true);
                        }}
                        className="grid h-9 w-9 shrink-0 place-items-center rounded-full text-muted transition hover:bg-sand hover:text-ink"
                        aria-label="Uredi"
                      >
                        <Pencil className="h-4 w-4" />
                      </button>
                      <button
                        onClick={() => setConfirmId(a.id)}
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

      <AnalizaForm open={open} onClose={() => setOpen(false)} analiza={editing} />
    </div>
  );
}
