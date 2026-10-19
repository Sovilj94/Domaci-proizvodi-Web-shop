"use client";

import { Suspense, useState } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { MailCheck } from "lucide-react";
import { Logo } from "@/components/layout/logo";
import { SocialLogin } from "@/components/auth/social-login";
import { Button } from "@/components/ui/button";
import { Field, Input, Select } from "@/components/ui/field";
import { LoadingBlock } from "@/components/ui/spinner";
import { useAuth } from "@/lib/auth/auth-context";
import { useDeliveryZones } from "@/lib/api/hooks";
import { authApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";

// useSearchParams() opts the page out of static generation unless it sits below a Suspense
// boundary — without this wrapper `next build` fails at the prerender step for /register.
export default function RegisterPage() {
  return (
    <Suspense fallback={<LoadingBlock />}>
      <RegisterForm />
    </Suspense>
  );
}

function RegisterForm() {
  const { register } = useAuth();
  const zones = useDeliveryZones();
  const searchParams = useSearchParams();

  // Only an internal path is a safe redirect target — an open `next` is an invitation
  // to bounce a customer to another site. Registration itself doesn't log anyone in (email
  // confirmation is required first), so `next` rides along on the links back to login instead
  // of an immediate redirect.
  // Lični kod ima tačno 8 znakova. Duži ili okrnjen parametar iz linka bi server odbio
  // greškom polja koje forma nema, pa bi registracija tiho pala — takav kod se ne šalje.
  const sirovaPreporuka = searchParams.get("preporuka")?.trim().toUpperCase() ?? "";
  const preporuka = /^[A-Z0-9]{8}$/.test(sirovaPreporuka) ? sirovaPreporuka : null;
  const next = searchParams.get("next");
  const safeNext = next && next.startsWith("/") && !next.startsWith("//") ? next : null;
  const loginHref = safeNext ? `/login?next=${encodeURIComponent(safeNext)}` : "/login";
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
        preporukaKod: preporuka ?? undefined,
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
        <div className="mt-6 w-full rounded-card bg-surface p-8 text-center shadow-card">
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
            href={loginHref}
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
      <div className="mt-6 w-full rounded-card bg-surface p-7 shadow-card">
        <h1 className="font-display text-2xl font-bold">Registracija</h1>
        <p className="mt-1 text-sm text-muted">Otvorite nalog i pratite svoje narudžbe.</p>
        {preporuka && (
          <p className="mt-4 rounded-lg bg-brand-50 px-3 py-2 text-sm font-medium text-brand-700">
            Preporučio te prijatelj — kad primiš prvu isporuku, dobijaš poene na nalogu.
          </p>
        )}

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

        {/* B5: i preko Googlea/Facebooka — kod preporuke iz linka ide uz prijavu. */}
        <div className="mt-6">
          <SocialLogin preporukaKod={preporuka ?? undefined} />
        </div>

        <p className="mt-5 text-center text-sm text-muted">
          Već imate nalog?{" "}
          <Link href={loginHref} className="font-semibold text-brand-700 hover:underline">
            Prijavite se
          </Link>
        </p>
      </div>
    </div>
  );
}
