"use client";

import { useMemo, useState } from "react";
import { Button, Pagination, StatusBadge, TableExportToolbar } from "@/components/ui";
import { apiClient, ApiError } from "@/lib/apiClient";
import type { CompanyDto, Paged } from "@/lib/apiTypes";
import { MergeCompanyDialog } from "./MergeCompanyDialog";

export interface DudiTableProps {
  initialCompanies: CompanyDto[];
}

export function DudiTable({ initialCompanies }: DudiTableProps) {
  const [companies, setCompanies] = useState(initialCompanies);
  const [query, setQuery] = useState("");
  const [verifyingId, setVerifyingId] = useState<string | null>(null);
  const [mergingId, setMergingId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  async function refresh() {
    const data = await apiClient.get<Paged<CompanyDto>>("/sa/companies?pageSize=200");
    setCompanies(data.items);
  }

  async function handleVerify(id: string) {
    setVerifyingId(id);
    setError(null);
    try {
      await apiClient.post(`/sa/companies/${id}/verify`);
      await refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Gagal verifikasi company.");
    } finally {
      setVerifyingId(null);
    }
  }

  const visible = useMemo(() => {
    const q = query.trim().toLowerCase();
    return companies.filter((c) => q.length === 0 || c.name.toLowerCase().includes(q) || (c.city ?? "").toLowerCase().includes(q));
  }, [companies, query]);

  const paginatedCompanies = useMemo(() => {
    return visible.slice((currentPage - 1) * pageSize, currentPage * pageSize);
  }, [visible, currentPage, pageSize]);

  const mergingCompany = mergingId ? companies.find((c) => c.id === mergingId) : null;

  return (
    <div className="flex flex-col gap-4">
      <div>
        <h1 className="text-xl font-semibold text-ink">Daftar Perusahaan DUDI</h1>
        <p className="text-sm text-ink-muted">Daftar perusahaan global mitra PKL lintas sekolah.</p>
      </div>

      <div className="flex flex-wrap items-center justify-between gap-3">
        <input
          type="search"
          value={query}
          onChange={(e) => {
            setQuery(e.target.value);
            setCurrentPage(1);
          }}
          aria-label="Cari DUDI berdasarkan nama atau kota"
          placeholder="Cari nama / kota…"
          className="h-[var(--tap-min)] w-64 rounded-[var(--radius-md)] border border-border bg-surface px-3 text-sm text-ink outline-none focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2"
        />

        <TableExportToolbar
          data={visible}
          filename="daftar_mitra_dudi"
          title="Daftar Perusahaan Mitra DUDI — Vokasia Platform"
          columns={[
            { key: "name", label: "Nama Perusahaan" },
            { key: "city", label: "Kota" },
            { key: "address", label: "Alamat" },
            { key: "isVerified", label: "Status Verifikasi", format: (val) => (val ? "Terverifikasi" : "Belum Terverifikasi") },
          ]}
        />
      </div>

      {error && <p className="text-sm text-status-red">{error}</p>}

      {mergingCompany && (
        <MergeCompanyDialog
          sourceId={mergingCompany.id}
          sourceName={mergingCompany.name}
          onCancel={() => setMergingId(null)}
          onMerged={() => {
            setMergingId(null);
            void refresh();
          }}
        />
      )}

      <div className="overflow-x-auto rounded-[var(--radius-lg)] border border-border bg-surface shadow-sm">
        <table className="w-full text-left text-sm">
          <thead className="bg-surface-muted border-b border-border font-medium text-ink-muted">
            <tr>
              <th className="p-3">Nama</th>
              <th className="p-3">Kota</th>
              <th className="p-3">Status</th>
              <th className="p-3">Aksi</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {paginatedCompanies.map((c) => (
              <tr key={c.id} className="hover:bg-surface-muted/50">
                <td className="p-3 font-medium text-ink">{c.name}</td>
                <td className="p-3 text-ink-muted">{c.city ?? "—"}</td>
                <td className="p-3">
                  {c.mergedIntoId ? (
                    <StatusBadge status="amber" label="Sudah Merged" />
                  ) : c.isVerified ? (
                    <StatusBadge status="green" label="Terverifikasi" />
                  ) : (
                    <StatusBadge status="red" label="Belum Verifikasi" />
                  )}
                </td>
                <td className="p-3">
                  {!c.mergedIntoId && (
                    <div className="flex gap-2">
                      {!c.isVerified && (
                        <Button variant="secondary" size="md" loading={verifyingId === c.id} onClick={() => handleVerify(c.id)} className="px-3 text-xs">
                          Verifikasi
                        </Button>
                      )}
                      <Button variant="secondary" size="md" onClick={() => setMergingId(c.id)} className="px-3 text-xs">
                        Gabungkan…
                      </Button>
                    </div>
                  )}
                </td>
              </tr>
            ))}
            {paginatedCompanies.length === 0 && (
              <tr>
                <td colSpan={4} className="p-6 text-center text-sm text-ink-muted">Tidak ada company yang cocok.</td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      <Pagination
        currentPage={currentPage}
        totalItems={visible.length}
        pageSize={pageSize}
        onPageChange={(page) => setCurrentPage(page)}
        onPageSizeChange={(size) => {
          setPageSize(size);
          setCurrentPage(1);
        }}
      />
    </div>
  );
}
