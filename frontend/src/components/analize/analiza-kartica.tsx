import Link from "next/link";
import { AlertTriangle, CheckCircle2, FileText } from "lucide-react";
import { REZULTAT_ANALIZE_TEKST, type Analiza } from "@/types/domain";
import { cn } from "@/lib/utils";

/** "2026-10-12" -> "12.10.2026." Parsira se kao lokalni dan; `new Date(str)` bi ga čitao kao UTC ponoć. */
export function formatirajDatumAnalize(iso: string): string {
  const [g, m, d] = iso.split("-").map(Number);
  if (!g || !m || !d) return iso;
  const datum = new Date(g, m - 1, d);
  return `${datum.getDate()}.${datum.getMonth() + 1}.${datum.getFullYear()}.`;
}

/**
 * pdfUrl unosi admin; link prikazujemo samo za http(s) i putanje na ovom sajtu
 * da `javascript:` (i slične sheme) ne završe u href-u.
 */
function jeBezbjedanUrl(url?: string): url is string {
  return !!url && /^(https?:\/\/|\/)/i.test(url.trim());
}

export function AnalizaKartica({
  analiza,
  bezLinkaNaDomacina = false,
}: {
  analiza: Analiza;
  /** Na stranici samog domaćina link na njega je suvišan. */
  bezLinkaNaDomacina?: boolean;
}) {
  const ok = analiza.rezultat === "Uskladjeno";
  const Ikona = ok ? CheckCircle2 : AlertTriangle;

  return (
    <article className="rounded-card border border-line bg-surface p-5">
      <span
        className={cn(
          "inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-sm font-semibold",
          ok ? "bg-brand-50 text-brand-700" : "bg-red-50 text-red-800",
        )}
      >
        <Ikona className="h-4 w-4" aria-hidden />
        {REZULTAT_ANALIZE_TEKST[analiza.rezultat]}
      </span>

      <h3 className="mt-3 font-semibold text-ink">{analiza.uzorak}</h3>
      <p className="mt-0.5 text-sm text-muted">
        {bezLinkaNaDomacina ? (
          analiza.selo
        ) : (
          <>
            <Link href={`/proizvodjaci/${analiza.producerId}`} className="font-medium text-ink hover:underline">
              {analiza.proizvodjac}
            </Link>
            {analiza.selo && `, ${analiza.selo}`}
          </>
        )}
      </p>

      <div className="mt-3 space-y-1 text-sm text-ink/80">
        <p>Ispitano: {analiza.ispitano}</p>
        <p>
          Laboratorija: {analiza.laboratorija}
          {analiza.brojIzvjestaja && ` · izvještaj ${analiza.brojIzvjestaja}`}
        </p>
        <p>Datum uzorkovanja: {formatirajDatumAnalize(analiza.datumUzorkovanja)}</p>
      </div>

      {analiza.napomena && <p className="mt-3 text-sm text-ink/80">{analiza.napomena}</p>}

      {jeBezbjedanUrl(analiza.pdfUrl) && (
        <a
          href={analiza.pdfUrl}
          target="_blank"
          rel="noopener noreferrer"
          className="mt-4 inline-flex items-center gap-1.5 text-sm font-medium text-brand-700 hover:underline"
        >
          <FileText className="h-4 w-4" aria-hidden /> Izvještaj (PDF)
        </a>
      )}
    </article>
  );
}
