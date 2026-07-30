"use client";

import { useRef, useState } from "react";
import { Button } from "@/components/ui";

export interface SignaturePadProps {
  onChange: (dataUrl: string | null) => void;
  disabled?: boolean;
}

/**
 * VOK-H5-E2 §1 SignaturePad({onChange}) — canvas gambar tanda tangan pembimbing industri saat
 * kunjungan guru (W4). Pointer Events (bukan mouse+touch terpisah) — satu handler utk mouse/stylus/
 * jari, `touch-action: none` di canvas supaya jari yang menggambar tidak ikut men-scroll halaman
 * (masalah umum canvas-tanda-tangan mobile).
 *
 * Output: `onChange(dataUrl)` dipanggil tiap goresan selesai (pointerup) dgn `image/png` base64 data
 * URL persis format yang diharapkan `CreateVisitRequest.SignatureDataUrl` (backend decode server-side,
 * lihat VisitEndpoints.TryUploadSignatureAsync) — TIDAK ada upload terpisah dari komponen ini,
 * murni canvas -> dataURL, VisitForm yang mengirim ke API saat submit.
 */
export function SignaturePad({ onChange, disabled }: SignaturePadProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const drawingRef = useRef(false);
  const [hasStroke, setHasStroke] = useState(false);

  function getContext() {
    const canvas = canvasRef.current;
    if (!canvas) return null;
    return canvas.getContext("2d");
  }

  function pointFromEvent(e: React.PointerEvent<HTMLCanvasElement>) {
    const canvas = canvasRef.current;
    if (!canvas) return { x: 0, y: 0 };
    const rect = canvas.getBoundingClientRect();
    return { x: e.clientX - rect.left, y: e.clientY - rect.top };
  }

  function handlePointerDown(e: React.PointerEvent<HTMLCanvasElement>) {
    if (disabled) return;
    const ctx = getContext();
    if (!ctx) return;
    drawingRef.current = true;
    const { x, y } = pointFromEvent(e);
    ctx.beginPath();
    ctx.moveTo(x, y);
    canvasRef.current?.setPointerCapture(e.pointerId);
  }

  function handlePointerMove(e: React.PointerEvent<HTMLCanvasElement>) {
    if (!drawingRef.current || disabled) return;
    const ctx = getContext();
    if (!ctx) return;
    const { x, y } = pointFromEvent(e);
    ctx.lineWidth = 2.5;
    ctx.lineCap = "round";
    ctx.strokeStyle = "#1a1a1a";
    ctx.lineTo(x, y);
    ctx.stroke();
    if (!hasStroke) setHasStroke(true);
  }

  function handlePointerUp() {
    if (!drawingRef.current) return;
    drawingRef.current = false;
    const canvas = canvasRef.current;
    if (canvas && hasStroke) {
      onChange(canvas.toDataURL("image/png"));
    }
  }

  function handleClear() {
    const canvas = canvasRef.current;
    const ctx = getContext();
    if (!canvas || !ctx) return;
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    setHasStroke(false);
    onChange(null);
  }

  return (
    <div className="flex flex-col gap-1.5">
      <span className="text-sm font-medium text-ink">Tanda tangan pembimbing industri</span>
      <canvas
        ref={canvasRef}
        width={320}
        height={140}
        style={{ touchAction: "none" }}
        className="w-full max-w-[320px] rounded-[var(--radius-md)] border border-dashed border-border bg-surface"
        onPointerDown={handlePointerDown}
        onPointerMove={handlePointerMove}
        onPointerUp={handlePointerUp}
        onPointerLeave={handlePointerUp}
        aria-label="Kanvas tanda tangan — gambar dengan jari atau stylus"
      />
      <div className="flex items-center justify-between">
        <span className="text-xs text-ink-muted">{hasStroke ? "Tanda tangan tersimpan" : "Belum ada tanda tangan"}</span>
        <Button type="button" variant="secondary" size="md" disabled={disabled || !hasStroke} onClick={handleClear}>
          Hapus / Ulangi
        </Button>
      </div>
    </div>
  );
}
