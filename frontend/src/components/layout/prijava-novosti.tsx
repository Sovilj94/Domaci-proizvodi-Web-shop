"use client";

import { useState } from "react";
import { novostiApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";

/**
 * Kartica „Prijavi se i budi u toku" iz farmit footera (R1). Polje `web` je honeypot —
 * sakriveno od ljudi i čitača ekrana; bot ga popuni i server ga tiho odbaci.
 */
export function PrijavaNovosti() {
  const [email, setEmail] = useState("");
  const [web, setWeb] = useState("");
  const [stanje, setStanje] = useState<"miruje" | "salje" | "gotovo">("miruje");
  const [greska, setGreska] = useState<string | null>(null);

  async function posalji(e: React.FormEvent) {
    e.preventDefault();
    setGreska(null);
    setStanje("salje");
    try {
      await novostiApi.prijavi({ email, web });
      setStanje("gotovo");
    } catch (err) {
      setStanje("miruje");
      setGreska(
        err instanceof ApiRequestError
          ? (err.fieldErrors.email?.[0] ?? err.message)
          : "Prijava nije uspjela. Pokušaj ponovo.",
      );
    }
  }

  return (
    <div className="relative rounded-2xl bg-white p-6 shadow-[0_6px_24px_rgba(64,74,61,0.15)] sm:flex sm:items-center sm:justify-between sm:gap-8 sm:p-8">
      <h2 className="text-xl font-semibold text-brand-600 sm:text-2xl">Prijavi se i budi u toku</h2>

      {stanje === "gotovo" ? (
        <p className="mt-4 text-base font-medium text-ink sm:mt-0">Hvala! Javljamo ti novosti iz domaćinstava.</p>
      ) : (
        <form onSubmit={posalji} className="mt-4 w-full sm:mt-0 sm:max-w-md">
          <div className="flex items-center rounded-full border border-brand-600 p-1 pl-5">
            <input
              type="email"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="tvoj@email.primjer"
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
              className="shrink-0 rounded-full bg-honey-500 px-5 py-2.5 text-sm font-semibold text-white transition hover:bg-honey-600 disabled:opacity-60"
            >
              {stanje === "salje" ? "Šaljem…" : "Prijavi se"}
            </button>
          </div>
          {greska && <p className="mt-2 pl-5 text-sm text-red-600">{greska}</p>}
        </form>
      )}
    </div>
  );
}
