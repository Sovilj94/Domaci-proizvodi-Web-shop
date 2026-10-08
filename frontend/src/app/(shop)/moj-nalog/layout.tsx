"use client";

import Link from "next/link";
import { useAuth } from "@/lib/auth/auth-context";
import { NalogMobileBar, NalogSidebar } from "@/components/nalog/nalog-nav";
import { LoadingBlock } from "@/components/ui/spinner";
import { EmptyState } from "@/components/ui/empty-state";
import { buttonVariants } from "@/components/ui/button";
import { cn } from "@/lib/utils";

/**
 * Straža stoji ovdje, jednom — ne u svakoj od četiri stranice ispod. Neprijavljenom
 * se nudi prijava, bez `router.replace`: redirekcija bi zaboravila gdje je mušterija
 * htjela da ide (vidi `admin-guard.tsx` za suprotan, namjerno drugačiji pristup
 * koji ovdje ne pratimo — admin nema razloga da pamti "odakle je mušterija došla").
 */
export default function NalogLayout({ children }: { children: React.ReactNode }) {
  const { user, loading } = useAuth();

  if (loading) return <LoadingBlock />;

  if (!user) {
    return (
      <div className="mx-auto max-w-2xl px-4 py-12">
        <EmptyState
          title="Prijavi se da vidiš svoj nalog"
          description="Tvoji podaci, adrese i narudžbe su na jednom mjestu — čim se prijaviš."
          action={
            <Link href="/login" className={cn(buttonVariants())}>
              Prijavi se
            </Link>
          }
        />
      </div>
    );
  }

  return (
    <div className="mx-auto flex w-full max-w-6xl gap-8 px-4 py-8">
      <NalogSidebar />
      <div className="flex min-w-0 flex-1 flex-col">
        <NalogMobileBar />
        <main className="min-w-0 flex-1">{children}</main>
      </div>
    </div>
  );
}
