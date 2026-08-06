import type { NextRequest } from "next/server";
import { db } from "@/server/mock/store";
import { jsonOk, requireAdmin } from "@/server/mock/http";
import type { OrderStatus } from "@/types/domain";

export function GET(req: NextRequest) {
  const { response } = requireAdmin(req);
  if (response) return response;

  const { searchParams } = new URL(req.url);
  const status = searchParams.get("status") as OrderStatus | null;
  const date = searchParams.get("date");

  let orders = [...db.orders];
  if (status) orders = orders.filter((o) => o.status === status);
  if (date) {
    const d = new Date(date);
    orders = orders.filter((o) => {
      const od = new Date(o.createdAt);
      return (
        od.getFullYear() === d.getFullYear() &&
        od.getMonth() === d.getMonth() &&
        od.getDate() === d.getDate()
      );
    });
  }
  return jsonOk(orders, { total: orders.length });
}
