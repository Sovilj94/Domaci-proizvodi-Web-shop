import type { PoslovanjeMjeseca } from "@/lib/api/contracts";

/** Privremeno prazno — Task 10 dodaje preuzimanje PDF-a. */
export function PdfPoslovanjaDugme({ d }: { d: PoslovanjeMjeseca }) {
  return d.mjesec ? null : null;
}
