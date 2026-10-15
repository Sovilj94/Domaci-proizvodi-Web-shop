import type { Metadata } from "next";
import { AdminGuard } from "@/components/admin/admin-guard";
import { AdminMobileBar, AdminSidebar } from "@/components/admin/admin-nav";

export const metadata: Metadata = {
  title: "Admin panel",
};

export default function AdminLayout({ children }: { children: React.ReactNode }) {
  return (
    <AdminGuard>
      {/*
        Admin nasljeđuje paletu shopa — ista je firma — ali ne i serif display font.
        Playfair je za naslove brošure; u gustom radnom alatu sa tabelama, filterima i
        dugmadima od 12px samo usporava čitanje. Override stoji ovdje, na jednom mjestu,
        umjesto `font-sans` posutog po svakom adminskom naslovu.

        `data-admin` + `translate="no"`: admin ostaje latinica (P7). Pri klijentskoj
        navigaciji iz prodavnice posmatrač ćirilice vidi admin čvorove PRIJE nego React
        ugasi PismoProvider, pa ih isključuje ova oznaka, a ne redoslijed demontaže.
      */}
      <div
        data-admin
        translate="no"
        className="flex min-h-screen bg-sand/40"
        style={{ "--font-display": "var(--font-dm-sans)" } as React.CSSProperties}
      >
        <AdminSidebar />
        <div className="flex min-w-0 flex-1 flex-col">
          <AdminMobileBar />
          <main className="flex-1 p-4 lg:p-8">{children}</main>
        </div>
      </div>
    </AdminGuard>
  );
}
