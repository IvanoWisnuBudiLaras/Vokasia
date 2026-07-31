"use client";

import { useState } from "react";
import { apiClient } from "@/lib/apiClient";
import { Button } from "./Button";
import { Icon } from "./Icon";

export interface ColumnDef<T> {
  key: keyof T | string;
  label: string;
  format?: (value: any, row: T) => string;
}

export interface TableExportToolbarProps<T extends Record<string, any>> {
  data: T[];
  filename?: string;
  columns: ColumnDef<T>[];
  title?: string;
}

export function TableExportToolbar<T extends Record<string, any>>({
  data,
  filename = "data_vokasia",
  columns,
  title = "Laporan Data Vokasia",
}: TableExportToolbarProps<T>) {
  const [copied, setCopied] = useState(false);

  function logAuditExport(exportType: string) {
    try {
      void apiClient.post("/api/audit", {
        action: `DataExported_${exportType}`,
        entity: "TableData",
        entityId: filename,
        metaJson: JSON.stringify({ count: data.length, exportType, timestamp: new Date().toISOString() }),
      });
    } catch (err) {
      // Audit log Best-effort
    }
  }

  function getFormattedValue(row: T, col: ColumnDef<T>): string {
    const val = row[col.key as string];
    if (col.format) {
      return col.format(val, row);
    }
    if (val === null || val === undefined) return "—";
    if (typeof val === "boolean") return val ? "Ya" : "Tidak";
    return String(val);
  }

  function exportToCsv() {
    if (!data || data.length === 0) return;

    logAuditExport("CSV_Excel");

    const headers = columns.map((c) => `"${c.label.replace(/"/g, '""')}"`);
    const csvLines = [headers.join(",")];

    for (const row of data) {
      const line = columns.map((col) => {
        const str = getFormattedValue(row, col);
        return `"${str.replace(/"/g, '""')}"`;
      });
      csvLines.push(line.join(","));
    }

    const bom = "\uFEFF";
    const blob = new Blob([bom + csvLines.join("\r\n")], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `${filename}_${new Date().toISOString().slice(0, 10)}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  }

  function exportToPdfPrint() {
    if (!data || data.length === 0) return;

    logAuditExport("PDF_Print");

    const printWin = window.open("", "_blank");
    if (!printWin) return;

    const tableHeaders = columns.map((c) => `<th style="border: 1px solid #cbd5e1; padding: 8px; background: #f8fafc;">${c.label}</th>`).join("");
    const tableRows = data
      .map(
        (row) =>
          `<tr>${columns
            .map((col) => `<td style="border: 1px solid #cbd5e1; padding: 8px;">${getFormattedValue(row, col)}</td>`)
            .join("")}</tr>`,
      )
      .join("");

    const html = `
      <!DOCTYPE html>
      <html>
        <head>
          <title>${title}</title>
          <style>
            body { font-family: system-ui, -apple-system, sans-serif; padding: 24px; color: #0f172a; }
            h1 { font-size: 20px; margin-bottom: 4px; }
            p { font-size: 12px; color: #64748b; margin-top: 0; margin-bottom: 16px; }
            table { width: 100%; border-collapse: collapse; font-size: 13px; text-align: left; }
            @media print {
              body { padding: 0; }
              @page { margin: 1.5cm; }
            }
          </style>
        </head>
        <body>
          <h1>${title}</h1>
          <p>Dicetak dari Vokasia Platform pada ${new Date().toLocaleDateString("id-ID", { dateStyle: "full" })}</p>
          <table>
            <thead><tr>${tableHeaders}</tr></thead>
            <tbody>${tableRows}</tbody>
          </table>
          <script>
            window.onload = function() { window.print(); };
          </script>
        </body>
      </html>
    `;

    printWin.document.write(html);
    printWin.document.close();
  }

  function copyToClipboard() {
    if (!data || data.length === 0) return;

    logAuditExport("Clipboard_Copy");

    const headers = columns.map((c) => c.label).join("\t");
    const rowsText = data
      .map((row) => columns.map((col) => getFormattedValue(row, col).replace(/\t/g, " ")).join("\t"))
      .join("\n");

    const fullText = `${headers}\n${rowsText}`;
    void navigator.clipboard.writeText(fullText);
    setCopied(true);
    setTimeout(() => setCopied(false), 3000);
  }

  return (
    <div className="flex flex-wrap items-center gap-2">
      <Button
        type="button"
        variant="secondary"
        size="md"
        disabled={data.length === 0}
        onClick={exportToCsv}
        title="Unduh berkas Excel / CSV"
      >
        <Icon name="download" size={16} /> Excel (.xlsx/.csv)
      </Button>

      <Button
        type="button"
        variant="secondary"
        size="md"
        disabled={data.length === 0}
        onClick={exportToPdfPrint}
        title="Cetak atau simpan sebagai dokumen PDF"
      >
        <Icon name="download" size={16} /> Dokumen PDF
      </Button>

      <Button
        type="button"
        variant="secondary"
        size="md"
        disabled={data.length === 0}
        onClick={copyToClipboard}
        title="Salin data ke clipboard untuk disalin ke Excel/Spreadsheet"
      >
        <Icon name={copied ? "check" : "file-text"} size={16} /> {copied ? "Tersalin!" : "Salin Tabel"}
      </Button>
    </div>
  );
}
