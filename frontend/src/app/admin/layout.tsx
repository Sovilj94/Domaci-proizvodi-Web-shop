import type { Metadata } from "next";
import { AdminGuard } from "@/components/admin/admin-guard";
import { AdminMobileBar, AdminSidebar } from "@/components/admin/admin-nav";

export const metadata: Metadata = {
  title: "Admin panel",
};

export default function AdminLayout({ children }: { children: React.ReactNode }) {
  return (
    <AdminGuard>
      <div className="flex min-h-screen bg-sand/40">
        <AdminSidebar />
        <div className="flex min-w-0 flex-1 flex-col">
          <AdminMobileBar />
          <main className="flex-1 p-4 lg:p-8">{children}</main>
        </div>
      </div>
    </AdminGuard>
  );
}
