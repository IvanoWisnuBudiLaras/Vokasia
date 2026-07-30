import { ErrorState } from "@/components/ui";
import { fetcher } from "@/lib/fetcher";
import type { Paged, PlanDto, TenantDto } from "@/lib/apiTypes";
import { TenantsTable } from "./TenantsTable";

export const dynamic = "force-dynamic";

/** VOK-H6-E2 §1 sa/tenants/page.tsx — muat tenant+plan awal (SSR), interaktivitas (cari/filter/wizard/nonaktifkan) di TenantsTable (client). */
export default async function SaTenantsPage() {
  let data: [Paged<TenantDto>, PlanDto[]];
  try {
    data = await Promise.all([
      fetcher<Paged<TenantDto>>("/sa/tenants?pageSize=100"),
      fetcher<PlanDto[]>("/sa/plans"),
    ]);
  } catch (err) {
    console.error("[sa/tenants] gagal memuat:", err);
    return <ErrorState message="Daftar tenant belum bisa dimuat." />;
  }

  return <TenantsTable initialTenants={data[0].items} plans={data[1]} />;
}
