"use client";

import { useMemo, useState } from "react";
import { Icon, StatusBadge } from "@/components/ui";
import { apiClient } from "@/lib/apiClient";
import type { RecapRowDto } from "@/lib/apiTypes";
import { FinalizeButton } from "./FinalizeButton";
import { ExportButton } from "./ExportButton";

export interface RecapTableProps {
  periodId: string;
  initialRows: RecapRowDto[];
  canExport?: boolean;
  canFinalize?: boolean;
}

type SortKey = "studentName" | "companyName" | "mentorAvg" | "teacherAvg" | "finalScore" | "status";
type SortDir = "asc" | "desc";

function statusBadgeFor(status: RecapRowDto["status"]) {
  if (status === "Final") return <StatusBadge status="green" label="Final" />;
  if (status === "Draft") return <StatusBadge status="amber" label="Draft" />;
  return <StatusBadge status="red" label="Belum Dinilai" />;
}

function fmt(n: number | null): string {
  return n === null ? "—" : n.toFixed(2);
}

/**
 * VOK-H5-E2 §3 RecapTable({periodId, initialRows}) — tabel GetGradeRecap dgn sort (klik header)
 * & cari (nama/DUDI), plus toolbar FinalizeButton+ExportButton. Fetch ulang (`refresh()`) dipanggil
 * FinalizeButton.onFinalized supaya tabel langsung tampil status "Final" terbaru TANPA reload
 * halaman penuh (AC: "admin finalize sukses -> semua ScoreForm jadi readOnly + rekap menampilkan
 * final").
 */
export function RecapTable({ periodId, initialRows, canExport = true, canFinalize = true }: RecapTableProps) {
  const [rows, setRows] = useState(initialRows);
  const [query, setQuery] = useState("");
  const [sortKey, setSortKey] = useState<SortKey>("studentName");
  const [sortDir, setSortDir] = useState<SortDir>("asc");
  const [refreshing, setRefreshing] = useState(false);

  async function refresh() {
    setRefreshing(true);
    try {
      const data = await apiClient.get<RecapRowDto[]>(`/periods/${periodId}/grade-recap`);
      setRows(data);
    } finally {
      setRefreshing(false);
    }
  }

  function toggleSort(key: SortKey) {
    if (key === sortKey) {
      setSortDir((d) => (d === "asc" ? "desc" : "asc"));
    } else {
      setSortKey(key);
      setSortDir("asc");
    }
  }

  const visibleRows = useMemo(() => {
    const q = query.trim().toLowerCase();
    const filtered = q.length === 0 ? rows : rows.filter((r) => r.studentName.toLowerCase().includes(q) || r.companyName.toLowerCase().includes(q));

    const sorted = [...filtered].sort((a, b) => {
      const av = a[sortKey];
      const bv = b[sortKey];
      if (av === null && bv === null) return 0;
      if (av === null) return 1;
      if (bv === null) return -1;
      const cmp = typeof av === "number" && typeof bv === "number" ? av - bv : String(av).localeCompare(String(bv));
      return sortDir === "asc" ? cmp : -cmp;
    });
    return sorted;
  }, [rows, query, sortKey, sortDir]);

  const incompleteCount = rows.filter((r) => r.status !== "Final").length;

  const columns: { key: SortKey; label: string }[] = [
    { key: "studentName", label: "Nama" },
    { key: "companyName", label: "DUDI" },
    { key: "mentorAvg", label: "Mentor" },
    { key: "teacherAvg", label: "Guru" },
    { key: "finalScore", label: "Final" },
    { key: "status", label: "Status" },
  ];

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <input
          type="search"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          aria-label="Cari rekap berdasarkan nama siswa atau DUDI"
          placeholder="Cari nama siswa atau DUDI…"
          className="h-[var(--tap-min)] w-full rounded-[var(--radius-md)] border border-border bg-surface px-3 text-sm text-ink outline-none focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2 sm:w-64"
        />
        <div className="flex items-center gap-3">
          {canExport && <ExportButton periodId={periodId} />}
          {canFinalize && <FinalizeButton periodId={periodId} incompleteCount={incompleteCount} rows={rows} onFinalized={refresh} />}
        </div>
      </div>

      {refreshing && <p className="text-xs text-ink-muted">Memuat ulang rekap…</p>}

      <div className="overflow-x-auto rounded-[var(--radius-lg)] border border-border">
        <table className="w-full text-left text-sm">
          <thead className="bg-surface-muted">
            <tr>
              {columns.map((col) => (
                <th key={col.key} className="p-3">
                  <button
                    type="button"
                    onClick={() => toggleSort(col.key)}
                    className="flex min-h-[var(--tap-min)] items-center gap-1 font-medium text-ink outline-none focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2"
                  >
                    {col.label}
                    {sortKey === col.key && <Icon name={sortDir === "asc" ? "chevron-up" : "chevron-down"} size={16} />}
                  </button>
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {visibleRows.map((r) => (
              <tr key={r.placementId} className="border-t border-border">
                <td className="p-3 font-medium text-ink">{r.studentName}</td>
                <td className="p-3 text-ink-muted">{r.companyName}</td>
                <td className="p-3 text-ink">{fmt(r.mentorAvg)}</td>
                <td className="p-3 text-ink">{fmt(r.teacherAvg)}</td>
                <td className="p-3 text-ink">{fmt(r.finalScore)}</td>
                <td className="p-3">{statusBadgeFor(r.status)}</td>
              </tr>
            ))}
            {visibleRows.length === 0 && (
              <tr>
                <td colSpan={columns.length} className="p-6 text-center text-sm text-ink-muted">
                  Tidak ada siswa yang cocok.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
