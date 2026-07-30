export default function Loading() {
  return (
    <main
      role="status"
      aria-live="polite"
      aria-busy="true"
      className="flex flex-1 items-center justify-center bg-surface px-5 py-10"
    >
      <div className="w-full max-w-lg rounded-[var(--radius-lg)] border border-border bg-surface p-6">
        <p className="text-sm font-medium text-ink">Menyiapkan ruang kerja…</p>
        <div className="mt-5 flex flex-col gap-3" aria-hidden="true">
          <div className="h-4 w-2/3 animate-pulse rounded-[var(--radius-sm)] bg-surface-muted" />
          <div className="h-4 w-full animate-pulse rounded-[var(--radius-sm)] bg-surface-muted" />
          <div className="h-24 w-full animate-pulse rounded-[var(--radius-md)] bg-surface-muted" />
        </div>
      </div>
    </main>
  );
}
