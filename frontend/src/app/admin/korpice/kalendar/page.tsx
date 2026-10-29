"use client";

import Link from "next/link";
import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Plus } from "lucide-react";
import { SmartImage } from "@/components/ui/smart-image";
import { LoadingBlock } from "@/components/ui/spinner";
import { useToast } from "@/components/ui/toast";
import { ApiRequestError } from "@/lib/api/client";
import { adminKorpiceApi, type KalendarStavka } from "@/lib/api/endpoints";
import { cn } from "@/lib/utils";

const MJESECI = ["Jan", "Feb", "Mar", "Apr", "Maj", "Jun", "Jul", "Avg", "Sep", "Okt", "Nov", "Dec"];
const SVI = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];
/** Velika korpica traži najmanje ovoliko različitih namirnica — ispod toga mjesec je crven. */
const MIN_ZA_VELIKU = 9;

/**
 * Sezonski kalendar korpica: koja kultura ide u korpicu i u kojim mjesecima. Isti podaci pune
 * „Šta dobijaš u korpici?" na sajtu i generator sedmičnog sadržaja. Svaka izmjena se snima
 * odmah. Nova kultura se dodaje kao proizvod (Proizvodi → Novi) u kategoriji povrća ili voća,
 * pa se ovdje uključi u korpicu.
 */
export default function KalendarKorpicaPage() {
  const [tip, setTip] = useState<"povrce" | "voce">("povrce");
  const qc = useQueryClient();
  const { show } = useToast();
  const kljuc = ["admin", "korpice", "kalendar", tip] as const;

  const kalendar = useQuery({ queryKey: kljuc, queryFn: () => adminKorpiceApi.kalendar(tip) });

  const snimi = useMutation({
    mutationFn: (s: KalendarStavka) =>
      adminKorpiceApi.postaviKalendar(s.productId, { uKorpici: s.uKorpici, mjeseci: s.mjeseci }),
    onMutate: async (s) => {
      await qc.cancelQueries({ queryKey: kljuc });
      const prije = qc.getQueryData<KalendarStavka[]>(kljuc);
      qc.setQueryData<KalendarStavka[]>(kljuc, (l) => l?.map((x) => (x.productId === s.productId ? s : x)));
      return { prije };
    },
    onError: (err, _s, ctx) => {
      if (ctx?.prije) qc.setQueryData(kljuc, ctx.prije);
      show(err instanceof ApiRequestError ? err.message : "Izmjena nije snimljena", "error");
    },
    onSettled: () => {
      qc.invalidateQueries({ queryKey: ["korpice", "sezona"] });
    },
  });

  // Prazni mjeseci znače „cijele godine" — u tabeli se prikazuju kao svih 12.
  const mjeseciOd = (s: KalendarStavka) => (s.mjeseci.length ? s.mjeseci : SVI);

  function prebaciMjesec(s: KalendarStavka, m: number) {
    const sad = mjeseciOd(s);
    const novi = sad.includes(m) ? sad.filter((x) => x !== m) : [...sad, m].sort((a, b) => a - b);
    // Isključen svaki mjesec bi značio „cijele godine" — umjesto toga kultura izlazi iz korpice.
    if (novi.length === 0) snimi.mutate({ ...s, uKorpici: false, mjeseci: sad });
    else snimi.mutate({ ...s, mjeseci: novi.length === 12 ? [] : novi });
  }

  const lista = kalendar.data ?? [];
  const uKorpici = lista.filter((s) => s.uKorpici);
  const van = lista.filter((s) => !s.uKorpici);
  const poMjesecu = SVI.map((m) => uKorpici.filter((s) => s.dostupan && mjeseciOd(s).includes(m)).length);

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="font-display text-2xl font-bold">Sezonski kalendar</h1>
          <p className="max-w-2xl text-muted">
            Koja kultura ide u korpicu i u kojim mjesecima. Ovo vide kupci u „Šta dobijaš u korpici?“ i po
            ovome generator puni sedmični sadržaj. Izmjena se snima odmah.
          </p>
        </div>
        <Link
          href="/admin/products?novi=1"
          className="inline-flex items-center gap-1.5 rounded-xl border border-line bg-white px-3 py-2 text-sm font-medium hover:border-brand-400"
        >
          <Plus className="h-4 w-4" /> Nova kultura (proizvod)
        </Link>
      </div>

      <div className="flex w-fit rounded-full bg-sand p-1" role="tablist">
        {(["povrce", "voce"] as const).map((t) => (
          <button
            key={t}
            type="button"
            role="tab"
            aria-selected={tip === t}
            onClick={() => setTip(t)}
            className={cn(
              "rounded-full px-5 py-1.5 text-sm font-medium transition",
              tip === t ? "bg-brand-600 text-white" : "text-ink hover:text-brand-700",
            )}
          >
            {t === "povrce" ? "Povrće" : "Voće"}
          </button>
        ))}
      </div>

      {kalendar.isLoading ? (
        <LoadingBlock />
      ) : (
        <div className="overflow-x-auto rounded-2xl border border-line bg-white">
          <table className="w-full min-w-[860px] text-sm">
            <thead>
              <tr className="border-b border-line text-left text-xs uppercase tracking-wide text-muted">
                <th className="px-3 py-2">Kultura</th>
                <th className="px-2 py-2 text-center">U korpici</th>
                {MJESECI.map((m) => (
                  <th key={m} className="px-1 py-2 text-center">
                    {m}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {uKorpici.map((s) => (
                <Red key={s.productId} s={s} mjeseci={mjeseciOd(s)} onKorpica={(v) => snimi.mutate({ ...s, uKorpici: v })} onMjesec={(m) => prebaciMjesec(s, m)} />
              ))}
              <tr className="border-y border-line bg-sand/60 text-xs font-semibold text-muted">
                <td className="px-3 py-1.5">Kultura u mjesecu</td>
                <td />
                {poMjesecu.map((c, i) => (
                  <td
                    key={i}
                    className={cn("px-1 py-1.5 text-center", c < MIN_ZA_VELIKU ? "text-red-600" : "text-brand-700")}
                    title={c < MIN_ZA_VELIKU ? `Velikoj korpici treba bar ${MIN_ZA_VELIKU} kultura` : undefined}
                  >
                    {c}
                  </td>
                ))}
              </tr>
              {van.length > 0 && (
                <tr>
                  <td colSpan={14} className="px-3 pb-1 pt-4 text-xs font-semibold uppercase tracking-wide text-muted">
                    Van korpica — uključi da kultura uđe u kalendar
                  </td>
                </tr>
              )}
              {van.map((s) => (
                <Red key={s.productId} s={s} mjeseci={mjeseciOd(s)} onKorpica={(v) => snimi.mutate({ ...s, uKorpici: v })} onMjesec={(m) => prebaciMjesec(s, m)} />
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

function Red({
  s,
  mjeseci,
  onKorpica,
  onMjesec,
}: {
  s: KalendarStavka;
  mjeseci: number[];
  onKorpica: (v: boolean) => void;
  onMjesec: (m: number) => void;
}) {
  return (
    <tr className={cn("border-b border-line last:border-0", !s.uKorpici && "opacity-60")}>
      <td className="px-3 py-1.5">
        <div className="flex items-center gap-2">
          <span className="relative h-8 w-8 shrink-0 overflow-hidden rounded-lg bg-sand">
            <SmartImage src={s.izrezUrl || s.imageUrl} alt="" sizes="32px" className={s.izrezUrl ? "object-contain" : undefined} />
          </span>
          <span className="font-medium text-ink">{s.naziv}</span>
          {!s.dostupan && <span className="rounded bg-sand px-1.5 text-[11px] text-muted">nedostupan</span>}
        </div>
      </td>
      <td className="px-2 text-center">
        <input
          type="checkbox"
          checked={s.uKorpici}
          onChange={(e) => onKorpica(e.target.checked)}
          aria-label={`${s.naziv} u korpici`}
          className="h-4 w-4 accent-brand-600"
        />
      </td>
      {SVI.map((m) => {
        const da = mjeseci.includes(m);
        return (
          <td key={m} className="px-0.5 py-1 text-center">
            <button
              type="button"
              disabled={!s.uKorpici}
              onClick={() => onMjesec(m)}
              aria-pressed={da}
              aria-label={`${s.naziv}, ${MJESECI[m - 1]}`}
              className={cn(
                "h-7 w-full min-w-[34px] rounded-md text-xs font-semibold transition disabled:cursor-not-allowed",
                da ? "bg-brand-600 text-white hover:bg-brand-700" : "bg-sand text-transparent hover:bg-brand-100",
              )}
            >
              ✓
            </button>
          </td>
        );
      })}
    </tr>
  );
}
