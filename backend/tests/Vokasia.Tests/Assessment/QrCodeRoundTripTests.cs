using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Vokasia.Worker.Export;
using ZXing;
using ZXing.ImageSharp;

namespace Vokasia.Tests.Assessment;

/// <summary>
/// Independent QR payload round-trip tests. The PNG is the same representation passed to
/// QuestPDF immediately before embedding in CertificatePdfDocument; this is not a physical PDF scan.
/// </summary>
public sealed class QrCodeRoundTripTests
{
    private static string Decode(string url)
    {
        using var image = Image.Load<Rgba32>(QrCodeSvgGenerator.GeneratePng(url));
        var reader = new ZXing.ImageSharp.BarcodeReader<Rgba32> { Options = new ZXing.Common.DecodingOptions { TryHarder = true, PossibleFormats = [BarcodeFormat.QR_CODE] } };
        return reader.Decode(image)?.Text ?? throw new InvalidOperationException("Independent QR decoder returned no result.");
    }

    [Theory]
    [InlineData("https://example.test/verify/ABC123")]
    [InlineData("https://vokasia.example/verify/VKS-2026-000001")]
    public void VerifyUrl_QrEncoder_IndependentDecoder_ReturnsExactUrl(string url) => Assert.Equal(url, Decode(url));

    [Fact]
    public void LongPublicVerifyUrl_RoundTripsExactly()
    {
        const string url = "https://vokasia.example/verify/VKS-2026-000001-CLASS-XII-RPL-2026";
        Assert.Equal(url, Decode(url));
    }

    [Fact]
    public void DifferentCertificateCodes_ProduceDifferentDecodedContent()
    {
        const string baseUrl = "https://vokasia.example/verify/";
        Assert.NotEqual(Decode(baseUrl + "VKS-2026-000001"), Decode(baseUrl + "VKS-2026-000002"));
    }
}
