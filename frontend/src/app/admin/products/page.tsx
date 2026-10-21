"use client";

import { Suspense, useEffect, useMemo, useState } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Pencil, Plus, Search, Trash2, X } from "lucide-react";
import { SmartImage } from "@/components/ui/smart-image";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { LoadingBlock } from "@/components/ui/spinner";
import { ProductForm } from "@/components/admin/product-form";
import { BrojUTabeli, MjeseciTraka, Prekidac } from "@/components/admin/brza-izmjena";
import { useToast } from "@/components/ui/toast";
import { adminApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import type { ProductInput } from "@/lib/api/contracts";
import {
  FILTERI,
  type Filter,
  jeFilter,
  odgovaraPretrazi,
  primijeni,
  prolaziFilter,
  ulazIzProizvoda,
} from "@/lib/admin/proizvodi-tabela";
import type { Product } from "@/types/domain";
import { cn, formatPrice, unitSuffix } from "@/lib/utils";
import { formatMarza, tonMarze } from "@/lib/marza";

const KLJUC = ["admin", "products"] as const;

// useSearchParams traži Suspense iznad sebe, inače `next build` obara stranicu.
export default function AdminProductsPage() {
  return (
    <Suspense fallback={<LoadingBlock />}>
      <AdminProducts />
    </Suspense>
  );
}

interface Izmjena {
  p: Product;
  izmjena: Partial<ProductInput>;
  /** Tekst obavještenja, npr. „Malina: zaliha 12 kg". */
  poruka: string;
  /** Obrnuta izmjena za „Poništi"; izostaje kad je sama izmjena poništavanje. */
  vrati?: Partial<ProductInput>;
}

function AdminProducts() {
  const qc = useQueryClient();
  const { show } = useToast();
  const params = useSearchParams();
  const router = useRouter();
  const pathname = usePathname();
  const [editing, setEditing] = useState<Product | null>(null);
  const [open, setOpen] = useState(false);
  const [confirmId, setConfirmId] = useState<string | null>(null);
  const mjesec = new Date().getMonth() + 1;

  // Filter, kategorija i pretraga žive u URL-u: link sa početnog ekrana ih postavi, a
  // osvježavanje stranice ih ne izgubi.
  const filter: Filter = jeFilter(params.get("filter")) ? (params.get("filter") as Filter) : "sve";
  const kategorija = params.get("kat") ?? "";
  const [pretraga, setPretraga] = useState(params.get("q") ?? "");

  function postavi(kljuc: string, vrijednost: string) {
    const next = new URLSearchParams(params.toString());
    if (vrijednost && vrijednost !== "sve") next.set(kljuc, vrijednost);
    else next.delete(kljuc);
    router.replace(next.size ? `${pathname}?${next}` : pathname, { scroll: false });
  }

  const products = useQuery({ queryKey: KLJUC, queryFn: () => adminApi.products() });

  const izmijeni = useMutation({
    mutationFn: ({ p, izmjena }: Izmjena) => adminApi.updateProduct(p.id, ulazIzProizvoda(p, izmjena)),
    // Optimistično: red se promijeni odmah, a vrati se ako server odbije.
    onMutate: async ({ p, izmjena }) => {
      await qc.cancelQueries({ queryKey: KLJUC });
      const prije = qc.getQueryData<Product[]>(KLJUC);
      qc.setQueryData<Product[]>(KLJUC, (l) => l?.map((x) => (x.id === p.id ? primijeni(x, izmjena) : x)));
      return { prije };
    },
    onError: (err, _v, ctx) => {
      if (ctx?.prije) qc.setQueryData(KLJUC, ctx.prije);
      show(err instanceof ApiRequestError ? err.message : "Izmjena nije sačuvana", "error");
    },
    onSuccess: (novi, { p, poruka, vrati }) => {
      qc.setQueryData<Product[]>(KLJUC, (l) => l?.map((x) => (x.id === novi.id ? novi : x)));
      qc.invalidateQueries({ queryKey: ["products"] });
      qc.invalidateQueries({ queryKey: ["admin", "zadaci"] });
      show(
        poruka,
        "success",
        vrati && {
          label: "Poništi",
          onClick: () => izmijeni.mutate({ p: novi, izmjena: vrati, poruka: `${p.name}: vraćeno` }),
        },
      );
    },
  });

  function sacuvaj(p: Product, izmjena: Partial<ProductInput>, poruka: string, vrati: Partial<ProductInput>) {
    izmijeni.mutate({ p, izmjena, poruka, vrati });
  }

  const remove = useMutation({
    mutationFn: (id: string) => adminApi.deleteProduct(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: KLJUC });
      qc.invalidateQueries({ queryKey: ["products"] });
      qc.invalidateQueries({ queryKey: ["categories"] });
      qc.invalidateQueries({ queryKey: ["admin", "zadaci"] });
      setConfirmId(null);
      show("Proizvod obrisan");
    },
    onError: (err) => {
      setConfirmId(null);
      show(err instanceof ApiRequestError ? err.message : "Brisanje nije uspjelo", "error");
    },
  });

  const sve = useMemo(() => products.data ?? [], [products.data]);
  const kategorije = useMemo(
    () => [...new Map(sve.map((p) => [p.categorySlug, p.categoryName])).entries()],
    [sve],
  );
  // Brojke na filterima računaju se poslije pretrage i kategorije — kažu koliko će ostati.
  const osnova = useMemo(
    () => sve.filter((p) => (!kategorija || p.categorySlug === kategorija) && odgovaraPretrazi(p, pretraga)),
    [sve, kategorija, pretraga],
  );
  const lista = osnova.filter((p) => prolaziFilter(p, filter, mjesec));

  function openNew() {
    setEditing(null);
    setOpen(true);
  }

  // „Novi proizvod" iz Ctrl+K palete dolazi kao ?novi=1 — otvori formu i skini parametar.
  useEffect(() => {
    if (params.get("novi") !== "1") return;
    setEditing(null);
    setOpen(true);
    postavi("novi", "");
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [params]);
  function openEdit(p: Product) {
    setEditing(p);
    setOpen(true);
  }

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="font-display text-2xl font-bold">Proizvodi</h1>
          <p className="text-muted">
            Klikni cijenu, nabavnu ili zalihu da je promijeniš; mjeseci i prekidači se čuvaju odmah.
          </p>
        </div>
        <Button onClick={openNew}>
          <Plus className="h-4 w-4" /> Novi proizvod
        </Button>
      </div>

      <div className="space-y-3 rounded-card bg-white p-3 shadow-card">
        <div className="flex flex-wrap gap-2">
          <label className="relative min-w-48 flex-1">
            <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted" />
            <input
              type="search"
              value={pretraga}
              onChange={(e) => {
                setPretraga(e.target.value);
                postavi("q", e.target.value.trim());
              }}
              placeholder="Traži po nazivu, domaćinu ili kategoriji"
              aria-label="Pretraga proizvoda"
              className="h-10 w-full rounded-full border border-line bg-sand/40 pl-9 pr-3 text-sm outline-none focus:border-brand-500 focus:bg-white"
            />
          </label>
          <select
            value={kategorija}
            onChange={(e) => postavi("kat", e.target.value)}
            aria-label="Kategorija"
            className="h-10 rounded-full border border-line bg-sand/40 px-3 text-sm outline-none focus:border-brand-500"
          >
            <option value="">Sve kategorije</option>
            {kategorije.map(([slug, naziv]) => (
              <option key={slug} value={slug}>
                {naziv}
              </option>
            ))}
          </select>
        </div>
        <div className="flex flex-wrap gap-1.5">
          {FILTERI.map((f) => {
            const broj = osnova.filter((p) => prolaziFilter(p, f.kljuc, mjesec)).length;
            const problem = f.kljuc === "bez-zalihe" || f.kljuc === "bez-nabavne" || f.kljuc === "niska-marza";
            return (
              <button
                key={f.kljuc}
                type="button"
                onClick={() => postavi("filter", f.kljuc)}
                aria-pressed={filter === f.kljuc}
                className={cn(
                  "flex items-center gap-1.5 rounded-full border px-3 py-1 text-sm font-medium transition",
                  filter === f.kljuc
                    ? "border-brand-600 bg-brand-600 text-white"
                    : "border-line bg-white text-ink hover:border-brand-300",
                )}
              >
                {f.naziv}
                <span
                  className={cn(
                    "rounded-full px-1.5 text-xs",
                    filter === f.kljuc
                      ? "bg-white/20"
                      : problem && broj > 0
                        ? "bg-red-100 text-red-700"
                        : "bg-sand text-muted",
                  )}
                >
                  {broj}
                </span>
              </button>
            );
          })}
        </div>
      </div>

      {products.isLoading ? (
        <LoadingBlock />
      ) : lista.length === 0 ? (
        <EmptyState
          title="Nema proizvoda za ovaj izbor"
          description={filter !== "sve" ? "Promijeni filter ili pretragu." : "Dodaj prvi proizvod."}
        />
      ) : (
        <div className="overflow-hidden rounded-card bg-white shadow-card">
          <div className="hidden grid-cols-[minmax(0,1fr)_6rem_7rem_6rem_14rem_4.5rem_4.5rem_5rem] items-center gap-3 border-b border-line bg-sand/40 px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted xl:grid">
            <span>Proizvod</span>
            <span className="text-right">Cijena</span>
            <span className="text-right">Nabavna · marža</span>
            <span className="text-right">Zaliha</span>
            <span>Sezona</span>
            <span className="text-center">Korpice</span>
            <span className="text-center">Prodaje se</span>
            <span />
          </div>
          <ul className="divide-y divide-line">
            {lista.map((p) => {
              const jed = unitSuffix(p.unit);
              const ton = tonMarze(p.marza);
              return (
                <li
                  key={p.id}
                  className={cn(
                    "grid grid-cols-[minmax(0,1fr)_auto] items-center gap-x-3 gap-y-2 p-3 xl:grid-cols-[minmax(0,1fr)_6rem_7rem_6rem_14rem_4.5rem_4.5rem_5rem]",
                    !p.isAvailable && "bg-zinc-50 text-muted",
                  )}
                >
                  <div className="flex min-w-0 items-center gap-3">
                    <div
                      className="relative h-11 w-11 shrink-0 overflow-hidden rounded-xl"
                      style={{ backgroundColor: p.boja ?? undefined }}
                    >
                      <SmartImage src={p.imageUrl} alt={p.name} sizes="44px" />
                    </div>
                    <div className="min-w-0">
                      <p className="truncate font-medium">
                        {p.name}
                        {p.isFeatured && (
                          <span className="ml-2 rounded bg-honey-100 px-1.5 py-0.5 align-middle text-[10px] font-bold text-honey-600">
                            izdvojeno
                          </span>
                        )}
                      </p>
                      <p className="truncate text-xs text-muted">
                        {p.categoryName} · {p.producer?.name}
                      </p>
                    </div>
                  </div>

                  {/* Na užem ekranu ostatak reda ide ispod naziva, sa natpisima. */}
                  <div className="col-span-2 flex flex-wrap items-center gap-x-4 gap-y-2 text-sm xl:contents">
                    <Celija natpis="Cijena">
                      <BrojUTabeli
                        oznaka={`Cijena za ${p.name}`}
                        vrijednost={p.price}
                        prikaz={<span className="font-semibold">{formatPrice(p.price)}<span className="text-xs font-normal text-muted">{jed}</span></span>}
                        onSacuvaj={(n) => {
                          if (n != null && n > 0)
                            sacuvaj(p, { price: n }, `${p.name}: cijena ${formatPrice(n)}`, { price: p.price });
                        }}
                      />
                    </Celija>
                    <Celija natpis="Nabavna · marža">
                      <BrojUTabeli
                        oznaka={`Nabavna cijena za ${p.name}`}
                        vrijednost={p.nabavnaCijena}
                        dozvoliPrazno
                        prikaz={
                          <span className="flex flex-col items-end leading-tight">
                            <span>{p.nabavnaCijena != null ? formatPrice(p.nabavnaCijena) : <span className="text-muted">unesi</span>}</span>
                            <span
                              className={cn(
                                "text-xs",
                                ton === "losa" && "font-semibold text-red-700",
                                ton === "tijesna" && "text-honey-700",
                                ton === "dobra" && "text-brand-700",
                                ton === "nema" && "text-muted",
                              )}
                            >
                              {formatMarza(p.marza)}
                            </span>
                          </span>
                        }
                        onSacuvaj={(n) =>
                          sacuvaj(
                            p,
                            n == null ? { ukloniNabavnuCijenu: true } : { nabavnaCijena: n },
                            n == null ? `${p.name}: nabavna obrisana` : `${p.name}: nabavna ${formatPrice(n)}`,
                            p.nabavnaCijena != null ? { nabavnaCijena: p.nabavnaCijena } : { ukloniNabavnuCijenu: true },
                          )
                        }
                      />
                    </Celija>
                    <Celija natpis="Zaliha">
                      <BrojUTabeli
                        oznaka={`Zaliha za ${p.name}`}
                        vrijednost={p.stock}
                        korak={p.unit === "kom" ? 1 : 0.1}
                        prikaz={
                          <span className={cn(p.stock <= 0 && p.isAvailable && "font-semibold text-red-700")}>
                            {p.stock.toLocaleString("sr-Latn-BA")}
                            <span className="text-xs font-normal text-muted"> {p.unit}</span>
                          </span>
                        }
                        onSacuvaj={(n) => {
                          if (n != null)
                            sacuvaj(p, { stock: n }, `${p.name}: zaliha ${n.toLocaleString("sr-Latn-BA")} ${p.unit}`, { stock: p.stock });
                        }}
                      />
                    </Celija>
                    <Celija natpis="Sezona" lijevo>
                      <MjeseciTraka
                        mjeseci={p.mjeseci}
                        tekuci={mjesec}
                        onPromijeni={(novi) =>
                          sacuvaj(
                            p,
                            { mjeseci: novi },
                            `${p.name}: ${novi.length === 0 ? "cijele godine" : `sezona ${opisMjeseci(novi)}`}`,
                            { mjeseci: p.mjeseci ?? [] },
                          )
                        }
                      />
                    </Celija>
                    <Celija natpis="Korpice" centar>
                      <Prekidac
                        oznaka="Može u korpicu"
                        ukljucen={!!p.mozeUKorpicu}
                        onPromijeni={(da) =>
                          sacuvaj(p, { mozeUKorpicu: da }, `${p.name}: ${da ? "ide" : "ne ide"} u korpice`, { mozeUKorpicu: !da })
                        }
                      />
                    </Celija>
                    <Celija natpis="Prodaje se" centar>
                      <Prekidac
                        oznaka="Dostupan u prodavnici"
                        ukljucen={p.isAvailable}
                        onPromijeni={(da) =>
                          sacuvaj(p, { isAvailable: da }, `${p.name}: ${da ? "u prodaji" : "skriven"}`, { isAvailable: !da })
                        }
                      />
                    </Celija>

                    <div className="ml-auto flex items-center justify-end gap-1 xl:ml-0">
                      {confirmId === p.id ? (
                        <>
                          <Button size="sm" variant="danger" onClick={() => remove.mutate(p.id)} disabled={remove.isPending}>
                            Obriši
                          </Button>
                          <button
                            onClick={() => setConfirmId(null)}
                            className="grid h-8 w-8 place-items-center rounded-full text-muted transition hover:bg-sand hover:text-ink"
                            aria-label="Otkaži"
                          >
                            <X className="h-4 w-4" />
                          </button>
                        </>
                      ) : (
                        <>
                          <button
                            onClick={() => openEdit(p)}
                            title="Sva polja"
                            className="grid h-8 w-8 place-items-center rounded-full text-muted transition hover:bg-sand hover:text-ink"
                            aria-label="Uredi"
                          >
                            <Pencil className="h-4 w-4" />
                          </button>
                          <button
                            onClick={() => setConfirmId(p.id)}
                            title="Obriši proizvod"
                            className="grid h-8 w-8 place-items-center rounded-full text-muted transition hover:bg-red-50 hover:text-red-600"
                            aria-label="Obriši"
                          >
                            <Trash2 className="h-4 w-4" />
                          </button>
                        </>
                      )}
                    </div>
                  </div>
                </li>
              );
            })}
          </ul>
        </div>
      )}

      <ProductForm open={open} onClose={() => setOpen(false)} product={editing} />
    </div>
  );
}

/** Ćelija tabele; ispod `xl` sa svojim natpisom, jer zaglavlja kolona tada nema. */
function Celija({
  natpis,
  centar,
  lijevo,
  children,
}: {
  natpis: string;
  centar?: boolean;
  lijevo?: boolean;
  children: React.ReactNode;
}) {
  return (
    <div
      className={cn(
        "flex items-center gap-1.5 xl:flex",
        centar ? "xl:justify-center" : lijevo ? "xl:justify-start" : "xl:justify-end",
      )}
    >
      <span className="text-xs text-muted xl:hidden">{natpis}</span>
      {children}
    </div>
  );
}

/** „6–9" ili „1–4, 10–12" — uzastopni mjeseci spojeni u raspon. */
function opisMjeseci(m: number[]): string {
  const dijelovi: string[] = [];
  let od = m[0];
  for (let i = 1; i <= m.length; i++) {
    if (m[i] !== m[i - 1] + 1) {
      dijelovi.push(od === m[i - 1] ? `${od}` : `${od}–${m[i - 1]}`);
      od = m[i];
    }
  }
  return dijelovi.join(", ");
}
