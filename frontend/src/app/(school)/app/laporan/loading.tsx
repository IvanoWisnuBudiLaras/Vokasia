export default function ReportingLoading() {
  return <div className="flex flex-col gap-6" aria-busy="true" aria-label="Memuat laporan"><div className="h-20 animate-pulse border-b border-border bg-surface-muted" /><div className="h-10 w-48 animate-pulse bg-surface-muted" /><div className="flex flex-col gap-3 border-y border-border py-4"><div className="h-5 w-56 animate-pulse bg-surface-muted" /><div className="h-4 w-full animate-pulse bg-surface-muted" /><div className="h-4 w-4/5 animate-pulse bg-surface-muted" /></div></div>;
}
