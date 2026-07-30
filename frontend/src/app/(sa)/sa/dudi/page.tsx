import { ErrorState } from "@/components/ui";
import { fetcher } from "@/lib/fetcher";
import type { CompanyDto, Paged } from "@/lib/apiTypes";
import { DudiTable } from "./DudiTable";

export const dynamic = "force-dynamic";

export default async function SaDudiPage() {
  let companies: Paged<CompanyDto>;
  try {
    companies = await fetcher<Paged<CompanyDto>>("/sa/companies?pageSize=200");
  } catch (err) {
    console.error("[sa/dudi] gagal memuat:", err);
    return <ErrorState message="Registry DUDI belum bisa dimuat." />;
  }

  return <DudiTable initialCompanies={companies.items} />;
}
