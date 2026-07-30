/** Next.js App Router: otomatis dirender saat student/page.tsx (Server Component) masih fetch. */
export default function StudentTodayLoading() {
  return (
    <div
      role="status"
      aria-live="polite"
      aria-busy="true"
      aria-label="Memuat jurnal hari ini"
      className="flex animate-pulse flex-col gap-4"
    >
      <div className="flex flex-col gap-2">
        <div className="h-5 w-40 rounded bg-surface-muted" />
        <div className="h-4 w-56 rounded bg-surface-muted" />
      </div>
      <div className="h-12 rounded-[var(--radius-md)] bg-surface-muted" />
      <div className="h-48 rounded-[var(--radius-lg)] bg-surface-muted" />
      <div className="h-14 rounded-[var(--radius-md)] bg-surface-muted" />
    </div>
  );
}
