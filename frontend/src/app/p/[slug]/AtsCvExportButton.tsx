export interface AtsCvExportButtonProps {
  slug: string;
}

export function AtsCvExportButton({ slug }: AtsCvExportButtonProps) {
  return (
    <a
      href={`/p/${encodeURIComponent(slug)}/cv`}
      download
      className="inline-flex min-h-[var(--tap-min)] items-center justify-center rounded-[var(--radius-md)] border border-primary/30 bg-primary/10 px-4 text-sm font-medium text-primary hover:bg-primary/20 focus-visible:outline-2 focus-visible:outline-focus"
    >
      Unduh CV ATS (PDF)
    </a>
  );
}
