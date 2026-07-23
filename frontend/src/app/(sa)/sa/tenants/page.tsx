import { ErrorState } from "@/components/ui";
import { fetcher } from "@/lib/fetcher";
import type { Paged, PlanDto, TenantDto } from "@/lib/apiTypes";
import { TenantsTable } from "./TenantsTable";

export const dynamic = "force-dynamic";

/** VOK-H6-E2 §1 sa/tenants/page.tsx — muat tenant+plan awal (SSR), interaktivitas (cari/filter/wizard/nonaktifkan) di TenantsTable (client). */
export default async function SaTenantsPage() {
  try {
    const [tenants, plans] = await Promise.all([
      fetcher<Paged<TenantDto>>("/sa/tenants?pageSize=100"),
      fetcher<PlanDto[]>("/sa/plans"),
    ]);
    return <TenantsTable initialTenants={tenants.items} plans={plans} />;
  } catch (err) {
    console.error("[sa/tenants] gagal memuat:", err);
    return <ErrorState message="Daftar tenant belum bisa dimuat." />;
  }
}
