"use client";

import { useId, useRef, useState } from "react";
import { Upload } from "lucide-react";
import { Input, Label } from "@/components/ui/field";
import { SmartImage } from "@/components/ui/smart-image";
import { ApiRequestError } from "@/lib/api/client";
import { adminApi, type FolderSlike } from "@/lib/api/endpoints";
import { cn } from "@/lib/utils";

/**
 * Polje slike u admin formama: „Učitaj sliku" je šalje u bazu (`POST /admin/slike`) i upiše
 * dobijeni `/slike/...` URL; ručno se i dalje može upisati vanjski URL. Pregled je odmah ispod.
 */
export function SlikaPolje({
  label,
  value,
  onChange,
  folder,
  error,
}: {
  label: string;
  value: string;
  onChange: (url: string) => void;
  folder: FolderSlike;
  error?: string;
}) {
  const id = useId();
  const fajl = useRef<HTMLInputElement>(null);
  const [salje, setSalje] = useState(false);
  const [greska, setGreska] = useState<string | null>(null);

  async function izabrano(e: React.ChangeEvent<HTMLInputElement>) {
    const f = e.target.files?.[0];
    e.target.value = "";
    if (!f) return;
    if (f.size > 5 * 1024 * 1024) {
      setGreska("Slika može imati najviše 5 MB.");
      return;
    }
    setGreska(null);
    setSalje(true);
    try {
      onChange((await adminApi.uploadSlika(f, folder)).url);
    } catch (err) {
      setGreska(
        err instanceof ApiRequestError
          ? (err.fieldErrors.fajl?.[0] ?? err.fieldErrors.folder?.[0] ?? err.message)
          : "Slika nije učitana. Pokušaj ponovo.",
      );
    } finally {
      setSalje(false);
    }
  }

  const poruka = greska ?? error;

  return (
    <div>
      <Label htmlFor={id}>{label}</Label>
      <div className="flex gap-2">
        <Input
          id={id}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          placeholder="Učitaj sliku ili upiši URL"
          aria-invalid={!!poruka || undefined}
          className="min-w-0 flex-1"
        />
        <button
          type="button"
          onClick={() => fajl.current?.click()}
          disabled={salje}
          className={cn(
            "inline-flex shrink-0 items-center gap-1.5 rounded-xl border border-line bg-white px-3 text-sm font-medium text-ink transition hover:border-brand-400",
            salje && "opacity-60",
          )}
        >
          <Upload className="h-4 w-4" />
          {salje ? "Šaljem…" : "Učitaj sliku"}
        </button>
        <input
          ref={fajl}
          type="file"
          accept="image/jpeg,image/png,image/webp"
          className="hidden"
          onChange={izabrano}
        />
      </div>
      {poruka && <p className="mt-1 text-xs text-red-600">{poruka}</p>}
      {value && (
        <div className="relative mt-2 h-24 w-32 overflow-hidden rounded-xl bg-sand">
          <SmartImage key={value} src={value} alt="" sizes="128px" />
        </div>
      )}
    </div>
  );
}
