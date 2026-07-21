using Vokasia.Api.Validation;

namespace Vokasia.Tests.Guard;

/// <summary>AC VOK-H3-E3 §4 SanitizerTests: script/event-handler dibersihkan, teks biasa tak berubah.</summary>
public class SanitizerTests
{
    [Fact]
    public void Clean_ScriptTag_RemovesEntireBlockIncludingContent()
    {
        var result = TextSanitizer.Clean("Sebelum <script>alert(1)</script> sesudah");

        Assert.DoesNotContain("<script", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alert(1)", result);
        Assert.Contains("Sebelum", result);
        Assert.Contains("sesudah", result);
    }

    [Fact]
    public void Clean_InlineEventHandlerAttribute_RemovesEntireTag()
    {
        // AC: "event handler inline -> bersih". Tag <img> DAN atribut onerror-nya hilang sekaligus
        // (regex tag menghapus dari '<' sampai '>' pertama berikutnya, bukan cuma nama tag).
        var result = TextSanitizer.Clean("Lihat <img src=x onerror=\"alert(1)\"> ya");

        Assert.DoesNotContain("onerror", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Lihat", result);
        Assert.Contains("ya", result);
    }

    [Fact]
    public void Clean_StyleTag_RemovesEntireBlock()
    {
        var result = TextSanitizer.Clean("A <style>body{display:none}</style> B");

        Assert.DoesNotContain("<style", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("display:none", result);
    }

    [Fact]
    public void Clean_NonScriptTag_StripsTagButKeepsInnerText()
    {
        var result = TextSanitizer.Clean("<b>penting</b> sekali");

        Assert.DoesNotContain("<b>", result);
        Assert.DoesNotContain("</b>", result);
        Assert.Contains("penting", result);
        Assert.Contains("sekali", result);
    }

    [Fact]
    public void Clean_PlainTextWithoutTags_IsUnchangedApartFromTrim()
    {
        const string input = "Hari ini belajar setup CI/CD, semua lancar.";
        Assert.Equal(input, TextSanitizer.Clean(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Clean_NullOrEmpty_ReturnsEmptyString(string? input)
    {
        Assert.Equal(string.Empty, TextSanitizer.Clean(input));
    }
}
