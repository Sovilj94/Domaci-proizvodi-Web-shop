import { Check } from "lucide-react";
import { PartnerForma } from "@/components/partneri/partner-forma";

export const metadata = { title: "Postani partner" };

const PONUDA = [
  "Otkup bez posrednika",
  "Sedmični ritam koji se zna unaprijed",
  "Mjesto na stranici „Naši domaćini“",
];

export default function PostaniPartnerPage() {
  return (
    <div className="mx-auto max-w-5xl px-4 py-8 sm:py-12">
      <div className="grid gap-10 lg:grid-cols-2">
        <div>
          <h1 className="font-display text-3xl font-bold text-ink sm:text-4xl">Postani partner</h1>
          <p className="mt-3 text-lg text-muted">
            Imate gazdinstvo na Manjači ili u okolini Banje Luke i proizvodite hranu na
            tradicionalan način? Javite nam se.
          </p>
          <ul className="mt-6 space-y-3">
            {PONUDA.map((t) => (
              <li key={t} className="flex items-start gap-3 text-ink">
                <Check className="mt-0.5 h-5 w-5 shrink-0 text-brand-700" aria-hidden />
                {t}
              </li>
            ))}
          </ul>
        </div>
        <div className="relative rounded-card bg-white p-5 shadow-card sm:p-6">
          <PartnerForma />
        </div>
      </div>
    </div>
  );
}
