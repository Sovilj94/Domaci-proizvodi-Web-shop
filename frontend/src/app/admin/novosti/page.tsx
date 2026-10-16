"use client";

import { useQuery } from "@tanstack/react-query";
import { Button } from "@/components/ui/button";
import { LoadingBlock } from "@/components/ui/spinner";
import { useToast } from "@/components/ui/toast";
import { adminApi } from "@/lib/api/endpoints";

function datum(iso: string): string {
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? "" : d.toLocaleDateString("sr-Latn-BA");
}

/** Prijave na novosti iz footera (R1). Slanje mejlova nije u aplikaciji — lista se kopira u alat za mejlove. */
export default function AdminNovostiPage() {
  const { show } = useToast();
  const q = useQuery({ queryKey: ["admin", "novosti"], queryFn: adminApi.novosti });

  async function kopiraj() {
    const svi = (q.data ?? []).map((p) => p.email).join(", ");
    try {
      await navigator.clipboard.writeText(svi);
      show("E-mail adrese su kopirane");
    } catch {
      show("Kopiranje nije uspjelo", "error");
    }
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold">Prijave na novosti</h1>
          <p className="text-sm text-muted">Adrese ostavljene u footeru sajta.</p>
        </div>
        <Button onClick={kopiraj} disabled={!q.data?.length}>
          Kopiraj sve e-mailove
        </Button>
      </div>

      {q.isLoading ? (
        <LoadingBlock />
      ) : q.isError ? (
        <p className="text-red-700">Lista nije učitana.</p>
      ) : !q.data?.length ? (
        <p className="text-muted">Još niko nije ostavio adresu.</p>
      ) : (
        <ul className="divide-y divide-line rounded-card border border-line bg-surface">
          {q.data.map((p) => (
            <li key={p.email} className="flex items-center justify-between gap-4 px-4 py-3 text-sm">
              <span className="font-medium">{p.email}</span>
              <span className="text-muted">{datum(p.kreirana)}</span>
            </li>
          ))}
        </ul>
      )}
      <p className="text-sm text-muted">Ukupno: {q.data?.length ?? 0}</p>
    </div>
  );
}
