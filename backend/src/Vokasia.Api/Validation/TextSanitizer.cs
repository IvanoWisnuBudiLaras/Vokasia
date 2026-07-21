using System.Text.RegularExpressions;

namespace Vokasia.Api.Validation;

/// <summary>
/// VOK-H3-E3 §2: strip HTML/script dari field teks bebas SEBELUM simpan (jurnal, komentar guru,
/// alasan reject) — pertahanan lapis server, TIDAK menggantikan kewajiban FE mengesc teks saat
/// render (React sudah escape by default selama tidak dipakai dangerouslySetInnerHTML).
///
/// Desain: blok &lt;script&gt;/&lt;style&gt; dibuang UTUH (tag+isi — isi script tak ada gunanya di
/// teks jurnal, beda dgn tag lain yang kontennya tetap teks sah user). Tag lain di-strip tapi teks
/// di antaranya DISIMPAN (mis. "&lt;b&gt;penting&lt;/b&gt;" -> "penting", bukan hilang total).
/// SENGAJA TIDAK men-decode HTML entity (&amp;lt; dst) balik ke karakter mentah — supaya payload
/// yang di-encode ganda tidak diam-diam kembali jadi "&lt;script&gt;" mentah pasca sanitasi.
/// </summary>
public static class TextSanitizer
{
    private static readonly Regex ScriptOrStyleBlock = new(
        @"<(script|style)\b[^>]*>[\s\S]*?</\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AnyTag = new(@"<[^>]*>", RegexOptions.Compiled);

    private static readonly Regex ExtraWhitespace = new(@"[ \t]{2,}", RegexOptions.Compiled);

    public static string Clean(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input ?? string.Empty;
        }

        var text = ScriptOrStyleBlock.Replace(input, string.Empty);
        text = AnyTag.Replace(text, string.Empty);
        text = ExtraWhitespace.Replace(text, " ");
        return text.Trim();
    }
}
