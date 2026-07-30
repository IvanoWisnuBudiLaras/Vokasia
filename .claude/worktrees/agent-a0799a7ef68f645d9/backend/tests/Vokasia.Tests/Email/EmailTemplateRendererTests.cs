using Vokasia.Infrastructure.Email;

namespace Vokasia.Tests.Email;

/// <summary>
/// VOK-H4-E3 §2 AC: "Given template email, When render 5 jenis, Then konsisten (header/footer sama),
/// plain-text fallback ada." Fungsi murni (EmailTemplateRenderer TIDAK ada I/O/DI) - test langsung
/// panggil kelima method statis dgn data contoh, TANPA mock apa pun.
///
/// [CAKUPAN]: 2 dari 5 template (ExportReady, InvoiceIssued) BELUM dipanggil consumer produksi apa
/// pun (fitur export=H5, invoice/billing=H6 belum ada) - lihat doc-comment EmailTemplateRenderer -
/// test ini SENGAJA tetap merender kelimanya (bukan cuma 3 yg sudah punya consumer) supaya AC "render
/// 5 jenis, konsisten" benar2 dibuktikan utk SEMUA template yang ada saat ini, bukan cuma yg kebetulan
/// sudah terpakai.
/// </summary>
public class EmailTemplateRendererTests
{
    private const string BrandMarker = ">Vokasia<";
    private const string FooterMarker = "Platform Manajemen PKL SMK";

    public static IEnumerable<object[]> AllTemplates()
    {
        yield return new object[] { "MentorInvite", (Func<(string Subject, string Html, string Text)>)(() =>
            EmailTemplateRenderer.MentorInvite("Budi Santoso", "PT Contoh Sejahtera", DateTimeOffset.UtcNow.AddDays(7))) };
        yield return new object[] { "JournalReminder", (Func<(string Subject, string Html, string Text)>)(() =>
            EmailTemplateRenderer.JournalReminder("Siti Aminah", DateOnly.FromDateTime(DateTime.UtcNow))) };
        yield return new object[] { "GhostingAlert", (Func<(string Subject, string Html, string Text)>)(() =>
            EmailTemplateRenderer.GhostingAlert("Rudi Hartono", "CV Maju Jaya", 5, "https://vokasia.local/app")) };
        yield return new object[] { "ExportReady", (Func<(string Subject, string Html, string Text)>)(() =>
            EmailTemplateRenderer.ExportReady("Guru Pembimbing", "https://vokasia.local/export/123", DateTimeOffset.UtcNow.AddDays(1))) };
        yield return new object[] { "InvoiceIssued", (Func<(string Subject, string Html, string Text)>)(() =>
            EmailTemplateRenderer.InvoiceIssued("SMK Negeri 1 Contoh", "Juli 2026", 1_500_000m, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)))) };
    }

    [Theory]
    [MemberData(nameof(AllTemplates))]
    public void Render_AllFiveTemplates_ShareSameHeaderAndFooter(string templateName, Func<(string Subject, string Html, string Text)> render)
    {
        var (subject, html, text) = render();

        Assert.False(string.IsNullOrWhiteSpace(subject));
        Assert.Contains(BrandMarker, html); // header brand span - SAMA di kelima template (lihat Layout() privat bersama).
        Assert.Contains(FooterMarker, html); // footer - SAMA di kelima template.
        Assert.Contains("<!DOCTYPE html>", html);
    }

    [Theory]
    [MemberData(nameof(AllTemplates))]
    public void Render_AllFiveTemplates_HavePlainTextFallback(string templateName, Func<(string Subject, string Html, string Text)> render)
    {
        var (_, _, text) = render();

        // AC eksplisit: "plain-text fallback ada" - bukan cuma non-kosong, tapi jg BUKAN html mentah
        // (tak ada tag) dan tetap bawa footer yg sama (konsisten dgn versi HTML).
        Assert.False(string.IsNullOrWhiteSpace(text));
        Assert.DoesNotContain("<html", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<body", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(FooterMarker, text);
    }

    [Fact]
    public void Render_UserSuppliedNameWithHtmlSpecialChars_IsEncodedNotInjected()
    {
        // Nama siswa literal berisi karakter HTML spesial - HARUS ter-encode di output HTML (lihat
        // doc-comment kelas: E() diterapkan ke semua nilai dari data pengguna), BUKAN krn ada ancaman
        // nyata diketahui, murni kebiasaan aman standar.
        var (_, html, _) = EmailTemplateRenderer.JournalReminder("<script>alert(1)</script>", DateOnly.FromDateTime(DateTime.UtcNow));

        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }
}
