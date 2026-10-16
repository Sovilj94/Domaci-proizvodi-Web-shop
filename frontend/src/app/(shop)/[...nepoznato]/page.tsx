import { notFound } from "next/navigation";

/**
 * Hvata svaku nepoznatu adresu unutar prodavnice i prikazuje `(shop)/not-found.tsx` sa
 * zaglavljem i footerom — Nextova podrazumijevana 404 je van ovog layouta, pa na ćirilici
 * nije imala provajder (nalaz iz P7).
 */
export default function Nepoznato() {
  notFound();
}
