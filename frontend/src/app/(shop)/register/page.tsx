"use client";

import { useState } from "react";
import Link from "next/link";
import { MailCheck } from "lucide-react";
import { Logo } from "@/components/layout/logo";
import { Button } from "@/components/ui/button";
import { Field, Input, Select } from "@/components/ui/field";
import { useAuth } from "@/lib/auth/auth-context";
import { useDeliveryZones } from "@/lib/api/hooks";
import { authApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";

export default function RegisterPage() {
  const { register } = useAuth();
  const zones = useDeliveryZones();
  const [form, setForm] = useState({
    name: "",
    email: "",
    phone: "",
    password: "",
    address: "",
    deliveryZoneId: "",
  });
  const [errors, setErrors] = useState<Record<string, string[]>>({});
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [sentTo, setSentTo] = useState<string | null>(null);
  const [resent, setResent] = useState(false);

  const set = (key: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) =>
    setForm((f) => ({ ...f, [key]: e.target.value }));

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setLoading(true);
    setError(null);
    setErrors({});
    try {
      const res = await register({
        name: form.name,
        email: form.email,
        phone: form.phone,
        password: form.password,
        address: form.address || undefined,
        deliveryZoneId: form.deliveryZoneId || undefined,
      });
      setSentTo(res.email);
    } catch (err) {
      if (err instanceof ApiRequestError) {
        setErrors(err.fieldErrors);
        if (Object.keys(err.fieldErrors).length === 0) setError(err.message);
      } else {
        setError("Došlo je do greške.");
      }
    } finally {
      setLoading(false);
    }
  }

  async function resend() {
    if (!sentTo) return;
    setResent(false);
    try {
      await authApi.resendConfirmation(sentTo);
    } finally {
      setResent(true);
    }
  }

  if (sentTo) {
    return (
      <div className="mx-auto flex max-w-md flex-col items-center px-4 py-16">
        <Logo />
        <div className="mt-6 w-full rounded-card bg-white p-8 text-center ring-1 ring-black/5">
          <div className="mx-auto grid h-14 w-14 place-items-center rounded-full bg-brand-100 text-brand-600">
            <MailCheck className="h-7 w-7" />
          </div>
          <h1 className="mt-5 font-display text-2xl font-bold">Potvrdite email adresu</h1>
          <p className="mt-2 text-sm text-muted">
            Poslali smo link za potvrdu na <span className="font-semibold text-ink">{sentTo}</span>.
            Otvorite mejl i kliknite link da aktivirate nalog i prijavite se.
          </p>

          {resent ? (
            <p className="mt-5 rounded-lg bg-brand-50 px-3 py-2 text-sm font-medium text-brand-700">
              Poslali smo novi link — provjerite email.
            </p>
          ) : (
            <p className="mt-5 text-sm text-muted">
              Niste dobili mejl?{" "}
              <button
                type="button"
                onClick={resend}
                className="font-semibold text-brand-700 hover:underline"
              >
                Pošalji ponovo
              </button>
            </p>
          )}

          <Link
            href="/login"
            className="mt-6 inline-block text-sm font-semibold text-brand-700 hover:underline"
          >
            Nazad na prijavu
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div className="mx-auto flex max-w-md flex-col items-center px-4 py-14">
      <Logo />
      <div className="mt-6 w-full rounded-card bg-white p-7 ring-1 ring-black/5">
        <h1 className="font-display text-2xl font-bold">Registracija</h1>
        <p className="mt-1 text-sm text-muted">Otvorite nalog i pratite svoje narudžbe.</p>

        <form onSubmit={submit} className="mt-6 space-y-4">
          <Field label="Ime i prezime" required error={errors.name?.[0]}>
            <Input value={form.name} onChange={set("name")} required />
          </Field>
          <Field label="Email" required error={errors.email?.[0]}>
            <Input type="email" value={form.email} onChange={set("email")} required />
          </Field>
          <Field label="Telefon" required error={errors.phone?.[0]}>
            <Input value={form.phone} onChange={set("phone")} placeholder="+387 6x xxx xxx" required />
          </Field>
          <Field label="Lozinka" required error={errors.password?.[0]}>
            <Input
              type="password"
              value={form.password}
              onChange={set("password")}
              placeholder="najmanje 6 znakova"
              required
            />
          </Field>
          <Field label="Adresa (opcionalno)">
            <Input value={form.address} onChange={set("address")} />
          </Field>
          <Field label="Zona dostave (opcionalno)">
            <Select value={form.deliveryZoneId} onChange={set("deliveryZoneId")}>
              <option value="">Odaberite kasnije…</option>
              {zones.data?.map((z) => (
                <option key={z.id} value={z.id}>
                  {z.name}
                </option>
              ))}
            </Select>
          </Field>

          {error && (
            <p className="rounded-lg bg-red-50 px-3 py-2 text-sm font-medium text-red-700">
              {error}
            </p>
          )}
          <Button type="submit" size="lg" className="w-full" disabled={loading}>
            {loading ? "Kreiram nalog…" : "Registruj se"}
          </Button>
        </form>

        <p className="mt-5 text-center text-sm text-muted">
          Već imate nalog?{" "}
          <Link href="/login" className="font-semibold text-brand-700 hover:underline">
            Prijavite se
          </Link>
        </p>
      </div>
    </div>
  );
}
