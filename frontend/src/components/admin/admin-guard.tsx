"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth/auth-context";
import { LoadingBlock } from "@/components/ui/spinner";

export function AdminGuard({ children }: { children: React.ReactNode }) {
  const { loading, isAdmin } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (!loading && !isAdmin) router.replace("/login");
  }, [loading, isAdmin, router]);

  if (loading) {
    return (
      <div className="grid min-h-screen place-items-center">
        <LoadingBlock label="Provjera pristupa…" />
      </div>
    );
  }
  if (!isAdmin) return null;
  return <>{children}</>;
}
