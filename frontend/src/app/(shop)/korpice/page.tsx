import { redirect } from "next/navigation";

/**
 * `/korpice` vodi na izbor korpice povrća (R2, po uzoru na farmit `/gajbice` → `/gajbice/povrce`).
 * Izabrani plan iz linka se zadržava.
 */
export default async function KorpicePage({
  searchParams,
}: {
  searchParams: Promise<{ plan?: string }>;
}) {
  const { plan } = await searchParams;
  redirect(plan ? `/korpice/povrce?plan=${encodeURIComponent(plan)}` : "/korpice/povrce");
}
