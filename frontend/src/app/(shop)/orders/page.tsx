import { redirect } from "next/navigation";

/**
 * Narudžbe su se preselile pod /moj-nalog/narudzbe. Ova ruta ostaje samo da
 * stari linkovi i markeri ne puknu — preusmjerenje je serversko (nema
 * "use client", nema useEffect), pa skok ne košta ništa na klijentu.
 */
export default function OrdersPage() {
  redirect("/moj-nalog/narudzbe");
}
