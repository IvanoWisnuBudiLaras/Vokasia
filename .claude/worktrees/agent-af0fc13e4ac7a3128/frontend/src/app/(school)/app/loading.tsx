/** Next.js App Router: otomatis dirender saat page.tsx (Server Component) masih fetch periods+dashboard. */
export default function SchoolDashboardLoading() {
  return (
    <div className="flex animate-pulse flex-col gap-4" aria-busy="true" aria-label="Memuat dashboard">
      <div className="h-6 w-48 rounded bg-surface-muted" />
      <div className="grid grid-cols-1 gap-4 md:grid-cols-4">
        {[0, 1, 2, 3].map((i) => (
          <div key={i} className="h-28 rounded-[var(--radius-lg)] bg-surface-muted" />
        ))}
      </div>
      <div className="h-5 w-32 rounded bg-surface-muted" />
      {[0, 1, 2].map((i) => (
        <div key={i} className="h-14 rounded-[var(--radius-md)] bg-surface-muted" />
      ))}
    </div>
  );
}
