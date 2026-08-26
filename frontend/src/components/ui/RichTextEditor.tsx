"use client";

import dynamic from "next/dynamic";
import { useEffect, useState } from "react";
import "react-quill-new/dist/quill.snow.css";

const ReactQuill = dynamic(() => import("react-quill-new"), { ssr: false });

interface RichTextEditorProps {
  label: string;
  value: string;
  onChange: (value: string) => void;
  disabled?: boolean;
  maxLength?: number;
}

const modules = {
  toolbar: [
    [{ header: [1, 2, false] }],
    ["bold", "italic", "underline", "strike"],
    [{ list: "ordered" }, { list: "bullet" }],
    ["clean"],
  ],
};

const formats = [
  "header",
  "bold",
  "italic",
  "underline",
  "strike",
  "list",
];

export function RichTextEditor({ label, value, onChange, disabled = false, maxLength = 500 }: RichTextEditorProps) {
  const [mounted, setMounted] = useState(false);

  useEffect(() => {
    setMounted(true);
  }, []);

  function stripHtml(html: string): string {
    if (typeof window === "undefined") return html;
    const doc = new DOMParser().parseFromString(html, "text/html");
    return doc.body.textContent || "";
  }

  const plainTextLength = stripHtml(value).trim().length;

  return (
    <div className="flex flex-col gap-2">
      <span className="text-sm font-medium text-ink">{label}</span>
      {mounted ? (
        <div className="bg-surface rounded-[var(--radius-md)] border border-border overflow-hidden">
          <ReactQuill
            theme="snow"
            value={value}
            onChange={onChange}
            readOnly={disabled}
            modules={modules}
            formats={formats}
            placeholder="Ceritakan kegiatan PKL-mu hari ini secara rinci..."
          />
        </div>
      ) : (
        <div className="h-36 rounded-[var(--radius-md)] border border-border bg-surface-muted animate-pulse" />
      )}
      <p className="text-xs text-ink-muted" aria-live="polite">
        {plainTextLength}/{maxLength} karakter · Menggunakan Quill.js Rich Text Editor
      </p>
    </div>
  );
}
