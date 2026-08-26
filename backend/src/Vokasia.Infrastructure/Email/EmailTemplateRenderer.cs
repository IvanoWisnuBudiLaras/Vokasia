using System.Net;
using Vokasia.Domain.Common;

namespace Vokasia.Infrastructure.Email;

/// <summary>
/// VOK-H4-E3 §2 — 5 template + base layout SERAGAM (AC: "render 5 jenis, konsisten header/footer,
/// plain-text fallback ada"). Fungsi statis MURNI (input model -> output string) - TIDAK ada I/O,
/// TIDAK butuh DI, gampang di-snapshot-test tanpa mock apa pun.
///
/// [CAKUPAN, dicatat eksplisit]: 3 dari 5 template (MentorInvite, JournalReminder, GhostingAlert)
/// SUDAH dipakai consumer nyata sesi ini (lihat Vokasia.Worker/Consumers). 2 sisanya (ExportReady,
/// InvoiceIssued) ticket H4-E3 SENDIRI minta ada+teruji SEKARANG (render konsisten, DoD "5 template
/// terkirim teruji") walau FITUR pemanggilnya (export H5, invoice H6) belum ada sama sekali - jadi
/// keduanya HANYA dirender+ditulis ke `.emails/` oleh test (belum dipanggil consumer produksi apa
/// pun). Ini SENGAJA (persis literal DoD ticket), bukan over-scope ke H5/H6 - wiring nyatanya nanti
/// milik ticket masing-masing.
///
/// HtmlEncode diterapkan ke SEMUA nilai yang datang dari data pengguna (nama siswa/sekolah dst.) -
/// mencegah HTML injection kalau nama punya karakter spesial (mis. nama siswa literal "<script>"),
/// bukan krn ada ancaman nyata diketahui, MURNI kebiasaan aman standar render HTML dari data variabel.
/// </summary>
public static class EmailTemplateRenderer
{
    private const string FooterText = "Vokasia — Platform Manajemen PKL SMK. Email ini dikirim otomatis, mohon tidak membalas.";

    public static (string Subject, string Html, string Text) MentorInvite(string studentName, string companyName, DateTimeOffset expiresAt)
    {
        var subject = $"Undangan mentor PKL — {studentName}";
        var bodyHtml = $"""
            <p>Anda diundang menjadi mentor pendamping PKL untuk <strong>{E(studentName)}</strong> di <strong>{E(companyName)}</strong>.</p>
            <p>Silakan hubungi staf sekolah untuk tautan aktivasi akun mentor. Undangan berlaku sampai <strong>{expiresAt:d MMMM yyyy}</strong>.</p>
            """;
        var bodyText = $"Anda diundang menjadi mentor pendamping PKL untuk {studentName} di {companyName}. Hubungi staf sekolah untuk tautan aktivasi. Berlaku sampai {expiresAt:d MMMM yyyy}.";
        return (subject, Layout(subject, bodyHtml), LayoutText(subject, bodyText));
    }

    public static (string Subject, string Html, string Text) JournalReminder(string studentName, DateOnly date)
    {
        var subject = "Pengingat: jurnal PKL hari ini belum diisi";
        var bodyHtml = $"""
            <p>Halo {E(studentName)}, jurnal PKL untuk tanggal <strong>{date:d MMMM yyyy}</strong> belum diisi.</p>
            <p>Yuk isi jurnal hari ini sebelum lupa — cukup beberapa menit lewat aplikasi Vokasia.</p>
            """;
        var bodyText = $"Halo {studentName}, jurnal PKL untuk tanggal {date:d MMMM yyyy} belum diisi. Yuk isi jurnal hari ini lewat aplikasi Vokasia.";
        return (subject, Layout(subject, bodyHtml), LayoutText(subject, bodyText));
    }

    public static (string Subject, string Html, string Text) LearningRecordReminder(
        string studentName,
        LearningAssessmentStage stage,
        LearningAssessmentReminderType reminderType,
        DateOnly dueDate)
    {
        var stageLabel = stage == LearningAssessmentStage.Middle ? "Penilaian Tengah" : "Penilaian Akhir";
        var isOverdue = reminderType == LearningAssessmentReminderType.Overdue;
        var subject = isOverdue
            ? $"Tertunda: Learning Record {stageLabel}"
            : $"Pengingat: Learning Record {stageLabel} perlu diisi";
        var stateLabel = isOverdue ? "sudah tertunda" : "perlu diisi";
        var bodyHtml = $"""
            <p>Halo Mentor, Learning Record <strong>{E(stageLabel)}</strong> untuk <strong>{E(studentName)}</strong> {stateLabel}.</p>
            <p>Tanggal penilaian: <strong>{dueDate:d MMMM yyyy}</strong>. Silakan buka Vokasia untuk melengkapi skor, catatan keseluruhan, dan bukti jurnal bila diperlukan.</p>
            """;
        var bodyText = $"Halo Mentor, Learning Record {stageLabel} untuk {studentName} {stateLabel}. Tanggal penilaian: {dueDate:d MMMM yyyy}. Silakan buka Vokasia untuk melengkapinya.";
        return (subject, Layout(subject, bodyHtml), LayoutText(subject, bodyText));
    }

    public static (string Subject, string Html, string Text) GhostingAlert(string studentName, string companyName, int emptyDays, string dashboardUrl)
    {
        var subject = $"Perhatian: {studentName} belum mengisi jurnal {emptyDays} hari kerja";
        var bodyHtml = $"""
            <p><strong>{E(studentName)}</strong> (PKL di {E(companyName)}) belum mengisi jurnal selama <strong>{emptyDays} hari kerja berturut-turut</strong>.</p>
            <p>Mohon tindak lanjuti — cek detail di <a href="{E(dashboardUrl)}">dashboard Vokasia</a>.</p>
            """;
        var bodyText = $"{studentName} (PKL di {companyName}) belum mengisi jurnal selama {emptyDays} hari kerja berturut-turut. Cek detail: {dashboardUrl}";
        return (subject, Layout(subject, bodyHtml), LayoutText(subject, bodyText));
    }

    /// <summary>[CAKUPAN] Belum dipanggil consumer produksi apa pun (fitur export = H5) — lihat doc-comment kelas.</summary>
    public static (string Subject, string Html, string Text) ExportReady(string requestedBy, string downloadUrl, DateTimeOffset expiresAt)
    {
        var subject = "Export Anda sudah siap diunduh";
        var bodyHtml = $"""
            <p>Halo {E(requestedBy)}, file export yang Anda minta sudah siap.</p>
            <p><a href="{E(downloadUrl)}">Unduh file di sini</a> (tautan berlaku sampai {expiresAt:d MMMM yyyy HH:mm}).</p>
            """;
        var bodyText = $"Halo {requestedBy}, file export Anda sudah siap. Unduh: {downloadUrl} (berlaku sampai {expiresAt:d MMMM yyyy HH:mm}).";
        return (subject, Layout(subject, bodyHtml), LayoutText(subject, bodyText));
    }

    public static (string Subject, string Html, string Text) ExportReadyPrivateReport(string requestedBy, string reportUrl)
    {
        var subject = "Export Learning Record siap diproses";
        var bodyHtml = $"""
            <p>Halo {E(requestedBy)}, file export Learning Record Anda sudah siap.</p>
            <p><a href="{E(reportUrl)}">Buka laporan untuk mengunduhnya</a>. Akses file tetap memerlukan sesi Vokasia Anda.</p>
            """;
        var bodyText = $"Halo {requestedBy}, file export Learning Record Anda sudah siap. Buka laporan: {reportUrl}. Akses file tetap memerlukan sesi Vokasia Anda.";
        return (subject, Layout(subject, bodyHtml), LayoutText(subject, bodyText));
    }

    /// <summary>[CAKUPAN] Belum dipanggil consumer produksi apa pun (fitur billing = H6) — lihat doc-comment kelas.</summary>
    public static (string Subject, string Html, string Text) InvoiceIssued(string schoolName, string month, decimal amount, DateOnly dueDate)
    {
        var subject = $"Invoice Vokasia — {month}";
        var bodyHtml = $"""
            <p>Invoice untuk <strong>{E(schoolName)}</strong> periode <strong>{E(month)}</strong> telah terbit.</p>
            <p>Jumlah tagihan: <strong>Rp {amount:N0}</strong>. Jatuh tempo: <strong>{dueDate:d MMMM yyyy}</strong>.</p>
            """;
        var bodyText = $"Invoice untuk {schoolName} periode {month} telah terbit. Jumlah: Rp {amount:N0}. Jatuh tempo: {dueDate:d MMMM yyyy}.";
        return (subject, Layout(subject, bodyHtml), LayoutText(subject, bodyText));
    }

    /// <summary>
    /// VOK-H6-E1 §1 — CreateTenant wizard: akun TenantAdmin baru + password sementara. [GAP dicatat
    /// eksplisit, bukan diam-diam]: password sementara dikirim APA ADANYA di badan email (bukan
    /// tautan "set password") krn repo ini belum punya endpoint reset-password sungguhan sampai
    /// ticket ini (AccountEndpoints hanya form login) — konsisten dgn pola SchoolUsersEndpoints.
    /// </summary>
    private static string E(string s) => WebUtility.HtmlEncode(s);

    private static string Layout(string title, string bodyHtml) => $"""
        <!DOCTYPE html>
        <html lang="id">
        <head><meta charset="utf-8"><title>{E(title)}</title></head>
        <body style="font-family:sans-serif;background:#f4f4f5;padding:24px;margin:0;">
          <table role="presentation" width="100%" style="max-width:560px;margin:0 auto;background:#ffffff;border-radius:8px;overflow:hidden;">
            <tr><td style="background:#1c1f26;padding:20px 24px;">
              <span style="color:#ffffff;font-size:18px;font-weight:600;">Vokasia</span>
            </td></tr>
            <tr><td style="padding:24px;color:#1c1f26;font-size:14px;line-height:1.6;">
              {bodyHtml}
            </td></tr>
            <tr><td style="padding:16px 24px;background:#f4f4f5;color:#6b7280;font-size:12px;">
              {E(FooterText)}
            </td></tr>
          </table>
        </body>
        </html>
        """;

    private static string LayoutText(string title, string bodyText) => $"""
        {title}
        {new string('-', title.Length)}

        {bodyText}

        --
        {FooterText}
        """;
}
