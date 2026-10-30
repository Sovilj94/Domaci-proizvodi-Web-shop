"use client";

import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { MapPin } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Field, Input } from "@/components/ui/field";
import { AddressPicker } from "@/components/shop/address-picker";
import { LoadingBlock } from "@/components/ui/spinner";
import { useToast } from "@/components/ui/toast";
import { adminApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import { PoslovnaPravila } from "@/components/admin/poslovna-pravila";

/**
 * Magacin — polazište i krajnja tačka svake ture. Jedna vrijednost za cijelu
 * piljaru, pa nema liste ni drawer-a: stranica je sama forma.
 */
export default function AdminWarehousePage() {
  const qc = useQueryClient();
  const { show } = useToast();

  const warehouse = useQuery({
    queryKey: ["warehouse"],
    queryFn: () => adminApi.warehouse(),
  });

  const [name, setName] = useState("");
  const [address, setAddress] = useState("");
  // Tačka koju je korisnik upravo odabrao na mapi. Sačuvana tačka se NE drži ovdje:
  // picker svoju poništava svaki put kad se tekst adrese promijeni (da tačka ne
  // preživi adresu za koju je nađena), pa bi sačuvana vrijednost tu bila obrisana
  // prvim prolazom — u dev modu i bez ijednog korisnikovog poteza, jer StrictMode
  // pokreće efekte dvaput. Sačuvana tačka zato živi u odgovoru upita ispod.
  const [novaTacka, setNovaTacka] = useState<{ lat: number; lon: number } | null>(null);
  const [errors, setErrors] = useState<Record<string, string[]>>({});

  // Naziv je jedino polje koje se popunjava iz odgovora; adresa se ne pamti u bazi
  // (magacin je tačka, ne narudžba), pa polje za pretragu ostaje prazno.
  useEffect(() => {
    if (!warehouse.data) return;
    setName(warehouse.data.name);
  }, [warehouse.data]);

  const sacuvana = warehouse.data
    ? { lat: warehouse.data.latitude, lon: warehouse.data.longitude }
    : null;
  // Bez nove tačke čuva se postojeća — tako preimenovanje magacina ne zahtijeva da se
  // lokacija iznova traži na mapi.
  const zaCuvanje = novaTacka ?? sacuvana;

  const save = useMutation({
    mutationFn: () =>
      adminApi.updateWarehouse({
        name,
        latitude: zaCuvanje!.lat,
        longitude: zaCuvanje!.lon,
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["warehouse"] });
      setErrors({});
      setNovaTacka(null);
      show("Magacin sačuvan");
    },
    onError: (err) => {
      if (err instanceof ApiRequestError) {
        setErrors(err.fieldErrors);
        if (Object.keys(err.fieldErrors).length === 0) show(err.message, "error");
      } else {
        show("Čuvanje nije uspjelo", "error");
      }
    },
  });

  return (
    <div className="space-y-6">
      <div>
        <h1 className="font-display text-2xl font-bold">Magacin</h1>
        <p className="text-muted">
          Polazište i krajnja tačka svake ture. Svaka optimizacija rute računa se od ove
          tačke, pa je mijenjajte samo kad se magacin stvarno preseli.
        </p>
      </div>

      {warehouse.isLoading ? (
        <LoadingBlock />
      ) : warehouse.isError ? (
        <p className="rounded-card bg-white p-6 text-sm text-red-600 shadow-card">
          Magacin nije moguće pročitati. Provjerite da je backend podignut.
        </p>
      ) : (
        <div className="max-w-2xl space-y-4 rounded-card bg-white p-6 shadow-card">
          <Field label="Naziv" required error={errors.name?.[0]}>
            <Input value={name} onChange={(e) => setName(e.target.value)} />
          </Field>

          <Field
            label="Lokacija"
            required
            error={errors.latitude?.[0] ?? errors.longitude?.[0]}
          >
            <AddressPicker
              value={address}
              onChange={setAddress}
              onCoordinates={setNovaTacka}
              initialCoordinates={sacuvana ?? undefined}
              id="magacin"
            />
          </Field>

          <div className="flex items-center gap-2 rounded-xl bg-sand px-3.5 py-2.5 text-sm">
            <MapPin className="h-4 w-4 shrink-0 text-brand-600" />
            {novaTacka ? (
              <span className="text-ink">
                Nova tačka: {novaTacka.lat.toFixed(6)}, {novaTacka.lon.toFixed(6)}
              </span>
            ) : sacuvana ? (
              <span className="text-muted">
                Trenutno:{" "}
                <span className="font-medium text-ink">
                  {sacuvana.lat.toFixed(6)}, {sacuvana.lon.toFixed(6)}
                </span>
              </span>
            ) : null}
          </div>

          <div className="flex justify-end">
            <Button onClick={() => save.mutate()} disabled={!zaCuvanje || save.isPending}>
              {save.isPending ? "Spremam…" : "Spremi"}
            </Button>
          </div>
        </div>
      )}

      <PoslovnaPravila />
    </div>
  );
}
