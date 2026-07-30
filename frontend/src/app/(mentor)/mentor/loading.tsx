/** Next.js App Router: otomatis dirender saat mentor/page.tsx (Server Component) masih fetch. */
export default function MentorHomeLoading() {
  return (
    <div
      role="status"
      aria-live="polite"
      aria-busy="true"
      aria-label="Memuat daftar persetujuan"
      className="flex animate-pulse flex-col gap-4"
    >
      <div className="flex flex-col gap-2">
        <div className="h-5 w-40 rounded bg-surface-muted" />
        <div className="h-4 w-48 rounded bg-surface-muted" />
      </div>
      <div className="h-10 rounded-[var(--radius-md)] bg-surface-muted" />
      {[0, 1, 2].map((i) => (
        <div key={i} className="h-16 rounded-[var(--radius-md)] bg-surface-muted" />
      ))}
    </div>
  );
}
