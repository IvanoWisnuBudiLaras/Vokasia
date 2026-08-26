"use client";

import Link from "next/link";
import { useMemo, useState } from "react";
import { Button, StatusBadge } from "@/components/ui";
import { apiClient, ApiError } from "@/lib/apiClient";
import type { SaUserDto } from "@/lib/apiTypes";

const roleLabels: Record<number, string> = { 1: "TenantAdmin", 2: "DeptHead", 3: "Teacher" };
const roleAccess: Record<number, string> = { 1: "operasi tenant dan user sekolah", 2: "monitoring penempatan dan laporan", 3: "siswa yang ditugaskan dan penilaian" };

export function TenantUsersTable({ tenantId, initialUsers }: { tenantId: string; initialUsers: SaUserDto[] }) {
  const [users, setUsers] = useState(initialUsers);
  const [active, setActive] = useState("active");
  const [role, setRole] = useState("");
  const [search, setSearch] = useState("");
  const [editingId, setEditingId] = useState<string | null>(null);
  const [newRole, setNewRole] = useState(3);
  const [deactivateId, setDeactivateId] = useState<string | null>(null);
  const [reason, setReason] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function refresh(nextActive = active) {
    const query = nextActive === "all" ? "" : `?active=${nextActive === "active"}`;
    setUsers(await apiClient.get<SaUserDto[]>(`/sa/tenants/${tenantId}/users${query}`));
  }

  const visible = useMemo(() => users.filter((user) => {
    if (role && String(user.role) !== role) return false;
    const q = search.trim().toLowerCase();
    return !q || user.fullName.toLowerCase().includes(q) || user.email.toLowerCase().includes(q);
  }), [users, role, search]);

  async function changeRole(userId: string) {
    setBusy(true); setError(null);
    try { await apiClient.put(`/sa/users/${userId}/role`, newRole); setEditingId(null); await refresh(); }
    catch (err) { setError(err instanceof ApiError ? err.message : "Role belum bisa diubah."); }
    finally { setBusy(false); }
  }

  async function deactivate(userId: string) {
    setBusy(true); setError(null);
    try { await apiClient.post(`/sa/users/${userId}/deactivate`, { reason }); setDeactivateId(null); setReason(""); await refresh(); }
    catch (err) { setError(err instanceof ApiError ? err.message : "Akun belum bisa dinonaktifkan."); }
    finally { setBusy(false); }
  }

  async function reactivate(userId: string) {
    setBusy(true); setError(null);
    try { await apiClient.post(`/sa/users/${userId}/reactivate`); await refresh(); }
    catch (err) { setError(err instanceof ApiError ? err.message : "Akun belum bisa diaktifkan kembali."); }
    finally { setBusy(false); }
  }

  function controls(user: SaUserDto) {
    return user.isActive ? <><Button variant="secondary" size="md" className="px-3 text-xs" onClick={() => { setEditingId(user.id); setNewRole(user.role); setDeactivateId(null); }}>Ubah role</Button><Button variant="danger-outline" size="md" className="px-3 text-xs" onClick={() => { setDeactivateId(user.id); setEditingId(null); }}>Nonaktifkan</Button></> : <Button variant="secondary" size="md" className="px-3 text-xs" onClick={() => void reactivate(user.id)} loading={busy}>Aktifkan kembali</Button>;
  }

  function confirmation(user: SaUserDto) {
    if (editingId !== user.id && deactivateId !== user.id) return null;
    const body = editingId === user.id ? <><p className="text-sm font-medium text-ink">Pilih role baru</p><p className="mt-1 text-xs text-ink-muted">Akses akun akan berubah menjadi {roleAccess[newRole] ?? "role baru"}.</p><div className="mt-3 flex flex-wrap items-center gap-2"><select aria-label={`Role baru untuk ${user.fullName}`} value={newRole} onChange={(e) => setNewRole(Number(e.target.value))} className="h-[var(--tap-min)] rounded-[var(--radius-md)] border border-border bg-surface px-2 text-sm"><option value={1}>TenantAdmin</option><option value={2}>DeptHead</option><option value={3}>Teacher</option></select><Button size="md" onClick={() => void changeRole(user.id)} loading={busy}>Konfirmasi perubahan</Button><Button variant="secondary" size="md" onClick={() => setEditingId(null)}>Batal</Button></div></> : <><label className="block text-sm font-medium text-ink" htmlFor={`reason-${user.id}`}>Alasan menonaktifkan</label><textarea id={`reason-${user.id}`} value={reason} onChange={(e) => setReason(e.target.value)} rows={2} className="mt-2 w-full rounded-[var(--radius-md)] border border-border bg-surface p-2 text-sm" placeholder="Contoh: staf sudah tidak bertugas" /><div className="mt-2 flex flex-wrap gap-2"><Button variant="danger" size="md" onClick={() => void deactivate(user.id)} disabled={!reason.trim()} loading={busy}>Konfirmasi nonaktifkan</Button><Button variant="secondary" size="md" onClick={() => { setDeactivateId(null); setReason(""); }}>Batal</Button></div></>;
    const tone = editingId === user.id ? "border-primary bg-brand-soft" : "border-status-red bg-status-red-bg";
    return <div className={`mt-3 border-l-2 p-3 ${tone}`}>{body}</div>;
  }

  function mobileRow(user: SaUserDto) { return <div className="border-b border-border py-4 last:border-0"><div className="min-w-0"><Link href={`/sa/users/${user.id}`} className="font-medium text-primary underline-offset-4 hover:underline">{user.fullName}</Link><p className="text-xs text-ink-muted">{user.email}</p></div><div className="mt-2 flex flex-wrap items-center gap-2"><span className="text-sm text-ink-muted">{roleLabels[user.role] ?? "Role"}</span><StatusBadge status={user.isActive ? "green" : "red"} label={user.isActive ? "Aktif" : "Nonaktif"} /><span className="text-xs text-ink-muted">Dibuat {new Date(user.createdAt).toLocaleDateString("id-ID")}</span></div><div className="mt-3 flex flex-wrap gap-2">{controls(user)}</div>{confirmation(user)}</div>; }

  return <div className="flex flex-col gap-4"><div className="flex flex-wrap items-center gap-2"><input type="search" aria-label="Cari user" value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Cari nama atau email" className="h-[var(--tap-min)] min-w-0 flex-1 rounded-[var(--radius-md)] border border-border bg-surface px-3 text-sm sm:max-w-xs" /><select aria-label="Filter role" value={role} onChange={(e) => setRole(e.target.value)} className="h-[var(--tap-min)] rounded-[var(--radius-md)] border border-border bg-surface px-2 text-sm"><option value="">Semua role</option><option value="1">TenantAdmin</option><option value="2">DeptHead</option><option value="3">Teacher</option></select><select aria-label="Filter status" value={active} onChange={(e) => { setActive(e.target.value); void refresh(e.target.value); }} className="h-[var(--tap-min)] rounded-[var(--radius-md)] border border-border bg-surface px-2 text-sm"><option value="active">Aktif</option><option value="inactive">Nonaktif</option><option value="all">Semua status</option></select></div>{error && <p role="alert" className="border-l-2 border-status-red bg-status-red-bg p-3 text-sm text-ink">{error}</p>}<div className="divide-y divide-border border-y border-border lg:hidden">{visible.map((user) => <div key={user.id}>{mobileRow(user)}</div>)}{visible.length === 0 && <p className="py-6 text-sm text-ink-muted">Tidak ada user yang cocok dengan filter.</p>}</div><div className="hidden overflow-x-auto border-y border-border lg:block"><table className="w-full min-w-[860px] text-left text-sm"><thead className="text-ink-muted"><tr><th className="py-3 pr-4 font-medium">Nama</th><th className="py-3 pr-4 font-medium">Role</th><th className="py-3 pr-4 font-medium">Status</th><th className="py-3 pr-4 font-medium">Dibuat</th><th className="py-3 font-medium">Aksi</th></tr></thead><tbody className="divide-y divide-border">{visible.map((user) => <tr key={user.id}><td className="py-3 pr-4"><Link href={`/sa/users/${user.id}`} className="font-medium text-primary underline-offset-4 hover:underline">{user.fullName}</Link><p className="text-xs text-ink-muted">{user.email}</p></td><td className="py-3 pr-4 text-sm text-ink-muted">{roleLabels[user.role] ?? "Role"}</td><td className="py-3 pr-4"><StatusBadge status={user.isActive ? "green" : "red"} label={user.isActive ? "Aktif" : "Nonaktif"} /></td><td className="py-3 pr-4 text-xs text-ink-muted">{new Date(user.createdAt).toLocaleDateString("id-ID")}</td><td className="py-3"><div className="flex flex-wrap gap-2">{controls(user)}</div>{confirmation(user)}</td></tr>)}{visible.length === 0 && <tr><td colSpan={5} className="py-6 text-sm text-ink-muted">Tidak ada user yang cocok dengan filter.</td></tr>}</tbody></table></div></div>;
}
