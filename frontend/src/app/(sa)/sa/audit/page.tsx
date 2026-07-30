import { ErrorState, Icon } from "@/components/ui";
import { fetcher } from "@/lib/fetcher";
import type { AuditDto, Paged } from "@/lib/apiTypes";

export const dynamic = "force-dynamic";

interface SearchParams {
  actorId?: string;
  entity?: string;
  from?: string;
  to?: string;
  page?: string;
}

/**
 * VOK-H6-E2 §1 sa/audit/page.tsx — viewer QueryAuditLogs (SA, semua tenant) — filter aktor/entitas/
 * tanggal + pagination lewat GET form (URL search params, TANPA JS - Server Component murni,
 * konsisten pola form filter server-driven, bukan client state spt tabel lain krn audit log
 * murni read-only tanpa aksi mutasi apa pun di halaman ini).
 */
export default async function SaAuditPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const sp = await searchParams;
  const page = Number(sp.page ?? "1") || 1;

  const query = new URLSearchParams();
  if (sp.actorId) query.set("actorId", sp.actorId);
  if (sp.entity) query.set("entity", sp.entity);
  if (sp.from) query.set("from", sp.from);
  if (sp.to) query.set("to", sp.to);
  query.set("page", String(page));
  query.set("pageSize", "30");

  let data: Paged<AuditDto> | null = null;
  let loadError = false;
  try {
    data = await fetcher<Paged<AuditDto>>(`/sa/audit-logs?${query.toString()}`);
  } catch (err) {
    console.error("[sa/audit] gagal memuat:", err);
    loadError = true;
  }

  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;

  function pageHref(p: number) {
    const q = new URLSearchParams();
    if (sp.actorId) q.set("actorId", sp.actorId);
    if (sp.entity) q.set("entity", sp.entity);
    if (sp.from) q.set("from", sp.from);
    if (sp.to) q.set("to", sp.to);
    q.set("page", String(p));
    return `/sa/audit?${q.toString()}`;
  }

  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-xl font-semibold text-ink">Audit Log</h1>

      <form method="get" className="flex flex-wrap items-end gap-3">
        <label className="flex flex-col gap-1 text-sm text-ink">
          Actor ID
          <input name="actorId" defaultValue={sp.actorId} className="h-[var(--tap-min)] w-40 rounded-[var(--radius-md)] border border-border bg-surface px-2 text-sm outline-none focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-1" />
        </label>
        <label className="flex flex-col gap-1 text-sm text-ink">
          Entitas
          <input name="entity" defaultValue={sp.entity} placeholder="mis. Tenant" className="h-[var(--tap-min)] w-40 rounded-[var(--radius-md)] border border-border bg-surface px-2 text-sm outline-none focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-1" />
        </label>
        <label className="flex flex-col gap-1 text-sm text-ink">
          Dari
          <input type="date" name="from" defaultValue={sp.from} className="h-[var(--tap-min)] rounded-[var(--radius-md)] border border-border bg-surface px-2 text-sm outline-none focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-1" />
        </label>
        <label className="flex flex-col gap-1 text-sm text-ink">
          Sampai
          <input type="date" name="to" defaultValue={sp.to} className="h-[var(--tap-min)] rounded-[var(--radius-md)] border border-border bg-surface px-2 text-sm outline-none focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-1" />
        </label>
        <button type="submit" className="h-[var(--tap-min)] whitespace-nowrap rounded-[var(--radius-md)] bg-primary px-4 text-sm font-medium text-primary-ink outline-none hover:bg-primary/90 focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2 active:bg-primary/80">
          Filter
        </button>
      </form>

      {loadError && <ErrorState message="Audit log belum bisa dimuat." />}

      {!loadError && data && (
        <>
          <div className="overflow-x-auto rounded-[var(--radius-lg)] border border-border">
            <table className="w-full text-left text-sm">
              <thead className="bg-surface-muted">
                <tr>
                  <th className="p-3 font-medium text-ink">Waktu</th>
                  <th className="p-3 font-medium text-ink">Actor</th>
                  <th className="p-3 font-medium text-ink">Aksi</th>
                  <th className="p-3 font-medium text-ink">Entitas</th>
                  <th className="p-3 font-medium text-ink">Detail</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((a) => (
                  <tr key={a.id} className="border-t border-border">
                    <td className="p-3 text-ink-muted">{new Date(a.createdAt).toLocaleString("id-ID")}</td>
                    <td className="p-3 text-xs text-ink-muted">
                      {a.actorUserId}
                      {a.actingAsUserId && (
                        <span className="ml-1 rounded-full bg-status-amber-bg px-1.5 py-0.5 text-[10px] font-medium text-status-amber">
                          sbg {a.actingAsUserId}
                        </span>
                      )}
                    </td>
                    <td className="p-3 font-medium text-ink">{a.action}</td>
                    <td className="p-3 text-ink-muted">{a.entity} <span className="text-xs">({a.entityId})</span></td>
                    <td className="p-3 text-xs text-ink-muted">{a.metaJson}</td>
                  </tr>
                ))}
                {data.items.length === 0 && (
                  <tr>
                    <td colSpan={5} className="p-6 text-center text-sm text-ink-muted">Tidak ada log yang cocok filter.</td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>

          <div className="flex flex-col gap-2 text-sm text-ink-muted sm:flex-row sm:items-center sm:justify-between">
            <span>Halaman {page} dari {totalPages} ({data.totalCount} entri)</span>
            <div className="flex flex-wrap gap-2">
              {page > 1 && (
                <a
                  href={pageHref(page - 1)}
                  className="inline-flex min-h-[var(--tap-min)] items-center gap-1 whitespace-nowrap rounded-[var(--radius-md)] border border-border px-3 outline-none transition-[color,background-color,border-color] hover:bg-surface-muted focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2 active:bg-primary-muted"
                >
                  <Icon name="arrow-left" size={16} /> Sebelumnya
                </a>
              )}
              {page < totalPages && (
                <a
                  href={pageHref(page + 1)}
                  className="inline-flex min-h-[var(--tap-min)] items-center gap-1 whitespace-nowrap rounded-[var(--radius-md)] border border-border px-3 outline-none transition-[color,background-color,border-color] hover:bg-surface-muted focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2 active:bg-primary-muted"
                >
                  Berikutnya <Icon name="arrow-right" size={16} />
                </a>
              )}
            </div>
          </div>
        </>
      )}
    </div>
  );
}
