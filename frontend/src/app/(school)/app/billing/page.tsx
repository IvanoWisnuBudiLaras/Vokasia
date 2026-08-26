import { ErrorState } from "@/components/ui";
import { fetcher } from "@/lib/fetcher";
import type { BankTransferInstructionsDto, InvoiceDto, SubscriptionDto } from "@/lib/apiTypes";
import { BillingTable } from "./BillingTable";

export const dynamic = "force-dynamic";

export default async function BillingPage() {
  let invoices: InvoiceDto[] = [];
  let subscription: SubscriptionDto | null = null;
  let bankInstructions: BankTransferInstructionsDto | null = null;
  let loadError = false;

  try {
    const [invs, sub, bank] = await Promise.all([
      fetcher<InvoiceDto[]>("/invoices"),
      fetcher<SubscriptionDto>("/invoices/subscription").catch(() => null),
      fetcher<BankTransferInstructionsDto>("/invoices/bank-instructions").catch(() => null),
    ]);
    invoices = invs;
    subscription = sub;
    bankInstructions = bank;
  } catch (err) {
    console.error("[app/billing] gagal memuat:", err);
    loadError = true;
  }

  return (
    <div className="flex flex-col gap-4">
      {loadError ? (
        <ErrorState message="Billing belum bisa dimuat (mungkin kamu tidak punya akses — hanya Admin Sekolah)." />
      ) : (
        <BillingTable
          initialInvoices={invoices}
          initialSubscription={subscription}
          bankInstructions={bankInstructions}
        />
      )}
    </div>
  );
}
