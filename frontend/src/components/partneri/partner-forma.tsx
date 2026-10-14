"use client";

import { useState } from "react";
import Link from "next/link";
import { useMutation } from "@tanstack/react-query";
import { Button, buttonVariants } from "@/components/ui/button";
import { Field, Input, Textarea } from "@/components/ui/field";
import { useToast } from "@/components/ui/toast";
import { ApiRequestError } from "@/lib/api/client";
import type { PartnerPrijavaInput } from "@/lib/api/contracts";
import { partneriApi } from "@/lib/api/endpoints";

const PRAZNO = {
  ime: "",
  gazdinstvo: "",
  mjesto: "",
  telefon: "",
  email: "",
  proizvodi: "",
  web: "",
};

export function PartnerForma() {
  const { show } = useToast();
  const [form, setForm] = useState(PRAZNO);
  const [errors, setErrors] = useState<Record<string, string[]>>({});
  const [previseZahtjeva, setPreviseZahtjeva] = useState(false);

  const mutation = useMutation({
    mutationFn: (body: PartnerPrijavaInput) => partneriApi.posalji(body),
    onError: (err) => {
      if (err instanceof ApiRequestError) {
        if (err.status === 429) {
          setPreviseZahtjeva(true);
          return;
        }
        if (Object.keys(err.fieldErrors).length > 0) {
          setErrors(err.fieldErrors);
          return;
        }
        show(err.message, "error");
        return;
      }
      show("Slanje nije uspjelo", "error");
    },
  });

  const set =
    (k: keyof typeof PRAZNO) =>
    (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) =>
      setForm((f) => ({ ...f, [k]: e.target.value }));
  const err = (k: string) => errors[`input.${k}`]?.[0] ?? errors[k]?.[0];

  function submit(e: React.FormEvent) {
    e.preventDefault();
    setErrors({});
    setPreviseZahtjeva(false);
    mutation.mutate({
      ime: form.ime,
      gazdinstvo: form.gazdinstvo || undefined,
      mjesto: form.mjesto,
      telefon: form.telefon,
      email: form.email || undefined,
      proizvodi: form.proizvodi,
      web: form.web || undefined,
    });
  }

  if (mutation.isSuccess) {
    return (
      <div role="status" className="space-y-4 py-4">
        <p className="font-display text-xl font-bold text-ink">
          Hvala! Prijava je primljena. Javićemo vam se telefonom.
        </p>
        <Link href="/" className={buttonVariants({ variant: "secondary" })}>
          Nazad na početnu
        </Link>
      </div>
    );
  }

  return (
    <form onSubmit={submit} className="space-y-4" noValidate>
      <Field label="Ime i prezime" htmlFor="pp-ime" required error={err("ime")}>
        <Input id="pp-ime" value={form.ime} onChange={set("ime")} autoComplete="name" required />
      </Field>
      <Field label="Naziv gazdinstva" htmlFor="pp-gazdinstvo" error={err("gazdinstvo")}>
        <Input id="pp-gazdinstvo" value={form.gazdinstvo} onChange={set("gazdinstvo")} />
      </Field>
      <Field label="Mjesto" htmlFor="pp-mjesto" required error={err("mjesto")}>
        <Input id="pp-mjesto" value={form.mjesto} onChange={set("mjesto")} required />
      </Field>
      <Field label="Telefon" htmlFor="pp-telefon" required error={err("telefon")}>
        <Input
          id="pp-telefon"
          type="tel"
          value={form.telefon}
          onChange={set("telefon")}
          autoComplete="tel"
          required
        />
      </Field>
      <Field label="Email" htmlFor="pp-email" error={err("email")}>
        <Input
          id="pp-email"
          type="email"
          value={form.email}
          onChange={set("email")}
          autoComplete="email"
        />
      </Field>
      <Field
        label="Šta proizvodite i u kojim količinama?"
        htmlFor="pp-proizvodi"
        required
        error={err("proizvodi")}
      >
        <Textarea id="pp-proizvodi" value={form.proizvodi} onChange={set("proizvodi")} required />
      </Field>

      {/* Honeypot: ljudi ga ne vide ni ne dohvataju; botovi ga popune. Vrijednost ide kao `web`. */}
      <div aria-hidden="true" className="absolute -left-[9999px] h-0 w-0 overflow-hidden">
        <input
          tabIndex={-1}
          autoComplete="off"
          name="web"
          value={form.web}
          onChange={set("web")}
        />
      </div>

      {previseZahtjeva && (
        <p role="alert" className="text-sm font-medium text-red-700">
          Previše pokušaja, pokušajte za minut.
        </p>
      )}
      <Button type="submit" size="lg" className="w-full" disabled={mutation.isPending}>
        {mutation.isPending ? "Šaljem…" : "Pošalji prijavu"}
      </Button>
    </form>
  );
}
