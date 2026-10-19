"use client";

import { useState } from "react";
import Link from "next/link";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Plus } from "lucide-react";
import { Button } from "@/components/ui/button";
import { LoadingBlock } from "@/components/ui/spinner";
import { EmptyState } from "@/components/ui/empty-state";
import { useToast } from "@/components/ui/toast";
import { OrderCard } from "@/components/admin/order-card";
import { ApiRequestError } from "@/lib/api/client";
import { adminApi } from "@/lib/api/endpoints";
import { STATUS_META } from "@/lib/orders";
import { ORDER_STATUSES, type OrderStatus } from "@/types/domain";
import { cn } from "@/lib/utils";

export default function AdminOrdersPage() {
  const [filter, setFilter] = useState<OrderStatus | null>(null);
  const [expanded, setExpanded] = useState<Set<string>>(new Set());
  const qc = useQueryClient();
  const { show } = useToast();

  const orders = useQuery({
    queryKey: ["admin", "orders", { status: filter }],
    queryFn: () => adminApi.orders({ status: filter ?? undefined }),
  });

  const mutation = useMutation({
    mutationFn: ({ id, status }: { id: string; status: OrderStatus }) =>
      adminApi.updateOrderStatus(id, status),
    onSuccess: (_data, { status }) => {
      qc.invalidateQueries({ queryKey: ["admin", "orders"] });
      qc.invalidateQueries({ queryKey: ["admin", "dashboard"] });
      show(`Status promijenjen: ${STATUS_META[status].label}`);
    },
    onError: (err) => show(err instanceof ApiRequestError ? err.message : "Status nije promijenjen", "error"),
  });

  function toggle(id: string) {
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="font-display text-2xl font-bold">Narudžbe</h1>
          <p className="text-muted">Kliknite narudžbu da je proširite i promijenite status.</p>
        </div>
        <Link href="/admin/orders/new">
          <Button size="sm">
            <Plus className="h-4 w-4" /> Nova narudžba
          </Button>
        </Link>
      </div>

      <div className="no-scrollbar -mx-1 flex gap-2 overflow-x-auto px-1">
        <FilterChip label="Sve" active={filter === null} onClick={() => setFilter(null)} />
        {ORDER_STATUSES.map((st) => (
          <FilterChip
            key={st}
            label={STATUS_META[st].label}
            active={filter === st}
            onClick={() => setFilter(st)}
          />
        ))}
      </div>

      {orders.isLoading ? (
        <LoadingBlock />
      ) : orders.data && orders.data.length > 0 ? (
        <div className="space-y-3">
          {orders.data.map((o) => (
            <OrderCard
              key={o.id}
              order={o}
              expanded={expanded.has(o.id)}
              pending={mutation.isPending}
              onToggle={() => toggle(o.id)}
              onAdvance={(status) => mutation.mutate({ id: o.id, status })}
            />
          ))}
        </div>
      ) : (
        <EmptyState title="Nema narudžbi" description="Za odabrani filter nema rezultata." />
      )}
    </div>
  );
}

function FilterChip({
  label,
  active,
  onClick,
}: {
  label: string;
  active: boolean;
  onClick: () => void;
}) {
  return (
    <button
      onClick={onClick}
      className={cn(
        "whitespace-nowrap rounded-full px-3.5 py-1.5 text-sm font-medium transition",
        active ? "bg-ink text-white" : "bg-white text-ink ring-1 ring-black/10 hover:bg-sand",
      )}
    >
      {label}
    </button>
  );
}
