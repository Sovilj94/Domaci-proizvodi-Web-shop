import { redirect } from "next/navigation";

/**
 * `/products` je od R3 Špajz na `/spajz` (po uzoru na farmit). Stari linkovi i pretraga iz
 * zaglavlja (`?category=`, `?search=`) se prenose; stranica proizvoda ostaje `/products/{slug}`.
 */
export default async function ProductsPage({
  searchParams,
}: {
  searchParams: Promise<{ category?: string; search?: string }>;
}) {
  const { category, search } = await searchParams;
  const q = new URLSearchParams();
  if (category) q.set("category", category);
  if (search) q.set("search", search);
  const s = q.toString();
  redirect(s ? `/spajz?${s}` : "/spajz");
}
