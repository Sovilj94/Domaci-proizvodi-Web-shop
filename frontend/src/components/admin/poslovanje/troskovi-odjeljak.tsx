import Link from "next/link";
import { KarticaPoslovanja } from "@/components/admin/poslovanje/kartica-poslovanja";
import { Odjeljak } from "@/components/admin/poslovanje/odjeljak";
import { Tabela } from "@/components/admin/poslovanje/tabela";
import type { PoslovanjeMjeseca } from "@/lib/api/contracts";
import { procenat, redoviDobiti } from "@/lib/admin/poslovanje";
import { cn, formatPrice } from "@/lib/utils";

const iznos = (x: number) => (x < 0 ? `− ${formatPrice(-x)}` : formatPrice(x));

/** Troškovi mjeseca i dobit: račun red po red, gorivo po dnevnim turama naspram računa, upozorenja. */
export function TroskoviOdjeljak({ d }: { d: PoslovanjeMjeseca }) {
  const t = d.troskovi;
  const g = t.gorivo;
  const link = `/admin/poslovanje/troskovi?godina=${d.godina}&mjesec=${d.mjesec}`;
  const nemaUnosa = t.poVrsti.length === 0 && t.ambalaza === 0 && g.brojRuta === 0;
  return (
    <Odjeljak
      naslov="Troškovi i dobit"
      opis="Neto prihod umanjen za nabavnu robu, ambalažu, gorivo i ostale troškove mjeseca."
    >
      {g.bezCijene && (
        <p className="rounded-card bg-red-50 px-4 py-3 text-sm font-medium text-red-700">
          Ima dana za koje nije unesena cijena goriva — procjena goriva nije potpuna.{" "}
          <Link href={link} className="underline underline-offset-2">
            Unesi cijenu goriva
          </Link>
        </p>
      )}
      {nemaUnosa && (
        <p className="rounded-card bg-honey-100 px-4 py-3 text-sm font-medium text-honey-700">
          Za ovaj mjesec još nema troškova.{" "}
          <Link href={link} className="underline underline-offset-2 print:hidden">
            Unesi troškove
          </Link>
        </p>
      )}

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <KarticaPoslovanja
          label="Troškovi ukupno"
          vrijednost={formatPrice(t.ukupno)}
          sada={t.ukupno}
          prije={d.prethodniTroskovi.ukupno}
          obrnuto
        />
        <KarticaPoslovanja
          label="Gorivo"
          vrijednost={formatPrice(g.uDobiti)}
          hint={
            g.izvor === "racuni"
              ? `po računima · po dnevnim turama ${g.poRutama == null ? "—" : formatPrice(g.poRutama)}`
              : `${g.km.toLocaleString("sr-Latn")} km u ${g.brojDana} vožnji${
                  g.danaSaUnosom > 0 ? ` · ${g.danaSaUnosom} sa pumpe (procjena ${formatPrice(g.procijenjeno)})` : " · procjena"
                }`
          }
        />
        <KarticaPoslovanja
          label="Ambalaža + gorivo po narudžbi"
          vrijednost={formatPrice(t.varijabilniPoNarudzbi)}
          hint={`ambalaža ukupno ${formatPrice(t.ambalaza)}`}
        />
        <KarticaPoslovanja label="Marža dobiti" vrijednost={procenat(t.marzaDobiti)} hint="Dobit / neto prihod" />
      </div>

      <Tabela
        kljuc={(r) => r.stavka}
        redovi={redoviDobiti(d.prihod, t)}
        kolone={[
          {
            naslov: "Stavka",
            vrijednost: (r) => <span className={cn(r.vrsta !== "trosak" && "font-semibold")}>{r.stavka}</span>,
          },
          {
            naslov: "Iznos",
            vrijednost: (r) => (
              <span
                className={cn(
                  "tabular-nums",
                  r.vrsta === "dobit" && "font-display text-base font-bold",
                  r.vrsta === "dobit" && (r.iznos < 0 ? "text-red-700" : "text-brand-700"),
                )}
              >
                {iznos(r.iznos)}
              </span>
            ),
            desno: true,
          },
        ]}
      />
      {t.ambalazaProcjena && (
        <p className="text-xs text-muted">* procjena — dio narudžbi je stariji od pamćenja ambalaže, računat po današnjoj.</p>
      )}
      <p className="text-xs text-muted print:hidden">
        <Link href={link} className="font-semibold text-brand-700 underline-offset-2 hover:underline">
          Unesi ili izmijeni troškove ovog mjeseca →
        </Link>
      </p>
    </Odjeljak>
  );
}
