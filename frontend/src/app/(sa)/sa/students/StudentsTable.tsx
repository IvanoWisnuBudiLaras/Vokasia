"use client";

import { useState } from "react";
import { Pagination, TableExportToolbar, ImportStudentsModal } from "@/components/ui";
import type { SaStudentDto } from "@/lib/apiTypes";

export interface StudentsTableProps {
  initialStudents: SaStudentDto[];
  totalCount: number;
}

export function StudentsTable({ initialStudents, totalCount }: StudentsTableProps) {
  const [query, setQuery] = useState("");
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  const filtered = initialStudents.filter((s) => {
    const q = query.trim().toLowerCase();
    return (
      q.length === 0 ||
      s.fullName.toLowerCase().includes(q) ||
      s.schoolName.toLowerCase().includes(q) ||
      s.classroom.toLowerCase().includes(q) ||
      (s.nisn ?? "").includes(q)
    );
  });

  const paginatedRows = filtered.slice((currentPage - 1) * pageSize, currentPage * pageSize);

  const [isImportModalOpen, setIsImportModalOpen] = useState(false);

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <input
          type="search"
          value={query}
          onChange={(e) => {
            setQuery(e.target.value);
            setCurrentPage(1);
          }}
          placeholder="Cari nama / NISN / sekolah / kelas…"
          className="h-[var(--tap-min)] w-72 rounded-[var(--radius-md)] border border-border bg-surface px-3 text-sm text-ink outline-none focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2"
        />

        <div className="flex items-center gap-3">
          <button
            type="button"
            onClick={() => setIsImportModalOpen(true)}
            className="inline-flex h-[var(--tap-min)] items-center justify-center rounded-[var(--radius-md)] border border-border bg-surface px-4 text-sm font-medium text-ink shadow-sm transition-colors hover:bg-surface-muted focus-visible:outline-2 focus-visible:outline-focus"
          >
            📥 Import CSV Siswa
          </button>

          <TableExportToolbar
            data={filtered}
            filename="daftar_siswa_seluruh_sekolah"
            title="Daftar Siswa Seluruh Sekolah (SuperAdmin Platform View)"
            columns={[
              { key: "schoolName", label: "Sekolah (Tenant)" },
              { key: "fullName", label: "Nama Siswa" },
              { key: "nisn", label: "NISN", format: (val) => val ?? "-" },
              { key: "majorName", label: "Jurusan" },
              { key: "classroom", label: "Kelas" },
            ]}
          />
        </div>
      </div>

      <div className="overflow-x-auto rounded-[var(--radius-md)] border border-border bg-surface shadow-sm">
        <table className="w-full text-left text-sm">
          <thead className="border-b border-border bg-surface-muted font-medium text-ink-muted">
            <tr>
              <th className="p-3">Sekolah (Tenant)</th>
              <th className="p-3">Nama Siswa</th>
              <th className="p-3">NISN</th>
              <th className="p-3">Jurusan</th>
              <th className="p-3">Kelas</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {paginatedRows.length === 0 ? (
              <tr>
                <td colSpan={5} className="p-6 text-center text-ink-muted">
                  Tidak ada data siswa yang cocok dengan pencarian.
                </td>
              </tr>
            ) : (
              paginatedRows.map((s) => (
                <tr key={s.id} className="hover:bg-surface-muted/50">
                  <td className="p-3 font-medium text-ink">{s.schoolName}</td>
                  <td className="p-3 text-ink">{s.fullName}</td>
                  <td className="p-3 text-ink-muted">{s.nisn ?? "—"}</td>
                  <td className="p-3 text-ink-muted">{s.majorName}</td>
                  <td className="p-3 text-ink-muted">{s.classroom}</td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      <Pagination
        currentPage={currentPage}
        totalItems={filtered.length}
        pageSize={pageSize}
        onPageChange={(page) => setCurrentPage(page)}
        onPageSizeChange={(size) => {
          setPageSize(size);
          setCurrentPage(1);
        }}
      />

      <ImportStudentsModal
        isOpen={isImportModalOpen}
        onClose={() => setIsImportModalOpen(false)}
      />
    </div>
  );
}
