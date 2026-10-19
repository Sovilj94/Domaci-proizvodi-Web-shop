"use client";

import { useEffect, useId, useRef } from "react";
import { X } from "lucide-react";
import { cn } from "@/lib/utils";

/**
 * Bočna ladica admin formi (E1). Do sada je svaka forma imala svoju kopiju istog omotača, bez
 * `role="dialog"`, bez veze naslova i bez zatvaranja na Escape. Ovdje je to jednom: ladica se
 * najavljuje čitaču ekrana kao modalni dijalog sa naslovom, fokus ulazi u nju pri otvaranju i
 * vraća se na dugme koje ju je otvorilo pri zatvaranju, a Escape je zatvara.
 */
export function AdminLadica({
  naslov,
  podnaslov,
  onClose,
  sirina = "max-w-lg",
  children,
}: {
  naslov: React.ReactNode;
  podnaslov?: React.ReactNode;
  onClose: () => void;
  sirina?: string;
  children: React.ReactNode;
}) {
  const naslovId = useId();
  const ladica = useRef<HTMLDivElement>(null);
  // onClose je najčešće nova funkcija pri svakom renderu forme — ref drži efekat ispod da se ne
  // pretplaćuje iznova (i ne krade fokus) na svaki taster u formi.
  const zatvori = useRef(onClose);
  zatvori.current = onClose;

  useEffect(() => {
    const prethodni = document.activeElement as HTMLElement | null;
    const prvo = ladica.current?.querySelector<HTMLElement>(
      "input:not([type=hidden]):not([disabled]), select:not([disabled]), textarea:not([disabled])",
    );
    (prvo ?? ladica.current)?.focus();

    function naTaster(e: KeyboardEvent) {
      if (e.key === "Escape") zatvori.current();
    }
    document.addEventListener("keydown", naTaster);
    return () => {
      document.removeEventListener("keydown", naTaster);
      prethodni?.focus?.();
    };
  }, []);

  return (
    <div className="fixed inset-0 z-50">
      <button
        type="button"
        aria-label="Zatvori"
        tabIndex={-1}
        onClick={onClose}
        className="absolute inset-0 bg-black/40"
      />
      <div
        ref={ladica}
        role="dialog"
        aria-modal="true"
        aria-labelledby={naslovId}
        tabIndex={-1}
        className={cn(
          "absolute right-0 top-0 flex h-full w-full flex-col bg-cream shadow-xl focus:outline-none",
          sirina,
        )}
      >
        <div className="flex items-start justify-between gap-4 border-b border-line px-6 py-4">
          <div>
            <h2 id={naslovId} className="font-display text-lg font-bold text-ink">
              {naslov}
            </h2>
            {podnaslov && <p className="text-sm text-muted">{podnaslov}</p>}
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label="Zatvori"
            className="shrink-0 rounded-full p-1.5 text-muted transition hover:bg-sand"
          >
            <X className="h-5 w-5" />
          </button>
        </div>
        {children}
      </div>
    </div>
  );
}
