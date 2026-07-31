import type { InvoiceDto } from "./apiTypes";

export function downloadSignedInvoicePdf(invoice: InvoiceDto, schoolName: string = "SMK Negeri 1 Jakarta") {
  const receiptNo = `RESI-VOK-${new Date(invoice.periodMonth).getFullYear()}${String(new Date(invoice.periodMonth).getMonth() + 1).padStart(2, "0")}-${invoice.id.slice(0, 5).toUpperCase()}`;
  const periodLabel = new Date(invoice.periodMonth).toLocaleDateString("id-ID", { month: "long", year: "numeric" });
  const paidDate = new Date().toLocaleDateString("id-ID", { dateStyle: "full" });

  const printWin = window.open("", "_blank");
  if (!printWin) return;

  const html = `
    <!DOCTYPE html>
    <html lang="id">
      <head>
        <meta charset="UTF-8">
        <title>Nota Pembayaran Resmi — ${receiptNo}</title>
        <style>
          @page { size: A4; margin: 20mm; }
          body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; color: #0f172a; margin: 0; padding: 24px; }
          .header { display: flex; justify-content: space-between; align-items: flex-start; border-b: 2px solid #1e5eb4; padding-bottom: 16px; margin-bottom: 24px; }
          .brand { font-size: 24px; font-weight: 800; color: #1e5eb4; letter-spacing: -0.5px; }
          .subbrand { font-size: 12px; color: #64748b; margin-top: 2px; }
          .receipt-badge { text-align: right; }
          .receipt-title { font-size: 18px; font-weight: 700; color: #16a34a; text-transform: uppercase; letter-spacing: 1px; }
          .receipt-no { font-family: monospace; font-size: 13px; font-weight: 600; color: #334155; margin-top: 4px; }
          .meta-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 24px; margin-bottom: 24px; font-size: 13px; }
          .meta-box { background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 12px 16px; }
          .meta-label { font-size: 11px; font-weight: 600; color: #64748b; text-transform: uppercase; margin-bottom: 4px; }
          .meta-val { font-size: 14px; font-weight: 600; color: #0f172a; }
          .table { width: 100%; border-collapse: collapse; margin-bottom: 32px; font-size: 13px; }
          .table th { background: #f1f5f9; border-bottom: 2px solid #cbd5e1; padding: 10px 12px; text-align: left; font-weight: 600; }
          .table td { border-bottom: 1px solid #e2e8f0; padding: 12px; }
          .total-row { background: #f8fafc; font-weight: 700; font-size: 15px; }
          .signature-section { display: flex; justify-content: space-between; align-items: flex-end; margin-top: 48px; border-top: 1px dashed #cbd5e1; padding-top: 24px; }
          .qr-box { font-size: 11px; color: #64748b; text-align: center; }
          .qr-code { width: 80px; height: 80px; border: 1px solid #cbd5e1; padding: 4px; border-radius: 4px; }
          .stamp-box { text-align: center; position: relative; }
          .stamp { width: 120px; opacity: 0.85; margin-bottom: -20px; }
          .sign-title { font-size: 12px; color: #64748b; margin-bottom: 40px; }
          .sign-name { font-size: 13px; font-weight: 700; border-bottom: 1px solid #0f172a; padding-bottom: 2px; }
          .sign-role { font-size: 11px; color: #64748b; margin-top: 2px; }
          @media print {
            body { padding: 0; }
          }
        </style>
      </head>
      <body>
        <div class="header">
          <div>
            <div class="brand">VOKASIA PLATFORM</div>
            <div class="subbrand">PT Vokasia Teknologi Nusantara — Manajemen PKL SMK Multi-Tenant</div>
            <div class="subbrand">Gedung Cyber 2, Lt. 14, Jakarta Selatan | support@vokasia.id</div>
          </div>
          <div class="receipt-badge">
            <div class="receipt-title">LUNAS / PAID</div>
            <div class="receipt-no">No. Resi: ${receiptNo}</div>
          </div>
        </div>

        <div class="meta-grid">
          <div class="meta-box">
            <div class="meta-label">Diterbitkan Untuk (Sekolah / Tenant)</div>
            <div class="meta-val">${schoolName}</div>
            <div style="font-size: 12px; color: #64748b; margin-top: 4px;">ID Tenant: ${invoice.tenantId}</div>
          </div>

          <div class="meta-box">
            <div class="meta-label">Rincian Pembayaran</div>
            <div style="display: flex; justify-content: space-between;">
              <span style="color: #64748b;">Periode Tagihan:</span>
              <strong>${periodLabel}</strong>
            </div>
            <div style="display: flex; justify-content: space-between; margin-top: 4px;">
              <span style="color: #64748b;">Tanggal Konfirmasi:</span>
              <strong>${paidDate}</strong>
            </div>
            <div style="display: flex; justify-content: space-between; margin-top: 4px;">
              <span style="color: #64748b;">Status Verifikasi:</span>
              <strong style="color: #16a34a;">Valid & Terverifikasi</strong>
            </div>
          </div>
        </div>

        <table class="table">
          <thead>
            <tr>
              <th>No</th>
              <th>Deskripsi Layanan / Lisensi</th>
              <th>Periode</th>
              <th style="text-align: right;">Jumlah (Rp)</th>
            </tr>
          </thead>
          <tbody>
            <tr>
              <td>1</td>
              <td>
                <strong>Berlangganan Sistem Informasi Manajemen PKL Vokasia Enterprise</strong><br>
                <span style="font-size: 11px; color: #64748b;">Akses Full Feature, Jurnal Digital, Geotagging, Approval Mentor, & Portofolio Siswa</span>
              </td>
              <td>${periodLabel}</td>
              <td style="text-align: right;">Rp ${invoice.amount.toLocaleString("id-ID")}</td>
            </tr>
            <tr class="total-row">
              <td colSpan="3" style="text-align: right;">TOTAL PEMBAYARAN:</td>
              <td style="text-align: right; color: #1e5eb4;">Rp ${invoice.amount.toLocaleString("id-ID")}</td>
            </tr>
          </tbody>
        </table>

        <div class="signature-section">
          <div class="qr-box">
            <svg class="qr-code" viewBox="0 0 24 24" fill="none" stroke="#1e5eb4" stroke-width="1.5">
              <rect x="3" y="3" width="7" height="7" rx="1"/>
              <rect x="14" y="3" width="7" height="7" rx="1"/>
              <rect x="3" y="14" width="7" height="7" rx="1"/>
              <path d="M14 14h3v3h-3zM17 17h3v3h-3zM14 17h3v3h-3z"/>
            </svg>
            <div style="margin-top: 4px;">Pindai QR untuk Verifikasi<br>Keabsahan Nota Vokasia</div>
          </div>

          <div className="stamp-box">
            <div class="sign-title">Jakarta, ${paidDate}</div>
            <div style="height: 40px; margin-bottom: 8px;">
              <span style="display: inline-block; padding: 4px 12px; border: 2px solid #16a34a; color: #16a34a; font-weight: 800; font-size: 11px; border-radius: 4px; transform: rotate(-4deg);">
                OFFICIAL STAMP PASSED
              </span>
            </div>
            <div class="sign-name">Wisnu Budi Laras, M.Kom</div>
            <div class="sign-role">Head of Finance & Operations Vokasia</div>
          </div>
        </div>

        <script>
          window.onload = function() { window.print(); };
        </script>
      </body>
    </html>
  `;

  printWin.document.write(html);
  printWin.document.close();
}
