"use client";

import { createContext, useCallback, useContext, useState } from "react";
import { motion, AnimatePresence } from "motion/react";
import { CheckCircle2, Info, XCircle } from "lucide-react";
import { cn } from "@/lib/utils";

type ToastVariant = "success" | "error" | "info";

/** Dugme u obavještenju, npr. „Poništi" poslije brze izmjene. Klik ga i zatvara. */
export interface ToastAkcija {
  label: string;
  onClick: () => void;
}

interface Toast {
  id: number;
  message: string;
  variant: ToastVariant;
  akcija?: ToastAkcija;
}

interface ToastContextValue {
  show: (message: string, variant?: ToastVariant, akcija?: ToastAkcija) => void;
}

const ToastContext = createContext<ToastContextValue | null>(null);

export function ToastProvider({ children }: { children: React.ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([]);

  const zatvori = useCallback((id: number) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  }, []);

  const show = useCallback(
    (message: string, variant: ToastVariant = "success", akcija?: ToastAkcija) => {
      const id = Date.now() + Math.random();
      setToasts((prev) => [...prev, { id, message, variant, akcija }]);
      // Sa akcijom duže — treba stići do dugmeta.
      setTimeout(() => zatvori(id), akcija ? 6000 : 3200);
    },
    [zatvori],
  );

  return (
    <ToastContext.Provider value={{ show }}>
      {children}
      <div className="pointer-events-none fixed inset-x-0 bottom-4 z-50 flex flex-col items-center gap-2 px-4">
        <AnimatePresence>
          {toasts.map((t) => (
            <motion.div
              key={t.id}
              layout
              initial={{ opacity: 0, y: 20, scale: 0.9 }}
              animate={{ opacity: 1, y: 0, scale: 1 }}
              exit={{ opacity: 0, scale: 0.9 }}
              transition={{ duration: 0.2 }}
              className={cn(
                "pointer-events-auto flex items-center gap-2 rounded-full px-4 py-2.5 text-sm font-medium shadow-lg",
                t.variant === "success" && "bg-brand-600 text-white",
                t.variant === "error" && "bg-red-600 text-white",
                t.variant === "info" && "bg-ink text-white",
              )}
            >
              {t.variant === "success" && <CheckCircle2 className="h-4 w-4" />}
              {t.variant === "error" && <XCircle className="h-4 w-4" />}
              {t.variant === "info" && <Info className="h-4 w-4" />}
              {t.message}
              {t.akcija && (
                <button
                  type="button"
                  onClick={() => {
                    t.akcija!.onClick();
                    zatvori(t.id);
                  }}
                  className="-mr-1 ml-1 rounded-full bg-white/20 px-2.5 py-0.5 text-xs font-semibold transition hover:bg-white/30"
                >
                  {t.akcija.label}
                </button>
              )}
            </motion.div>
          ))}
        </AnimatePresence>
      </div>
    </ToastContext.Provider>
  );
}

export function useToast(): ToastContextValue {
  const ctx = useContext(ToastContext);
  if (!ctx) throw new Error("useToast must be used within ToastProvider");
  return ctx;
}
