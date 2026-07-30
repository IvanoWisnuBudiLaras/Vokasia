"use client";

import { useEffect, useState } from "react";
import { Button, Icon } from "@/components/ui";
import { apiClient, ApiError } from "@/lib/apiClient";
import { UserRole, type SchoolUserDto } from "@/lib/apiTypes";

interface ImpersonatePanelProps {
  tenantId: string;
  onClose: () => void;
}

const ROLE_LABEL: Record<number, string> = {
  [UserRole.TenantAdmin]: "Admin Sekolah",
  [UserRole.DeptHead]: "Kaprog",
  [UserRole.Teacher]: "Guru",
};

/**
 * VOK-H6-E3 §2 — target picker StartImpersonation. Panel inline (pola sama MergeCompanyDialog/
 * DeactivateAction — tak ada modal/dialog bersama di codebase ini). Memanggil BFF LANGSUNG
 * (`/api/sa/impersonate/start`, BUKAN lewat `/api/proxy/*`/apiClient) krn rute ini butuh menukar
 * SELURUH sesi Redis (accessToken+refreshToken+user), bukan sekadar proxy 1 panggilan API biasa —
 * lihat app/api/sa/impersonate/start/route.ts. Sukses -> navigasi PENUH (bukan router.push) krn
 * cookie httpOnly yang berubah baru dibaca ulang lewat request baru, RSC cache lama harus dibuang.
 */
export function ImpersonatePanel({ tenantId, onClose }: ImpersonatePanelProps) {
  const [staff, setStaff] = useState<SchoolUserDto[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [startingId, setStartingId] = useState<string | null>(null);
  const [startError, setStartError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    apiClient
      .get<SchoolUserDto[]>(`/sa/tenants/${tenantId}/staff`)
      .then((data) => {
        if (!cancelled) setStaff(data);
      })
      .catch((err) => {
        if (!cancelled) setLoadError(err instanceof ApiError ? err.message : "Gagal memuat daftar staf.");
      });
    return () => {
      cancelled = true;
    };
  }, [tenantId]);

  async function handleImpersonate(targetUserId: string) {
    setStartingId(targetUserId);
    setStartError(null);
    try {
      const res = await fetch("/api/sa/impersonate/start", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ targetUserId }),
      });
      const body = await res.json();
      if (!res.ok) {
        throw new Error(body.message ?? "Gagal memulai impersonasi.");
      }
      // eslint-disable-next-line react-hooks/immutability
      window.location.href = body.redirectTo;
    } catch (err) {
      setStartError(err instanceof Error ? err.message : "Gagal memulai impersonasi.");
      setStartingId(null);
    }
  }

  return (
    <div className="flex flex-col gap-2 rounded-[var(--radius-md)] border border-border bg-surface-muted p-3">
      <div className="flex items-center justify-between">
        <span className="text-xs font-medium text-ink">Login sebagai staf tenant ini</span>
        <button
          type="button"
          onClick={onClose}
          aria-label="Tutup"
          className="flex min-h-[var(--tap-min)] min-w-[var(--tap-min)] items-center justify-center rounded-[var(--radius-md)] text-ink-muted outline-none hover:bg-surface focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2"
        >
          <Icon name="x" size={20} />
        </button>
      </div>

      {loadError && <p className="text-xs text-status-red">{loadError}</p>}
      {startError && <p className="text-xs text-status-red">{startError}</p>}

      {!staff && !loadError && <p className="text-xs text-ink-muted">Memuat…</p>}

      {staff && staff.length === 0 && <p className="text-xs text-ink-muted">Tenant ini belum punya staf aktif.</p>}

      {staff && staff.length > 0 && (
        <ul className="flex flex-col gap-1">
          {staff.map((s) => (
            <li key={s.id} className="flex items-center justify-between gap-2 rounded-[var(--radius-md)] bg-surface p-2">
              <span className="text-xs text-ink">
                {s.fullName} <span className="text-ink-muted">— {ROLE_LABEL[s.role] ?? "Staf"}</span>
              </span>
              <Button
                variant="secondary"
                size="md"
                loading={startingId === s.id}
                onClick={() => handleImpersonate(s.id)}
                className="min-h-[var(--tap-min)] px-3 text-xs"
              >
                Login sebagai
              </Button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
