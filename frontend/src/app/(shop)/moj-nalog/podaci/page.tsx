"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Field, Input } from "@/components/ui/field";
import { useToast } from "@/components/ui/toast";
import { useAuth } from "@/lib/auth/auth-context";
import { ApiRequestError } from "@/lib/api/client";

/**
 * Kartica 1 mijenja ime i telefon; kartica 2 je sivo, samo-za-čitanje objašnjenje
 * zašto se e-mail i lozinka ne mijenjaju ovdje. `azurirajProfil` sam upisuje
 * rezultat u auth kontekst (vidi auth-context.tsx), pa se `user` ovdje osvježi
 * sam i dugme se vrati u disabled stanje bez ikakvog ručnog računanja.
 */
export default function MojNalogPodaciPage() {
  const { user, azurirajProfil } = useAuth();
  const { show } = useToast();

  const [ime, setIme] = useState(user?.name ?? "");
  const [telefon, setTelefon] = useState(user?.phone ?? "");
  const [errors, setErrors] = useState<Record<string, string[]>>({});
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  // Poređenje sa trenutnim `user` (ne sa vrijednostima u trenutku mounta) — zato se
  // dugme sâmo vrati u disabled stanje poslije uspješnog čuvanja, bez posebne
  // "sačuvano" zastavice: azurirajProfil upiše novi user, ime/telefon state i dalje
  // sadrže upravo poslatu vrijednost, pa su jednaki i poređenje ponovo daje false.
  const izmijenjeno = ime !== (user?.name ?? "") || telefon !== (user?.phone ?? "");

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setSaving(true);
    setError(null);
    setErrors({});
    try {
      await azurirajProfil({ ime, telefon });
      show("Podaci su sačuvani.");
    } catch (err) {
      if (err instanceof ApiRequestError) {
        setErrors(err.fieldErrors);
        if (Object.keys(err.fieldErrors).length === 0) setError(err.message);
      } else {
        setError("Došlo je do greške.");
      }
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="space-y-6">
      <div>
        <h1 className="font-display text-2xl font-bold text-ink md:text-3xl">Lični podaci</h1>
        <p className="mt-1 text-muted">Pregledaj i izmijeni svoje podatke.</p>
      </div>

      <div className="rounded-card border border-line bg-surface p-6 shadow-card">
        <h2 className="font-display text-lg font-semibold text-ink">Lični podaci</h2>

        <form onSubmit={submit} className="mt-4 max-w-md space-y-4">
          <Field label="Ime i prezime" required error={errors.ime?.[0]}>
            <Input value={ime} onChange={(e) => setIme(e.target.value)} />
          </Field>

          <Field label="Broj telefona" required error={errors.telefon?.[0]}>
            <Input value={telefon} onChange={(e) => setTelefon(e.target.value)} />
          </Field>
          <p className="-mt-2 text-xs text-muted">Na ovaj broj te zove vozač kad stigne.</p>

          {error && (
            <p className="rounded-lg bg-red-50 px-3 py-2 text-sm font-medium text-red-700">
              {error}
            </p>
          )}

          <div className="flex justify-end">
            <Button type="submit" disabled={!izmijenjeno || saving}>
              {saving ? "Čuvam…" : "Sačuvaj"}
            </Button>
          </div>
        </form>
      </div>

      <div className="rounded-card border border-line bg-surface p-6 shadow-card">
        <h2 className="font-display text-lg font-semibold text-ink">Podaci za prijavu</h2>

        <div className="mt-4 max-w-md space-y-4">
          <Field label="E-mail">
            <Input value={user?.email ?? ""} disabled readOnly />
          </Field>

          <Field label="Lozinka">
            <Input value="••••••••" disabled readOnly type="password" />
          </Field>

          <p className="text-xs text-muted">
            E-mail je i tvoje korisničko ime, pa se mijenja uz ponovnu potvrdu — javi nam se na
            kontakt ako treba.
          </p>
        </div>
      </div>
    </div>
  );
}
