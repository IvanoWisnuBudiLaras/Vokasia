"use client";

import { Button, Input } from "@/components/ui";

export function VerifyForm() {
  return (
    <form action="/verify" method="get" className="mt-6">
      <Input
        id="code"
        name="code"
        type="text"
        required
        autoComplete="off"
        spellCheck={false}
        label="Kode sertifikat"
        hint="Kode tercetak di sertifikat atau tersedia melalui QR."
        placeholder="Contoh: VOK-2026-ABC123"
      />
      <Button type="submit" size="lg" className="mt-2 w-full">
        Periksa sertifikat
      </Button>
    </form>
  );
}
