"use client";

import { useState } from "react";
import { FileDown } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useToast } from "@/components/ui/toast";
import type { PoslovanjeMjeseca } from "@/lib/api/contracts";

/** PDF mjesečnog pregleda; pdfmake se učitava tek na klik. */
export function PdfPoslovanjaDugme({ d }: { d: PoslovanjeMjeseca }) {
  const [radi, setRadi] = useState(false);
  const { show } = useToast();
  async function preuzmi() {
    setRadi(true);
    try {
      const { preuzmiPdfPoslovanja } = await import("@/lib/admin/poslovanje-pdf");
      await preuzmiPdfPoslovanja(d);
    } catch {
      show("PDF nije napravljen — pokušaj ponovo.", "error");
    } finally {
      setRadi(false);
    }
  }
  return (
    <Button size="sm" onClick={preuzmi} disabled={radi}>
      <FileDown className="h-4 w-4" /> {radi ? "Pravim PDF…" : "Preuzmi PDF"}
    </Button>
  );
}
