namespace Vokasia.Api.Validation;

public static class RichTextDocument
{
    public const int MaxPlainTextLength = 5000;
    public const int MaxSerializedLength = 50_000;

    public static bool TryNormalize(string? input, out string normalized, out string error)
    {
        normalized = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "Teks jurnal wajib diisi.";
            return false;
        }

        var plain = TextSanitizer.ToPlainText(input);
        if (string.IsNullOrWhiteSpace(plain))
        {
            error = "Teks jurnal wajib diisi dan tidak boleh hanya berisi tag kosong.";
            return false;
        }

        if (plain.Length > MaxPlainTextLength)
        {
            error = $"Teks jurnal maksimal {MaxPlainTextLength} karakter.";
            return false;
        }

        normalized = TextSanitizer.Clean(input);
        return true;
    }

    public static string ToPlainText(string? input) => TextSanitizer.ToPlainText(input);
}
