import { ErrorState } from "@/components/ui";
import { fetcher } from "@/lib/fetcher";
import type { Paged, PlanDto, TenantDto } from "@/lib/apiTypes";
import { TenantsTable } from "./TenantsTable";
import type { SaTenantUsageDto, TenantDetailDto } from "@/lib/apiTypes";
import { TenantQuickPanel } from "./TenantQuickPanel";

export const dynamic = "force-dynamic";

/** VOK-H6-E2 §1 sa/tenants/page.tsx — muat tenant+plan awal (SSR), interaktivitas (cari/filter/wizard/nonaktifkan) di TenantsTable (client). */
export default async function SaTenantsPage({ searchParams }: { searchParams: Promise<{ selected?: string }> }) {
  const { selected } = await searchParams;
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

  let selectedDetail: TenantDetailDto | null = null;
  let selectedUsage: SaTenantUsageDto | null = null;
  if (selected) {
    try {
      [selectedDetail, selectedUsage] = await Promise.all([
        fetcher<TenantDetailDto>(`/sa/tenants/${selected}`),
        fetcher<SaTenantUsageDto>(`/sa/tenants/${selected}/usage`),
      ]);
    } catch (err) {
      console.error("[sa/tenants] gagal memuat panel tenant:", err);
    }
  }

  return <div className={selectedDetail && selectedUsage ? "grid gap-8 lg:grid-cols-[minmax(0,1fr)_360px]" : ""}><TenantsTable initialTenants={data[0].items} plans={data[1]} /><div>{selectedDetail && selectedUsage && <TenantQuickPanel detail={selectedDetail} usage={selectedUsage} />}</div></div>;
}
