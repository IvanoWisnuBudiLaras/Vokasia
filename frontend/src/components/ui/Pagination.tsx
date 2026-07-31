"use client";

import { Button, Icon } from "@/components/ui";

export interface PaginationProps {
  currentPage: number;
  totalItems: number;
  pageSize: number;
  onPageChange: (page: number) => void;
  onPageSizeChange?: (size: number) => void;
  className?: string;
}

export function Pagination({
  currentPage,
  totalItems,
  pageSize,
  onPageChange,
  onPageSizeChange,
  className = "",
}: PaginationProps) {
  const totalPages = Math.max(1, Math.ceil(totalItems / pageSize));
  const startItem = totalItems === 0 ? 0 : (currentPage - 1) * pageSize + 1;
  const endItem = Math.min(totalItems, currentPage * pageSize);

  return (
    <div className={`flex flex-wrap items-center justify-between gap-3 pt-3 text-sm border-t border-border ${className}`}>
      <div className="flex items-center gap-2 text-ink-muted">
        <span>
          Menampilkan <strong className="font-semibold text-ink">{startItem}</strong> -{" "}
          <strong className="font-semibold text-ink">{endItem}</strong> dari{" "}
          <strong className="font-semibold text-ink">{totalItems}</strong> data
        </span>

        {onPageSizeChange && (
          <select
            value={pageSize}
            onChange={(e) => onPageSizeChange(Number(e.target.value))}
            aria-label="Pilih jumlah baris per halaman"
            className="ml-2 h-[36px] rounded-[var(--radius-md)] border border-border bg-surface px-2 text-xs text-ink outline-none focus-visible:outline-2 focus-visible:outline-focus"
          >
            <option value={10}>10 per hal</option>
            <option value={25}>25 per hal</option>
            <option value={50}>50 per hal</option>
            <option value={100}>100 per hal</option>
          </select>
        )}
      </div>

      <div className="flex items-center gap-1.5">
        <Button
          type="button"
          variant="secondary"
          size="md"
          disabled={currentPage <= 1}
          onClick={() => onPageChange(currentPage - 1)}
          aria-label="Halaman Sebelumnya"
          className="px-2.5 text-xs"
        >
          <Icon name="arrow-left" size={16} /> Prev
        </Button>

        <span className="px-2 text-xs font-medium text-ink">
          {currentPage} / {totalPages}
        </span>

        <Button
          type="button"
          variant="secondary"
          size="md"
          disabled={currentPage >= totalPages}
          onClick={() => onPageChange(currentPage + 1)}
          aria-label="Halaman Selanjutnya"
          className="px-2.5 text-xs"
        >
          Next <Icon name="arrow-right" size={16} />
        </Button>
      </div>
    </div>
  );
}
