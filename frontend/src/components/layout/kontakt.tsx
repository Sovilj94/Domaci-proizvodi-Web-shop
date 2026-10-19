"use client";

import { useSadrzajSajta } from "@/lib/api/hooks";
import { kontaktIz } from "@/lib/kontakt";

/**
 * Kontakt podaci za serverske stranice (footer, /o-nama). Prvo se prikaže ugrađena vrijednost,
 * pa ona koju je admin unio čim stigne — tako stranica ne čeka API da bi se nacrtala.
 */
export function useKontakt() {
  const { data } = useSadrzajSajta();
  return kontaktIz(data);
}

export function KontaktTelefon({ className }: { className?: string }) {
  const k = useKontakt();
  return (
    <a href={k.telefonHref} translate="no" className={className}>
      {k.telefon}
    </a>
  );
}

export function KontaktEmail({ className }: { className?: string }) {
  const k = useKontakt();
  if (!k.email) return null;
  return (
    <a href={`mailto:${k.email}`} translate="no" className={className}>
      {k.email}
    </a>
  );
}

export function KontaktPolje({ polje }: { polje: "adresa" | "podrucje" }) {
  return <>{useKontakt()[polje]}</>;
}
