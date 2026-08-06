"use client";

import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { MapPin, Navigation, Route, Sparkles } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Field, Select } from "@/components/ui/field";
import { LoadingBlock } from "@/components/ui/spinner";
import { EmptyState } from "@/components/ui/empty-state";
import { useToast } from "@/components/ui/toast";
import { adminApi } from "@/lib/api/endpoints";
import type { DeliveryRoute } from "@/types/domain";
import { cn } from "@/lib/utils";

const ASSIGNABLE = new Set(["Confirmed", "Packing", "OutForDelivery"]);

export default function AdminRoutesPage() {
  const qc = useQueryClient();
  const { show } = useToast();
  const [date, setDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [driverId, setDriverId] = useState("");
  const [selected, setSelected] = useState<Set<string>>(new Set());

  const drivers = useQuery({ queryKey: ["admin", "drivers"], queryFn: () => adminApi.drivers() });
  const orders = useQuery({
    queryKey: ["admin", "orders", "assignable"],
    queryFn: () => adminApi.orders(),
  });
  const routes = useQuery({
    queryKey: ["admin", "routes", date],
    queryFn: () => adminApi.routes(date),
  });

  // Order IDs already placed on a route for the selected date — hide them from
  // the assignable list so an order disappears the moment it's given to a driver.
  const assignedIds = useMemo(() => {
    const ids = new Set<string>();
    routes.data?.forEach((r) => r.stops.forEach((s) => ids.add(s.orderId)));
    return ids;
  }, [routes.data]);

  const candidates = useMemo(
    () =>
      orders.data?.filter(
        (o) => ASSIGNABLE.has(o.status) && !assignedIds.has(o.id),
      ) ?? [],
    [orders.data, assignedIds],
  );

  const assign = useMutation({
    mutationFn: () =>
      adminApi.assignRoute({ date, driverId, orderIds: [...selected] }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin", "routes", date] });
      setSelected(new Set());
      show("Ruta dodijeljena vozaču");
    },
  });

  const optimize = useMutation({
    mutationFn: (routeId: string) => adminApi.optimizeRoute(routeId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin", "routes", date] });
      show("Ruta optimizovana");
    },
  });

  function toggle(id: string) {
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  const canAssign = driverId && selected.size > 0 && !assign.isPending;

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="font-display text-2xl font-bold">Rute dostave</h1>
          <p className="text-muted">Dodijeli narudžbe vozačima i optimizuj redoslijed.</p>
        </div>
        <div className="w-40">
          <Field label="Datum dostave">
            <input
              type="date"
              value={date}
              onChange={(e) => setDate(e.target.value)}
              className="w-full rounded-xl border-none bg-white px-3.5 py-2.5 text-sm ring-1 ring-black/10 focus:outline-none focus:ring-2 focus:ring-brand-400"
            />
          </Field>
        </div>
      </div>

      <div className="grid gap-6 lg:grid-cols-2">
        {/* Assign */}
        <section className="rounded-card bg-white p-5 ring-1 ring-black/5">
          <h2 className="font-display text-lg font-semibold">Narudžbe za dostavu</h2>
          <p className="text-sm text-muted">Odaberite narudžbe i vozača.</p>

          <div className="mt-4 flex items-end gap-3">
            <div className="flex-1">
              <Field label="Vozač">
                <Select value={driverId} onChange={(e) => setDriverId(e.target.value)}>
                  <option value="">Odaberite vozača…</option>
                  {drivers.data?.map((d) => (
                    <option key={d.id} value={d.id}>
                      {d.name}
                    </option>
                  ))}
                </Select>
              </Field>
            </div>
            <Button disabled={!canAssign} onClick={() => assign.mutate()}>
              Dodijeli ({selected.size})
            </Button>
          </div>

          <div className="mt-4 space-y-2">
            {orders.isLoading ? (
              <LoadingBlock />
            ) : candidates.length === 0 ? (
              <p className="py-6 text-center text-sm text-muted">
                Nema narudžbi spremnih za dostavu.
              </p>
            ) : (
              candidates.map((o) => (
                <label
                  key={o.id}
                  className={cn(
                    "flex cursor-pointer items-center gap-3 rounded-xl border p-3 transition",
                    selected.has(o.id)
                      ? "border-brand-400 bg-brand-50"
                      : "border-black/5 hover:bg-sand/50",
                  )}
                >
                  <input
                    type="checkbox"
                    checked={selected.has(o.id)}
                    onChange={() => toggle(o.id)}
                    className="h-4 w-4 accent-brand-600"
                  />
                  <div className="min-w-0 flex-1">
                    <p className="font-medium">{o.code}</p>
                    <p className="truncate text-xs text-muted">
                      {o.customerName} · {o.deliveryAddress}
                    </p>
                  </div>
                  <span className="rounded-full bg-sand px-2 py-0.5 text-xs font-medium">
                    {o.deliveryZoneName}
                  </span>
                </label>
              ))
            )}
          </div>
        </section>

        {/* Routes */}
        <section className="space-y-4">
          <h2 className="font-display text-lg font-semibold">
            Rute za {date}
          </h2>
          {routes.isLoading ? (
            <LoadingBlock />
          ) : routes.data && routes.data.length > 0 ? (
            routes.data.map((r) => (
              <RouteCard
                key={r.id}
                route={r}
                optimizing={optimize.isPending}
                onOptimize={() => optimize.mutate(r.id)}
              />
            ))
          ) : (
            <EmptyState
              icon={<Route className="h-7 w-7" />}
              title="Nema ruta"
              description="Dodijelite narudžbe vozaču da kreirate rutu."
            />
          )}
        </section>
      </div>
    </div>
  );
}

/**
 * Builds a Google Maps directions URL for the whole route. With a GPS `origin`
 * the trip starts from the driver's current position and every stop becomes a
 * waypoint; without one it falls back to using the first stop as the start.
 */
function buildNavUrl(stops: { address: string }[], gpsOrigin?: string): string {
  const points = stops.map((s) => `${s.address}, Banja Luka`);
  const params = new URLSearchParams({ api: "1", travelmode: "driving" });

  let sequence = points;
  if (gpsOrigin) {
    params.set("origin", gpsOrigin);
  } else if (points.length > 1) {
    params.set("origin", points[0]);
    sequence = points.slice(1);
  }

  const destination = sequence[sequence.length - 1];
  const waypoints = sequence.slice(0, -1);
  if (destination) params.set("destination", destination);
  if (waypoints.length) params.set("waypoints", waypoints.join("|"));

  return `https://www.google.com/maps/dir/?${params.toString()}`;
}

function RouteCard({
  route,
  optimizing,
  onOptimize,
}: {
  route: DeliveryRoute;
  optimizing: boolean;
  onOptimize: () => void;
}) {
  const { show } = useToast();
  const [locating, setLocating] = useState(false);

  function openNavigation() {
    const open = (url: string) => window.open(url, "_blank", "noopener,noreferrer");

    if (typeof navigator === "undefined" || !navigator.geolocation) {
      show("Lokacija nije dostupna — navigacija počinje od prve stanice.");
      open(buildNavUrl(route.stops));
      return;
    }

    setLocating(true);
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        setLocating(false);
        open(buildNavUrl(route.stops, `${pos.coords.latitude},${pos.coords.longitude}`));
      },
      () => {
        setLocating(false);
        show("Nije moguće dohvatiti lokaciju — navigacija počinje od prve stanice.");
        open(buildNavUrl(route.stops));
      },
      { enableHighAccuracy: true, timeout: 10000 },
    );
  }

  return (
    <div className="rounded-card bg-white p-5 ring-1 ring-black/5">
      <div className="flex items-center justify-between">
        <div>
          <p className="font-display font-semibold">{route.driverName}</p>
          <p className="text-xs text-muted">{route.stops.length} stajanja</p>
        </div>
        <span
          className={cn(
            "rounded-full px-2.5 py-1 text-xs font-semibold",
            route.status === "Optimized"
              ? "bg-brand-100 text-brand-700"
              : "bg-honey-100 text-honey-600",
          )}
        >
          {route.status === "Optimized" ? "Optimizovana" : "Planirana"}
        </span>
      </div>

      <ol className="mt-4 space-y-2">
        {route.stops.map((s) => (
          <li key={s.orderId} className="flex items-center gap-3 text-sm">
            <span className="grid h-6 w-6 shrink-0 place-items-center rounded-full bg-ink text-xs font-bold text-white">
              {s.sequence}
            </span>
            <div className="min-w-0 flex-1">
              <p className="truncate font-medium">
                {s.customerName} <span className="text-muted">· {s.orderCode}</span>
              </p>
              <p className="flex items-center gap-1 truncate text-xs text-muted">
                <MapPin className="h-3 w-3" /> {s.address} ({s.zoneName})
              </p>
            </div>
          </li>
        ))}
      </ol>

      <div className="mt-4 flex gap-2">
        <Button size="sm" variant="secondary" onClick={onOptimize} disabled={optimizing}>
          <Sparkles className="h-4 w-4" /> Optimizuj rutu
        </Button>
        <Button size="sm" onClick={openNavigation} disabled={locating}>
          <Navigation className="h-4 w-4" /> {locating ? "Lociranje…" : "Navigacija"}
        </Button>
      </div>
    </div>
  );
}
