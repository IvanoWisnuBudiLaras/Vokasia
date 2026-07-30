"use client";

import { useState } from "react";
import { Button, StatusBadge } from "@/components/ui";
import { apiClient, ApiError } from "@/lib/apiClient";
import type { CompanyDto, Paged } from "@/lib/apiTypes";
import { MergeCompanyDialog } from "./MergeCompanyDialog";

export interface DudiTableProps {
  initialCompanies: CompanyDto[];
}

/** VOK-H6-E2 §1 sa/dudi/page.tsx — registry global: verifikasi usulan + cari duplikat + merge. */
export function DudiTable({ initialCompanies }: DudiTableProps) {
  const [companies, setCompanies] = useState(initialCompanies);
  const [query, setQuery] = useState("");
  const [verifyingId, setVerifyingId] = useState<string | null>(null);
  const [mergingId, setMergingId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

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

  const visible = companies.filter((c) => {
    const q = query.trim().toLowerCase();
    return q.length === 0 || c.name.toLowerCase().includes(q) || (c.city ?? "").toLowerCase().includes(q);
  });

  const mergingCompany = mergingId ? companies.find((c) => c.id === mergingId) : null;

  return (
    <div className="flex flex-col gap-4">
      <div>
        <h1 className="text-xl font-semibold text-ink">DUDI Registry</h1>
        <p className="text-sm text-ink-muted">Registry perusahaan global lintas tenant.</p>
      </div>

      <input
        type="search"
        value={query}
        onChange={(e) => setQuery(e.target.value)}
        placeholder="Cari nama / kota…"
        className="h-9 w-64 rounded-[var(--radius-md)] border border-border bg-surface px-3 text-sm text-ink outline-none focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2"
      />

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

      <div className="overflow-x-auto rounded-[var(--radius-lg)] border border-border">
        <table className="w-full text-left text-sm">
          <thead className="bg-surface-muted">
            <tr>
              <th className="p-3 font-medium text-ink">Nama</th>
              <th className="p-3 font-medium text-ink">Kota</th>
              <th className="p-3 font-medium text-ink">Status</th>
              <th className="p-3 font-medium text-ink">Aksi</th>
            </tr>
          </thead>
          <tbody>
            {visible.map((c) => (
              <tr key={c.id} className="border-t border-border">
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
                        <Button variant="secondary" size="md" loading={verifyingId === c.id} onClick={() => handleVerify(c.id)} className="h-8 px-3 text-xs">
                          Verifikasi
                        </Button>
                      )}
                      <Button variant="secondary" size="md" onClick={() => setMergingId(c.id)} className="h-8 px-3 text-xs">
                        Gabungkan…
                      </Button>
                    </div>
                  )}
                </td>
              </tr>
            ))}
            {visible.length === 0 && (
              <tr>
                <td colSpan={4} className="p-6 text-center text-sm text-ink-muted">Tidak ada company yang cocok.</td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
