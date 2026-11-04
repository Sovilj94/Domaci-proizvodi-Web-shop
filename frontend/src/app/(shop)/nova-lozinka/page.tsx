"use client";

import { Suspense, useState } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { Eye, EyeOff, Lock } from "lucide-react";
import { Logo } from "@/components/layout/logo";
import { Button } from "@/components/ui/button";
import { fieldClass } from "@/components/ui/field";
import { LoadingBlock } from "@/components/ui/spinner";
import { useAuth } from "@/lib/auth/auth-context";
import { ApiRequestError } from "@/lib/api/client";
import { cn } from "@/lib/utils";

export default function NovaLozinkaPage() {
  return (
    <Suspense fallback={<LoadingBlock />}>
      <Forma />
    </Suspense>
  );
}

/** Nova lozinka iz linka u mejlu (token važi 1 h, jednokratan). Uspjeh prijavljuje i vodi na nalog. */
function Forma() {
  const router = useRouter();
  const token = useSearchParams().get("token") ?? "";
  const { novaLozinka } = useAuth();
  const [lozinka, setLozinka] = useState("");
  const [ponovo, setPonovo] = useState("");
  const [vidi, setVidi] = useState(false);
  const [greska, setGreska] = useState<string | null>(null);
  const [istekao, setIstekao] = useState(false);
  const [salje, setSalje] = useState(false);

  async function sacuvaj(e: React.FormEvent) {
    e.preventDefault();
    setGreska(null);
    if (lozinka.length < 6) return setGreska("Lozinka mora imati najmanje 6 znakova.");
    if (lozinka !== ponovo) return setGreska("Lozinke se ne poklapaju.");
    setSalje(true);
    try {
      const user = await novaLozinka(token, lozinka);
      router.push(user.role === "admin" ? "/admin/dashboard" : "/moj-nalog/narudzbe");
    } catch (err) {
      if (err instanceof ApiRequestError) {
        setGreska(err.message);
        setIstekao(err.apiError.code === "reset_istekao");
      } else setGreska("Čuvanje nije uspjelo. Pokušaj ponovo.");
    } finally {
      setSalje(false);
    }
  }

  return (
    <div className="mx-auto flex max-w-md flex-col items-center px-4 py-20">
      <Logo />
      <div className="mt-6 w-full rounded-card bg-surface p-8 shadow-card">
        {!token ? (
          <div className="text-center">
            <h1 className="font-display text-2xl font-bold">Link nije potpun</h1>
            <p className="mt-2 text-sm text-muted">Otvori link direktno iz mejla, ili zatraži novi.</p>
            <Link href="/zaboravljena-lozinka" className="mt-6 inline-block text-sm font-semibold text-brand-700 hover:underline">
              Zatraži novi link
            </Link>
          </div>
        ) : (
          <form onSubmit={sacuvaj} className="space-y-4">
            <div>
              <h1 className="font-display text-2xl font-bold">Nova lozinka</h1>
              <p className="mt-1 text-sm text-muted">Najmanje 6 znakova. Poslije čuvanja odmah si prijavljen.</p>
            </div>
            {[
              { v: lozinka, set: setLozinka, label: "Nova lozinka" },
              { v: ponovo, set: setPonovo, label: "Ponovi lozinku" },
            ].map((p, i) => (
              <div key={p.label} className="relative">
                <Lock className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted" />
                <input
                  type={vidi ? "text" : "password"}
                  required
                  autoFocus={i === 0}
                  autoComplete="new-password"
                  value={p.v}
                  onChange={(e) => p.set(e.target.value)}
                  placeholder={p.label}
                  aria-label={p.label}
                  className={cn(fieldClass, "px-9")}
                />
                {i === 0 && (
                  <button
                    type="button"
                    onClick={() => setVidi((x) => !x)}
                    aria-label={vidi ? "Sakrij lozinku" : "Prikaži lozinku"}
                    className="absolute right-2 top-1/2 grid h-7 w-7 -translate-y-1/2 place-items-center rounded-md text-muted transition hover:text-ink"
                  >
                    {vidi ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                  </button>
                )}
              </div>
            ))}
            {greska && <p className="rounded-lg bg-red-50 px-3 py-2 text-sm font-medium text-red-700">{greska}</p>}
            {istekao && (
              <Link href="/zaboravljena-lozinka" className="block text-center text-sm font-semibold text-brand-700 hover:underline">
                Zatraži novi link
              </Link>
            )}
            <Button type="submit" className="w-full" disabled={salje}>
              {salje ? "Čuvam…" : "Sačuvaj lozinku"}
            </Button>
          </form>
        )}
      </div>
    </div>
  );
}
