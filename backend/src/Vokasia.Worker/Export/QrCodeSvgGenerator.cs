using QRCoder;

namespace Vokasia.Worker.Export;

/// <summary>Standards-compliant QR encoder used by certificate PDFs.</summary>
public static class QrCodeSvgGenerator
{
    public static string GenerateSvg(string content, int sizePx = 120)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        using var generator = new QRCodeGenerator();
        using var qrData = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        var svg = new SvgQRCode(qrData).GetGraphic(4, "#1c1f26", "#ffffff", drawQuietZones: true);
        return svg.Replace("<svg ", $"<svg width=\"{sizePx}\" height=\"{sizePx}\" ", StringComparison.Ordinal);
    }

    /// <summary>PNG representation used by the certificate PDF and independently decodable in tests.</summary>
    public static byte[] GeneratePng(string content, int pixelsPerModule = 8)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        using var generator = new QRCodeGenerator();
        using var qrData = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        return new PngByteQRCode(qrData).GetGraphic(pixelsPerModule, drawQuietZones: true);
    }
}
