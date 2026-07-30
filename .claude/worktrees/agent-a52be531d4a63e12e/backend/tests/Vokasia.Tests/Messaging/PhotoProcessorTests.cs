using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using Vokasia.Worker.Imaging;

namespace Vokasia.Tests.Messaging;

/// <summary>
/// AC VOK-H4-E1 §2 (PhotoUploadedConsumer): strip EXIF kecuali Tenant.GeotagAllowed, kompresi
/// target &lt;=200KB, thumbnail 320px, gagal-decode -> exception spesifik (permanen). Diuji
/// LANGSUNG lewat PhotoProcessor murni (bukan lewat consumer+MinIO nyata) - lihat doc-comment
/// PhotoProcessor.cs utk alasan (IMinioClient ~70 anggota gabungan, dibuktikan lewat probe
/// compiler CS0535 sesi ini - tak sepadan di-fake utuh hanya utk menguji transformasi byte).
/// </summary>
public class PhotoProcessorTests
{
    private static byte[] BuildJpegWithExif(int width, int height, bool withExifGps)
    {
        using var image = new Image<Rgba32>(width, height);

        if (withExifGps)
        {
            var exif = new ExifProfile();
            exif.SetValue(ExifTag.Make, "VokasiaTestCam");
            image.Metadata.ExifProfile = exif;
        }

        using var stream = new MemoryStream();
        image.SaveAsJpeg(stream, new JpegEncoder { Quality = 95 });
        return stream.ToArray();
    }

    /// <summary>Noise per-piksel acak - JPEG mengompresi buruk (mirip foto sungguhan beresolusi
    /// tinggi dgn detail padat), dipakai utk memaksa loop kompresi benar2 menurunkan quality
    /// (bukan hanya lolos di percobaan pertama quality=85).</summary>
    private static byte[] BuildHighEntropyJpeg(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        var rng = new Random(42);
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    row[x] = new Rgba32((byte)rng.Next(256), (byte)rng.Next(256), (byte)rng.Next(256));
                }
            }
        });

        using var stream = new MemoryStream();
        image.SaveAsJpeg(stream, new JpegEncoder { Quality = 95 });
        return stream.ToArray();
    }

    [Fact]
    public void Process_GeotagNotAllowed_StripsExifProfileEntirely()
    {
        var original = BuildJpegWithExif(200, 150, withExifGps: true);

        var result = PhotoProcessor.Process(original, geotagAllowed: false);

        using var decoded = Image.Load(result.Processed);
        Assert.Null(decoded.Metadata.ExifProfile);
    }

    [Fact]
    public void Process_GeotagAllowed_PreservesExifProfile()
    {
        var original = BuildJpegWithExif(200, 150, withExifGps: true);

        var result = PhotoProcessor.Process(original, geotagAllowed: true);

        using var decoded = Image.Load(result.Processed);
        Assert.NotNull(decoded.Metadata.ExifProfile);
        Assert.True(decoded.Metadata.ExifProfile!.TryGetValue(ExifTag.Make, out var makeValue));
        Assert.Equal("VokasiaTestCam", makeValue!.Value);
    }

    [Fact]
    public void Process_ProducesThumbnailWithConfiguredWidth()
    {
        var original = BuildJpegWithExif(640, 480, withExifGps: false);

        var result = PhotoProcessor.Process(original, geotagAllowed: false);

        using var thumb = Image.Load(result.Thumbnail);
        Assert.Equal(PhotoProcessor.ThumbnailWidth, thumb.Width);
        // Resize(width, 0) -> tinggi menyesuaikan rasio asli (640x480, rasio 4:3) -> 320x240.
        Assert.Equal(240, thumb.Height);
    }

    [Fact]
    public void Process_MainImagePreservesOriginalDimensions()
    {
        var original = BuildJpegWithExif(300, 200, withExifGps: false);

        var result = PhotoProcessor.Process(original, geotagAllowed: false);

        using var decoded = Image.Load(result.Processed);
        Assert.Equal(300, decoded.Width);
        Assert.Equal(200, decoded.Height);
    }

    [Fact]
    public void Process_HighEntropyLargeImage_OutputSmallerThanTopQualityBaseline()
    {
        // Baseline: gambar SAMA di-encode langsung quality=85 (percobaan pertama loop) - kalau
        // baseline ini SUDAH <=200KB, loop akan berhenti di percobaan pertama (tak ada yg utk
        // dibuktikan soal step-down quality) - test ini hanya bermakna kalau baseline > 200KB.
        using var probe = Image.Load(BuildHighEntropyJpeg(1600, 1200));
        using var baselineStream = new MemoryStream();
        probe.SaveAsJpeg(baselineStream, new JpegEncoder { Quality = 85 });
        var baselineSize = baselineStream.Length;

        var original = BuildHighEntropyJpeg(1600, 1200);
        var result = PhotoProcessor.Process(original, geotagAllowed: true);

        Assert.True(baselineSize > PhotoProcessor.MaxBytes,
            $"Prasyarat test tak terpenuhi: baseline quality=85 ({baselineSize} bytes) sudah <=200KB - " +
            "gambar noise perlu diperbesar dulu supaya benar2 menguji loop step-down quality.");
        Assert.True(result.Processed.Length < baselineSize,
            $"Hasil ({result.Processed.Length} bytes) seharusnya lebih kecil drpd baseline quality=85 ({baselineSize} bytes) - loop step-down quality tak nampak bekerja.");
    }

    [Fact]
    public void Process_CorruptBytes_ThrowsUnknownImageFormatException()
    {
        var garbage = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0xFF, 0xFE, 0x10, 0x20 };

        Assert.ThrowsAny<Exception>(() => PhotoProcessor.Process(garbage, geotagAllowed: false));
        // AC PhotoUploadedConsumer catch spesifik UnknownImageFormatException/InvalidImageContentException -
        // dibuktikan tipe persisnya (bukan Exception generik) supaya kontrak dgn consumer benar2 cocok.
        var ex = Record.Exception(() => PhotoProcessor.Process(garbage, geotagAllowed: false));
        Assert.True(ex is UnknownImageFormatException or InvalidImageContentException,
            $"Tipe exception tak sesuai kontrak catch PhotoUploadedConsumer: {ex?.GetType().FullName}");
    }
}
