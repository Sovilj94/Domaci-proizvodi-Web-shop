"use client";

import { useEffect } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { LogIn, Mail, ShoppingBasket, UserPlus } from "lucide-react";
import { buttonVariants } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { LoadingBlock } from "@/components/ui/spinner";
import { useAuth } from "@/lib/auth/auth-context";
import { useCart } from "@/lib/cart/cart-context";
import { cn } from "@/lib/utils";

const OPTIONS = [
  {
    href: "/login?next=/checkout",
    icon: LogIn,
    title: "Prijavi se",
    description: "Tvoje adrese i istorija narudžbi su već tu.",
  },
  {
    href: "/register?next=/checkout",
    icon: UserPlus,
    title: "Registruj se",
    description: "Napravi nalog da sljedeći put ide brže.",
  },
  {
    href: "/checkout?gost=1",
    icon: Mail,
    title: "Naruči bez naloga",
    description: "Bez registracije. Poslaćemo ti mejl da potvrdiš narudžbu.",
  },
] as const;

export default function CheckoutNalogPage() {
  const router = useRouter();
  const { user, loading } = useAuth();
  const { items, hydrated } = useCart();

  // A logged-in customer has nothing to choose here — send them straight to checkout.
  useEffect(() => {
    if (!loading && user) router.replace("/checkout");
  }, [loading, user, router]);

  if (loading || user) return <LoadingBlock />;

  if (!hydrated) return <LoadingBlock />;

  if (items.length === 0) {
    return (
      <div className="mx-auto max-w-3xl px-4 py-12">
        <EmptyState
          icon={<ShoppingBasket className="h-7 w-7" />}
          title="Korpa je prazna"
          description="Dodajte proizvode prije nego nastavite na dostavu."
          action={
            <Link href="/products" className={cn(buttonVariants())}>
              Pogledaj proizvode
            </Link>
          }
        />
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-xl px-4 py-10">
      <h1 className="text-center font-display text-2xl font-bold md:text-3xl">
        Kako želite da nastavite?
      </h1>
      <p className="mt-2 text-center text-sm text-muted">
        Izaberite jednu od opcija da nastavite na dostavu.
      </p>

      <div className="mt-8 space-y-4">
        {OPTIONS.map((opt) => (
          <Link
            key={opt.href}
            href={opt.href}
            className="flex items-start gap-4 rounded-card border border-line bg-surface p-5 shadow-card transition hover:border-brand-300 hover:shadow-md"
          >
            <div className="grid h-11 w-11 shrink-0 place-items-center rounded-full bg-brand-100 text-brand-600">
              <opt.icon className="h-5 w-5" />
            </div>
            <div>
              <h2 className="font-display font-semibold text-ink">{opt.title}</h2>
              <p className="mt-0.5 text-sm text-muted">{opt.description}</p>
            </div>
          </Link>
        ))}
      </div>
    </div>
  );
}
