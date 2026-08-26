"use client";

import { FormEvent, useEffect, useState } from "react";
import { useSearchParams } from "next/navigation";

export default function SetPasswordPage() {
  const params = useSearchParams();
  const token = params.get("token") ?? "";
  const [state, setState] = useState<"loading" | "valid" | "invalid" | "expired" | "used" | "network" | "success">(() => token ? "loading" : "invalid");
  const [password, setPassword] = useState("");
  const [confirmation, setConfirmation] = useState("");
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!token) return;
    fetch(`/api/staff-invitations/${encodeURIComponent(token)}`)
      .then(async (response) => {
        if (response.ok) setState("valid");
        else if (response.status === 409) setState("expired");
        else setState("invalid");
      })
      .catch(() => setState("network"));
  }, [token]);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (password.length < 8 || password !== confirmation) { setError("Kata sandi minimal 8 karakter dan harus sama."); return; }
    setError(null);
    const response = await fetch(`/api/staff-invitations/${encodeURIComponent(token)}/password`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ password }) });
    if (response.ok) setState("success");
    else if (response.status === 409) {
      const body = await response.json().catch(() => ({}));
      setState(body.code === "used_invitation" ? "used" : "expired");
    } else if (response.status === 404) setState("invalid");
    else if (response.status === 422) setError("Kata sandi belum memenuhi aturan. Gunakan minimal 8 karakter dan perbaiki bidang yang ditandai.");
    else setState("network");
  }

  if (state === "loading") return <main className="p-6">Memeriksa undangan…</main>;
  if (state === "invalid") return <main className="p-6">Tautan undangan tidak valid.</main>;
  if (state === "expired") return <main className="p-6">Tautan undangan sudah kedaluwarsa.</main>;
  if (state === "used") return <main className="p-6">Tautan undangan sudah digunakan.</main>;
  if (state === "network") return <main className="p-6"><p>Undangan belum dapat diperiksa.</p><button type="button" onClick={() => window.location.reload()} className="mt-3 min-h-11 border px-4">Coba lagi</button></main>;
  if (state === "success") return <main className="p-6">Kata sandi berhasil diatur. Silakan masuk.</main>;
  return <main className="mx-auto flex max-w-md flex-col gap-4 p-6">
    <h1 className="text-2xl font-bold">Atur kata sandi</h1>
    <p className="text-sm">Gunakan minimal 8 karakter. Jangan gunakan kata sandi yang mudah ditebak.</p>
    <form onSubmit={submit} className="flex flex-col gap-3">
      <label>Kata sandi<input aria-label="Kata sandi" type="password" value={password} onChange={(e) => setPassword(e.target.value)} required className="h-11 border p-2" /></label>
      <label>Ulangi kata sandi<input aria-label="Ulangi kata sandi" type="password" value={confirmation} onChange={(e) => setConfirmation(e.target.value)} required className="h-11 border p-2" /></label>
      {error && <p role="alert">{error}</p>}
      <button type="submit" className="min-h-11 border px-4">Atur kata sandi</button>
    </form>
  </main>;
}
