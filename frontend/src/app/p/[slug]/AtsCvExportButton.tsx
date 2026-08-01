"use client";

import { Icon } from "@/components/ui";

export interface AtsCvExportButtonProps {
  studentName: string;
}

export function AtsCvExportButton({ studentName }: AtsCvExportButtonProps) {
  const handleDownloadPdf = () => {
    window.print();
  };

  return (
    <button
      type="button"
      onClick={handleDownloadPdf}
      className="inline-flex min-h-[var(--tap-min)] items-center gap-2 rounded-[var(--radius-md)] border border-primary/30 bg-primary/10 px-4 text-xs font-bold text-primary shadow-sm hover:bg-primary/20 focus-visible:outline-2 focus-visible:outline-focus transition-colors"
    >
      <Icon name="download" size={16} />
      <span>Unduh CV (ATS-Friendly PDF)</span>
    </button>
  );
}
