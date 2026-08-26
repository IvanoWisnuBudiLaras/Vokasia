using Xunit;
using Vokasia.Api.Validation;

namespace Vokasia.Tests.Auth;

public class TextSanitizerUnitTests
{
    [Fact]
    public void Clean_StripsScriptTags_Completely()
    {
        var html = "<p>Hello <script>alert('xss')</script> world</p>";
        var result = TextSanitizer.Clean(html);
        Assert.DoesNotContain("<script>", result);
        Assert.DoesNotContain("alert", result);
        Assert.Contains("<p>Hello  world</p>", result);
    }

    [Fact]
    public void Clean_StripsOnclickAttributes()
    {
        var html = "<p onclick=\"alert('hack')\">Safe text</p>";
        var result = TextSanitizer.Clean(html);
        Assert.DoesNotContain("onclick", result);
        Assert.DoesNotContain("alert", result);
        Assert.Contains("<p>Safe text</p>", result);
    }

    [Fact]
    public void Clean_AllowsAllowedFormattingTags()
    {
        var html = "<h1>Title</h1><p><strong>Bold</strong> and <em>italic</em> and <u>underline</u>.</p>";
        var result = TextSanitizer.Clean(html);
        Assert.Contains("<h1>", result);
        Assert.Contains("<strong>", result);
        Assert.Contains("<em>", result);
        Assert.Contains("<u>", result);
        Assert.Equal("<h1>Title</h1><p><strong>Bold</strong> and <em>italic</em> and <u>underline</u>.</p>", result);
    }

    [Fact]
    public void Clean_StripsIframeAndObjectTags()
    {
        var html = "<p>Text</p><iframe src=\"http://evil.com\"></iframe><object data=\"http://evil.com\"></object>";
        var result = TextSanitizer.Clean(html);
        Assert.DoesNotContain("<iframe", result);
        Assert.DoesNotContain("<object", result);
        Assert.Equal("<p>Text</p>", result);
    }

    [Fact]
    public void Clean_StripsStyleBlocks()
    {
        var html = "<p>Content</p><style>body{color:red}</style>";
        var result = TextSanitizer.Clean(html);
        Assert.DoesNotContain("<style>", result);
        Assert.DoesNotContain("color:red", result);
        Assert.Equal("<p>Content</p>", result);
    }

    [Fact]
    public void Clean_StripsJavascriptHref()
    {
        var html = "<a href=\"javascript:alert('xss')\">Click me</a>";
        var result = TextSanitizer.Clean(html);
        Assert.DoesNotContain("javascript:", result);
        Assert.DoesNotContain("alert", result);
    }

    [Fact]
    public void ToPlainText_ReturnsPlainTextFromHtml()
    {
        var html = "<h1>Judul</h1><p>Ini <strong>paragraf</strong> biasa.</p>";
        var result = RichTextDocument.ToPlainText(html);
        Assert.Equal("Judul Ini paragraf biasa.", result);
    }

    [Fact]
    public void ToPlainText_HandlesNullInput()
    {
        var result = RichTextDocument.ToPlainText(null);
        Assert.Equal("", result);
    }

    [Fact]
    public void Clean_StripsDisallowedTags_LikeDivAndSpan()
    {
        var html = "<div>wrapper</div><span>inline</span><p>only paragraph allowed</p>";
        var result = TextSanitizer.Clean(html);
        Assert.DoesNotContain("<div>", result);
        Assert.DoesNotContain("<span>", result);
        Assert.Contains("<p>", result);
    }

    [Fact]
    public void Clean_EmptyInput_ReturnsEmpty()
    {
        Assert.Equal("", TextSanitizer.Clean(null));
        Assert.Equal("", TextSanitizer.Clean(""));
        Assert.Equal("", TextSanitizer.Clean("   "));
    }

    [Fact]
    public void TryNormalize_ValidHtml_ReturnsNormalized()
    {
        var html = "<p>Hari ini saya belajar coding.</p>";
        var ok = RichTextDocument.TryNormalize(html, out var normalized, out var error);
        Assert.True(ok);
        Assert.NotEmpty(normalized);
        Assert.Empty(error);
    }

    [Fact]
    public void TryNormalize_EmptyInput_ReturnsError()
    {
        var ok = RichTextDocument.TryNormalize("", out _, out var error);
        Assert.False(ok);
        Assert.NotEmpty(error);
    }

    [Fact]
    public void TryNormalize_ScriptInjection_StrippedAndAccepted()
    {
        var html = "<p>Normal <script>alert('hack')</script> text</p>";
        var ok = RichTextDocument.TryNormalize(html, out var normalized, out var error);
        Assert.True(ok);
        Assert.DoesNotContain("<script>", normalized);
        Assert.DoesNotContain("alert", normalized);
    }
}