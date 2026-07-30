import { ErrorState } from "@/components/ui";
import { fetcher } from "@/lib/fetcher";
import type { InvoiceDto } from "@/lib/apiTypes";
import { BillingTable } from "./BillingTable";

export const dynamic = "force-dynamic";

/**
 * VOK-H6-E2 §3 app/billing/page.tsx — Server Component, ListMyInvoices (TenantAdminOnly). Halaman
 * ini SENDIRI tetap bisa diakses lewat guard matrix /app (TenantAdmin/DeptHead/Teacher, VOK-H2-E2)
 * tp backend policy `/api/invoices` = TenantAdminOnly murni (lebih sempit dari guard) — DeptHead/
 * Teacher yang membuka /app/billing akan dapat 403 dari fetcher, ditangkap try/catch di bawah
 * (pola sama rekap/page.tsx: halaman boleh dibuka, backend yang menegakkan RBAC sesungguhnya).
 */
export default async function BillingPage() {
  let invoices: InvoiceDto[] = [];
  let loadError = false;

  try {
    invoices = await fetcher<InvoiceDto[]>("/invoices");
  } catch (err) {
    console.error("[app/billing] gagal memuat:", err);
    loadError = true;
  }

  return (
    <div className="flex flex-col gap-4">
      {loadError ? (
        <ErrorState message="Billing belum bisa dimuat (mungkin kamu tidak punya akses — hanya Admin Sekolah)." />
      ) : (
        <BillingTable initialInvoices={invoices} />
      )}
    </div>
  );
}
