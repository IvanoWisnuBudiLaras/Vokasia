import { ErrorState } from "@/components/ui";
import { fetcher } from "@/lib/fetcher";
import type { PlanDto } from "@/lib/apiTypes";
import { PlansPageClient } from "./PlansPageClient";

export const dynamic = "force-dynamic";

export default async function SaPlansPage() {
  try {
    const plans = await fetcher<PlanDto[]>("/sa/plans");
    return <PlansPageClient initialPlans={plans} />;
  } catch (err) {
    console.error("[sa/plans] gagal memuat:", err);
    return <ErrorState message="Daftar plan belum bisa dimuat." />;
  }
}
