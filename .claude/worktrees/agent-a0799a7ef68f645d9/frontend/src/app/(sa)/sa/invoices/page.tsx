import { ErrorState } from "@/components/ui";
import { fetcher } from "@/lib/fetcher";
import type { InvoiceDto } from "@/lib/apiTypes";
import { InvoicesTable } from "./InvoicesTable";

export const dynamic = "force-dynamic";

export default async function SaInvoicesPage() {
  try {
    const invoices = await fetcher<InvoiceDto[]>("/sa/invoices");
    return <InvoicesTable initialInvoices={invoices} />;
  } catch (err) {
    console.error("[sa/invoices] gagal memuat:", err);
    return <ErrorState message="Daftar invoice belum bisa dimuat." />;
  }
}
