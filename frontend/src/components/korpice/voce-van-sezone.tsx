"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { buttonVariants } from "@/components/ui/button";
import { ApiRequestError } from "@/lib/api/client";
import { korpiceApi } from "@/lib/api/endpoints";
import { useBlokovi } from "@/lib/api/hooks";
import { godinaZadnjeSezone, popuniSezonu } from "@/lib/mjeseci";
import { cn } from "@/lib/utils";

/**
 * /korpice/voce van sezone (spec 2026-11-26, odluka 4): po uzoru na pristup farmit.rs — poruka sa
 * mjesecima sezone, prijava „Javi mi kad stigne voće" i linkovi dalje. Naslov i tekst su sadržaj
 * (blok „korpice-voce-van-sezone" u /admin/sadrzaj); bez bloka ostaju forma i linkovi.
 */
export function VoceVanSezone({ sezonaOd, sezonaDo }: { sezonaOd: number; sezonaDo: number }) {
  const { blokovi } = useBlokovi("korpice-voce-van-sezone");
  const blok = blokovi[0];
  // Datum samo u efektu: stranica se prerenderuje, a Date u renderu je hydration mismatch.
  const [godina, setGodina] = useState<number | undefined>();
  useEffect(() => setGodina(godinaZadnjeSezone(sezonaDo, new Date())), [sezonaDo]);
  const s = { od: sezonaOd, do: sezonaDo, godina };

  return (
    <div className="mx-auto max-w-xl rounded-[26px] bg-beige px-6 py-10 text-center sm:px-10">
      {blok?.naslov && <h2 className="text-2xl font-bold text-ink sm:text-3xl">{popuniSezonu(blok.naslov, s)}</h2>}
      {blok?.tekst && <p className="mt-3 text-base text-ink">{popuniSezonu(blok.tekst, s)}</p>}

      <JaviMi />

      <div className="mt-6 flex flex-wrap justify-center gap-3">
        <Link href="/korpice/povrce" className={buttonVariants({ variant: "primary" })}>
          Pogledaj korpice povrća
        </Link>
        <Link href="/spajz" className={buttonVariants({ variant: "secondary" })}>
          Špajz
        </Link>
      </div>
    </div>
  );
}

/** Polje `web` je honeypot — sakriveno od ljudi i čitača ekrana (isti obrazac kao PrijavaNovosti). */
function JaviMi() {
  const [email, setEmail] = useState("");
  const [web, setWeb] = useState("");
  const [stanje, setStanje] = useState<"miruje" | "salje" | "gotovo">("miruje");
  const [greska, setGreska] = useState<string | null>(null);

  async function posalji(e: React.FormEvent) {
    e.preventDefault();
    setGreska(null);
    setStanje("salje");
    try {
      await korpiceApi.javiMi("voce", { email, web });
      setStanje("gotovo");
    } catch (err) {
      setStanje("miruje");
      setGreska(
        err instanceof ApiRequestError
          ? (err.fieldErrors["input.email"]?.[0] ?? err.message)
          : "Prijava nije uspjela. Pokušaj ponovo.",
      );
    }
  }

  if (stanje === "gotovo")
    return <p className="mt-6 text-base font-medium text-brand-700">Javićemo ti čim voće stigne.</p>;

  return (
    <form onSubmit={posalji} className="relative mx-auto mt-6 max-w-md text-left">
      <p className="mb-2 text-center text-sm font-semibold text-ink">Javi mi kad stigne voće</p>
      <div className="flex items-center rounded-full border border-brand-600 bg-white p-1 pl-5">
        <input
          type="email"
          required
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          placeholder="E-mail"
          aria-label="E-mail adresa"
          className="min-w-0 flex-1 bg-transparent text-sm text-ink placeholder:text-muted focus:outline-none"
        />
        <input
          type="text"
          name="web"
          tabIndex={-1}
          autoComplete="off"
          aria-hidden
          value={web}
          onChange={(e) => setWeb(e.target.value)}
          className="absolute -left-[9999px] h-px w-px opacity-0"
        />
        <button
          type="submit"
          disabled={stanje === "salje"}
          className={cn(buttonVariants({ size: "sm" }), "shrink-0 disabled:opacity-60")}
        >
          {stanje === "salje" ? "Šaljem…" : "Javi mi"}
        </button>
      </div>
      {greska && <p className="mt-2 pl-5 text-sm text-red-600">{greska}</p>}
    </form>
  );
}
