"use client";

import { useEffect, useState } from "react";
import { apiClient, ApiError } from "@/lib/apiClient";
import type { TeacherLearningRecordPlacementDto, TeacherMonitoringEventDto, TeacherMonitoringWorkspaceDto, LearningRecordMonitoringStatus, LearningRecordMonitoringVisibility } from "@/lib/apiTypes";
import { TeacherMonitoringTimeline } from "./TeacherMonitoringTimeline";
import { TeacherLearningRecordDetail } from "./TeacherLearningRecordDetail";

const statuses: Array<[LearningRecordMonitoringStatus, string]> = [
  ["ProgressingAsExpected", "Berjalan sesuai rencana"],
  ["NeedsAttention", "Perlu perhatian"],
  ["Problem", "Ada masalah"],
];

export function TeacherLearningRecord({ initialWorkspace }: { initialWorkspace: TeacherMonitoringWorkspaceDto }) {
  const [workspace, setWorkspace] = useState(initialWorkspace);
  const [placementId, setPlacementId] = useState(initialWorkspace.placements[0]?.placementId ?? "");
  const [detail, setDetail] = useState<TeacherLearningRecordPlacementDto | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailError, setDetailError] = useState<string | null>(null);
  const [status, setStatus] = useState<LearningRecordMonitoringStatus>("ProgressingAsExpected");
  const [visibility, setVisibility] = useState<LearningRecordMonitoringVisibility>("Internal");
  const [note, setNote] = useState("");
  const [followUpContext, setFollowUpContext] = useState("");
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    async function loadDetail() {
      if (!placementId) return;
      setDetail(null);
      setDetailLoading(true);
      setDetailError(null);
      try {
        const result = await apiClient.get<TeacherLearningRecordPlacementDto>(`/placements/${placementId}/teacher-learning-record`);
        if (!cancelled) setDetail(result);
      } catch (cause) {
        if (!cancelled) setDetailError(cause instanceof ApiError ? cause.message : "Detail Learning Record belum dapat dimuat.");
      } finally {
        if (!cancelled) setDetailLoading(false);
      }
    }
    void loadDetail();
    return () => { cancelled = true; };
  }, [placementId]);

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!placementId) return;
    setBusy(true); setMessage(null); setError(null);
    try {
      const item = await apiClient.post<TeacherMonitoringEventDto>(`/placements/${placementId}/teacher-monitoring`, {
        status, visibility, note: note || null, followUpContext: followUpContext || null, followUpVisitId: null,
      });
      setWorkspace((current) => ({ ...current, events: [item, ...current.events] }));
      setNote(""); setFollowUpContext(""); setMessage("Catatan monitoring tersimpan sebagai riwayat baru.");
    } catch (cause) {
      setError(cause instanceof ApiError ? cause.message : "Catatan monitoring belum tersimpan. Coba lagi.");
    } finally { setBusy(false); }
  }

  return <div className="flex max-w-6xl flex-col gap-6">
    <header><p className="text-sm font-medium text-primary">Learning Record</p><h1 className="text-3xl font-extrabold tracking-tight text-ink">Monitoring perkembangan</h1><p className="mt-1 max-w-2xl text-base text-ink-muted">Catat kondisi siswa secara manual dan lihat tindak lanjut tanpa mengubah hasil penilaian Mentor Industri.</p></header>
    {message && <p role="status" className="border border-status-green/40 bg-status-green-bg p-3 text-sm text-ink">{message}</p>}
    {error && <p role="alert" className="border border-status-red/40 bg-status-red-bg p-3 text-sm text-status-red">{error}</p>}
    <section className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_22rem]">
      <div className="flex flex-col gap-6">
        {detailLoading && <p role="status" className="border border-border bg-surface p-4 text-sm text-ink-muted">Memuat detail Learning Record...</p>}
        {detailError && <p role="alert" className="border border-status-red/40 bg-status-red-bg p-4 text-sm text-status-red">{detailError}</p>}
        {detail && <TeacherLearningRecordDetail detail={detail} />}
        <TeacherMonitoringTimeline workspace={workspace} />
      </div>
      <form onSubmit={submit} className="flex flex-col gap-4 border border-border bg-surface p-5">
        <div><h2 className="text-lg font-semibold text-ink">Catat monitoring</h2><p className="mt-1 text-sm text-ink-muted">Setiap simpan membuat riwayat baru.</p></div>
        <label className="text-sm font-medium text-ink">Siswa dan placement<select required value={placementId} onChange={(event) => setPlacementId(event.target.value)} className="mt-1 h-11 w-full border border-border bg-surface px-3"><option value="">Pilih placement</option>{workspace.placements.map((item) => <option key={item.placementId} value={item.placementId}>{item.studentName} · {item.companyName}</option>)}</select></label>
        <label className="text-sm font-medium text-ink">Status<select value={status} onChange={(event) => setStatus(event.target.value as LearningRecordMonitoringStatus)} className="mt-1 h-11 w-full border border-border bg-surface px-3">{statuses.map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>
        <label className="text-sm font-medium text-ink">Visibilitas<select value={visibility} onChange={(event) => setVisibility(event.target.value as LearningRecordMonitoringVisibility)} className="mt-1 h-11 w-full border border-border bg-surface px-3"><option value="Internal">Internal sekolah</option><option value="StudentVisible">Dapat dilihat siswa</option></select></label>
        <label className="text-sm font-medium text-ink">Catatan{status !== "ProgressingAsExpected" && <span className="text-status-red"> wajib untuk status ini</span>}<textarea required={status !== "ProgressingAsExpected"} value={note} onChange={(event) => setNote(event.target.value)} maxLength={2000} rows={4} className="mt-1 w-full border border-border bg-surface p-3 text-sm" placeholder="Tulis kondisi dan tindak lanjut yang relevan." /></label>
        <label className="text-sm font-medium text-ink">Konteks tindak lanjut <span className="font-normal text-ink-muted">opsional</span><textarea value={followUpContext} onChange={(event) => setFollowUpContext(event.target.value)} maxLength={1000} rows={2} className="mt-1 w-full border border-border bg-surface p-3 text-sm" /></label>
        <button type="submit" disabled={busy || !placementId} className="min-h-11 border border-primary bg-primary px-4 text-sm font-semibold text-primary-ink disabled:opacity-50">{busy ? "Menyimpan..." : "Simpan monitoring"}</button>
      </form>
    </section>
  </div>;
}
