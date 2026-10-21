import type { Metadata } from "next";
import { AdminGuard } from "@/components/admin/admin-guard";
import { AdminMobileBar, AdminSidebar } from "@/components/admin/admin-nav";
import { KomandnaPaleta } from "@/components/admin/komandna-paleta";

export const metadata: Metadata = {
  title: "Admin panel",
};

export default function AdminLayout({ children }: { children: React.ReactNode }) {
  return (
    <AdminGuard>
      {/*
        Admin nasljeđuje paletu i font shopa (od R1 Poppins za sve, pa override display
        fonta više ne treba).

        `data-admin` + `translate="no"`: admin ostaje latinica (P7). Pri klijentskoj
        navigaciji iz prodavnice posmatrač ćirilice vidi admin čvorove PRIJE nego React
        ugasi PismoProvider, pa ih isključuje ova oznaka, a ne redoslijed demontaže.
      */}
      <div data-admin translate="no" className="flex min-h-screen bg-sand/40">
        <AdminSidebar />
        <div className="flex min-w-0 flex-1 flex-col">
          <AdminMobileBar />
          <main className="flex-1 p-4 lg:p-8">{children}</main>
        </div>
        <KomandnaPaleta />
      </div>
    </AdminGuard>
  );
}
