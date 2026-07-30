/** Next.js App Router: otomatis dirender saat history/page.tsx (Server Component) masih fetch. */
export default function StudentHistoryLoading() {
  return (
    <div
      role="status"
      aria-live="polite"
      aria-busy="true"
      aria-label="Memuat riwayat jurnal"
      className="flex animate-pulse flex-col gap-4"
    >
      <div className="h-5 w-32 rounded bg-surface-muted" />
      <div className="h-10 rounded-[var(--radius-md)] bg-surface-muted" />
      {[0, 1, 2, 3].map((i) => (
        <div key={i} className="h-20 rounded-[var(--radius-md)] bg-surface-muted" />
      ))}
    </div>
  );
}
