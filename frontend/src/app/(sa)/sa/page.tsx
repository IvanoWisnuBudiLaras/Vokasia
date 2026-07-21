import { EmptyState } from "@/components/ui";

/** Placeholder H1 — diisi KPI+health nyata di H6-E2 (GetPlatformKpis, GetSystemHealth). */
export default function SuperAdminHomePage() {
  return (
    <EmptyState
      icon="📊"
      title="Dashboard Superadmin"
      description="KPI platform & system health akan tampil di sini (H6)."
    />
  );
}
