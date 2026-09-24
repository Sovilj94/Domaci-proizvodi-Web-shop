"use client";

import { useParams } from "next/navigation";
import Link from "next/link";
import { PackageSearch } from "lucide-react";
import { OrderReceipt } from "@/components/shop/order-receipt";
import { LoadingBlock } from "@/components/ui/spinner";
import { EmptyState } from "@/components/ui/empty-state";
import { buttonVariants } from "@/components/ui/button";
import { useTrackOrder } from "@/lib/api/hooks";
import { cn } from "@/lib/utils";

/**
 * The permanent link from the order email — opened any time after ordering, possibly
 * days later. Unlike `/potvrda`, there is nothing to create here (`orderApi.track` is
 * a read) and nothing to thank the customer for again, so the receipt renders in
 * "pracenje" mode: the order's current status leads instead of "Hvala ti!".
 */
export default function NarudzbaPage() {
  const token = String(useParams().token);
  const { data: order, isLoading, isError } = useTrackOrder(token);

  if (isLoading) return <LoadingBlock label="Učitavam narudžbu…" />;

  if (isError || !order) {
    return (
      <div className="mx-auto max-w-2xl px-4 py-12">
        <EmptyState
          icon={<PackageSearch className="h-7 w-7" />}
          title="Narudžba nije pronađena"
          description="Link za praćenje je pogrešan ili je narudžba uklonjena."
          action={
            <Link href="/" className={cn(buttonVariants())}>
              Na početnu
            </Link>
          }
        />
      </div>
    );
  }

  return <OrderReceipt order={order} mode="pracenje" />;
}
