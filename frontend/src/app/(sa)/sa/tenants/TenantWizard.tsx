"use client";

import { useState } from "react";
import { Input } from "@/components/ui";
import { MaterialButton } from "@/components/ui/MaterialButton";
import { apiClient, ApiError } from "@/lib/apiClient";
import type { PlanDto, TenantDto } from "@/lib/apiTypes";

export interface TenantWizardProps {
  plans: PlanDto[];
  onCreated: () => void;
  onCancel: () => void;
}

type Step = 1 | 2 | 3;

/**
 * VOK-H6-E2 §1 TenantWizard({plans, onCreated}) — 3 langkah literal ticket: data sekolah -> pilih
 * plan -> admin pertama, lalu satu panggilan `CreateTenant` (wizard backend, 1 transaksi: Tenant+
 * RubricTemplate default+AppUser TenantAdmin+email undangan — lihat SaTenantsEndpoints.CreateTenant).
 * AC: "tenant muncul di tabel + admin menerima undangan (dev inbox)" — email dev diverifikasi manual
 * (DevLogEmailSender menulis ke log/`.emails/`, di luar cakupan komponen FE ini, TenantAdminInvite
 * template sudah ada sejak H6-E1).
 */
export function TenantWizard({ plans, onCreated, onCancel }: TenantWizardProps) {
  const [step, setStep] = useState<Step>(1);
  const [schoolName, setSchoolName] = useState("");
  const [npsn, setNpsn] = useState("");
  const [city, setCity] = useState("");
  const [planId, setPlanId] = useState(plans[0]?.id ?? "");
  const [adminName, setAdminName] = useState("");
  const [adminEmail, setAdminEmail] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [invitationSent, setInvitationSent] = useState(false);

  const step1Valid = schoolName.trim().length > 0 && city.trim().length > 0;
  const step2Valid = planId.length > 0;
  const step3Valid = adminName.trim().length > 0 && adminEmail.trim().length > 0;

  async function handleSubmit() {
    setSubmitting(true);
    setError(null);
    try {
      await apiClient.post<TenantDto>("/sa/tenants", {
        schoolName,
        npsn: npsn.trim().length > 0 ? npsn : null,
        city,
        adminEmail,
        adminName,
        planId,
      });
      setInvitationSent(true);
      onCreated();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Gagal membuat tenant. Coba lagi.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="flex flex-col gap-4 rounded-[var(--radius-lg)] border border-border bg-surface-muted p-4">
      <div className="flex items-center justify-between">
        <h2 className="text-sm font-semibold text-ink">Tenant Baru — Langkah {step}/3</h2>
        <MaterialButton type="button" className="border-border bg-surface text-ink" onClick={onCancel} disabled={submitting}>
          Batal
        </MaterialButton>
      </div>

      {invitationSent && (
        <div role="status" className="border-l-4 border-status-green bg-status-green-bg p-4 text-sm text-ink">
          <strong className="block">Tenant berhasil dibuat.</strong>
          <span>Undangan admin sudah dikirim. Admin akan mengatur kata sandi melalui tautan satu kali.</span>
        </div>
      )}

      {step === 1 && (
        <div className="flex flex-col gap-3">
          <Input label="Nama Sekolah" value={schoolName} onChange={(e) => setSchoolName(e.target.value)} placeholder="SMK Negeri 1 Contoh" />
          <Input label="NPSN (opsional)" value={npsn} onChange={(e) => setNpsn(e.target.value)} />
          <Input label="Kota" value={city} onChange={(e) => setCity(e.target.value)} />
          <MaterialButton type="button" disabled={!step1Valid} onClick={() => setStep(2)} className="self-end border-primary bg-primary text-on-primary disabled:opacity-50">
            Lanjut: Pilih Plan
          </MaterialButton>
        </div>
      )}

      {step === 2 && (
        <div className="flex flex-col gap-3">
          <label className="flex flex-col gap-1 text-sm font-medium text-ink">
            Plan Langganan
            <select
              value={planId}
              onChange={(e) => setPlanId(e.target.value)}
              className="h-[var(--tap-min)] rounded-[var(--radius-md)] border border-border bg-surface px-3 text-sm text-ink outline-none focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2"
            >
              {plans.length === 0 && <option value="">Belum ada plan — buat di /sa/plans dulu</option>}
              {plans.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name} — Rp {p.priceMonthly.toLocaleString("id-ID")}/bln (maks {p.maxStudents} siswa, {p.maxPlacements} placement)
                </option>
              ))}
            </select>
          </label>
          <div className="flex justify-between">
            <MaterialButton type="button" className="border-border bg-surface text-ink" onClick={() => setStep(1)}>
              Kembali
            </MaterialButton>
            <MaterialButton type="button" disabled={!step2Valid} onClick={() => setStep(3)} className="border-primary bg-primary text-on-primary disabled:opacity-50">
              Lanjut: Admin Pertama
            </MaterialButton>
          </div>
        </div>
      )}

      {step === 3 && (
        <div className="flex flex-col gap-3">
          <Input label="Nama Admin" value={adminName} onChange={(e) => setAdminName(e.target.value)} placeholder="Nama TenantAdmin pertama" />
          <Input label="Email Admin" type="email" value={adminEmail} onChange={(e) => setAdminEmail(e.target.value)} placeholder="admin@sekolah.sch.id" />
          {error && <p className="text-sm text-status-red">{error}</p>}
          <div className="flex justify-between">
            <MaterialButton type="button" className="border-border bg-surface text-ink" onClick={() => setStep(2)} disabled={submitting}>
              Kembali
            </MaterialButton>
            <MaterialButton type="button" disabled={!step3Valid || submitting} onClick={handleSubmit} className="border-primary bg-primary text-on-primary disabled:opacity-50">
              {submitting ? "Membuat tenant…" : "Buat tenant dan kirim undangan"}
            </MaterialButton>
          </div>
        </div>
      )}
    </div>
  );
}
