"use client";

import { useEffect, useRef, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { Clock3, XCircle } from "lucide-react";
import { OrderReceipt } from "@/components/shop/order-receipt";
import { LoadingBlock } from "@/components/ui/spinner";
import { buttonVariants } from "@/components/ui/button";
import { orderApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import { useCart } from "@/lib/cart/cart-context";
import type { Order } from "@/types/domain";
import { cn } from "@/lib/utils";

type State =
  | { kind: "loading" }
  | { kind: "ok"; order: Order }
  | { kind: "expired" }
  | { kind: "unavailable"; message: string }
  | { kind: "error"; message: string };

/**
 * This is the click that creates the order — `confirmGuest` isn't a status check,
 * it consumes the guest's confirmation token server-side. A consumed token and an
 * expired one look identical afterwards (both come back `guest_request_expired`),
 * which is exactly what happens if this fires twice.
 */
export default function PotvrdaPage() {
  const token = String(useParams().token);
  const { clear } = useCart();
  const [state, setState] = useState<State>({ kind: "loading" });
  const ran = useRef(false);

  useEffect(() => {
    // Guard against React strict mode's double effect invocation in dev: a `useRef`
    // flag, not a dependency-array trick, because the call has a side effect on the
    // server (it consumes the token) that a second render must never repeat. Without
    // this, the second call would hit an already-consumed token and report
    // "expired" for an order that in fact just went through — the worst lie to tell
    // a customer at this exact moment.
    if (ran.current) return;
    ran.current = true;

    orderApi
      .confirmGuest(token)
      .then((order) => {
        clear(); // only here, only on success — until now the cart might still be needed
        setState({ kind: "ok", order });
      })
      .catch((e: unknown) => {
        if (e instanceof ApiRequestError && e.apiError.code === "guest_request_expired") {
          setState({ kind: "expired" });
        } else if (e instanceof ApiRequestError && e.apiError.code === "product_unavailable") {
          setState({ kind: "unavailable", message: e.message });
        } else {
          setState({
            kind: "error",
            message:
              e instanceof ApiRequestError ? e.message : "Potvrda nije uspjela. Pokušajte ponovo.",
          });
        }
      });
  }, [token, clear]);

  if (state.kind === "loading") return <LoadingBlock label="Potvrđujem narudžbu…" />;

  if (state.kind === "ok") return <OrderReceipt order={state.order} mode="zahvala" />;

  if (state.kind === "expired") {
    return (
      <div className="mx-auto max-w-2xl px-4 py-12">
        <div className="rounded-card bg-surface p-8 text-center shadow-card">
          <div className="mx-auto grid h-14 w-14 place-items-center rounded-full bg-red-100 text-red-600">
            <Clock3 className="h-8 w-8" />
          </div>
          <h1 className="mt-5 font-display text-2xl font-bold">Potvrda je istekla</h1>
          <p className="mt-2 text-sm text-muted">
            Link za potvrdu vrijedi 15 minuta od slanja narudžbe.
          </p>
          <p className="mt-2 text-sm text-muted">
            Ako ste link otvorili više puta, narudžba je možda već primljena — prije nego
            što je pošaljete ponovo, provjerite svoj mejl.
          </p>
          <Link href="/checkout" className={cn(buttonVariants(), "mt-6 inline-flex")}>
            Vrati u korpu
          </Link>
        </div>
      </div>
    );
  }

  if (state.kind === "unavailable") {
    return (
      <div className="mx-auto max-w-2xl px-4 py-12">
        <div className="rounded-card bg-surface p-8 text-center shadow-card">
          <div className="mx-auto grid h-14 w-14 place-items-center rounded-full bg-red-100 text-red-600">
            <XCircle className="h-8 w-8" />
          </div>
          <h1 className="mt-5 font-display text-2xl font-bold">Narudžba nije poslana</h1>
          <p className="mt-2 text-sm text-muted">{state.message}</p>
          <Link href="/cart" className={cn(buttonVariants(), "mt-6 inline-flex")}>
            Nazad na korpu
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-2xl px-4 py-12">
      <div className="rounded-card bg-surface p-8 text-center shadow-card">
        <div className="mx-auto grid h-14 w-14 place-items-center rounded-full bg-red-100 text-red-600">
          <XCircle className="h-8 w-8" />
        </div>
        <h1 className="mt-5 font-display text-2xl font-bold">Potvrda nije uspjela</h1>
        <p className="mt-2 text-sm text-muted">{state.message}</p>
        <Link href="/checkout" className={cn(buttonVariants(), "mt-6 inline-flex")}>
          Nazad na dostavu
        </Link>
      </div>
    </div>
  );
}
