import { EmptyState } from "@/components/ui";
import { getSession } from "@/lib/session";

export const dynamic = "force-dynamic";

/**
 * VOK-H2-E2: sapaan nama via session. Daftar tenant BELUM ditampilkan: belum ada endpoint
 * GET /api/tenants di backend (registry tenant = scope H6-E1, sesuai placeholder asli) — bukan
 * gap yang lahir di ticket ini. KPI+health nyata tetap H6-E2 (GetPlatformKpis, GetSystemHealth).
 */
export default async function SuperAdminHomePage() {
  const session = await getSession();

  return (
    <EmptyState
      icon="📊"
      title={session ? `Halo, ${session.name}` : "Dashboard Superadmin"}
      description="KPI platform & system health akan tampil di sini (H6)."
    />
  );
}
