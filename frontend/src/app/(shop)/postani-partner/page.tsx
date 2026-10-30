import { stranica } from "@/lib/seo/metadata";
import { PartnerForma } from "@/components/partneri/partner-forma";
import { PartnerPonuda } from "@/components/partneri/partner-ponuda";

export const metadata = stranica({
  naslov: "Postani partner",
  opis: "Imaš domaćinstvo na Manjači ili u okolini? Prodaj svoje proizvode kupcima u Banjoj Luci bez posrednika, u sedmičnom ritmu.",
  putanja: "/postani-partner",
});

export default function PostaniPartnerPage() {
  return (
    <div className="mx-auto max-w-5xl px-4 py-8 sm:py-12">
      <div className="grid gap-10 lg:grid-cols-2">
        <div>
          <h1 className="text-[32px] font-bold leading-tight text-ink sm:text-5xl">Postani partner</h1>
          <p className="mt-3 text-lg text-muted">
            Imate gazdinstvo na Manjači ili u okolini Banje Luke i proizvodite hranu na
            tradicionalan način? Javite nam se.
          </p>
          <PartnerPonuda />
        </div>
        <div className="relative rounded-card bg-white p-5 shadow-card sm:p-6">
          <PartnerForma />
        </div>
      </div>
    </div>
  );
}
