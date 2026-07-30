using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace Vokasia.Worker.Imaging;

/// <summary>
/// VOK-H4-E1 §2 — logika transformasi gambar MURNI (tanpa MinIO/DbContext), diekstrak dari
/// <see cref="Consumers.PhotoUploadedConsumer"/> KHUSUS supaya bisa diuji langsung tanpa perlu
/// fake <c>IMinioClient</c> (interface itu ~70 anggota gabungan IBucketOperations+IObjectOperations,
/// dibuktikan lewat probe compiler CS0535 sesi ini - mem-fake-nya utuh tak sepadan hanya utk
/// menguji strip-EXIF/resize/target-ukuran). Consumer TETAP satu-satunya pintu MinIO get/put +
/// penanganan exception permanen-vs-transient (lihat doc-comment di sana) - kelas ini HANYA
/// decode->proses->encode byte-ke-byte.
/// </summary>
public static class PhotoProcessor
{
    public const int MaxBytes = 200 * 1024;
    public const int ThumbnailWidth = 320;

    public sealed record Result(byte[] Processed, byte[] Thumbnail);

    /// <summary>
    /// Decode -> strip SELURUH profil metadata (Exif/Iptc/Xmp) kecuali <paramref name="geotagAllowed"/>
    /// -> encode JPEG target &lt;=200KB -> thumbnail lebar 320px. Melempar
    /// <see cref="UnknownImageFormatException"/>/<see cref="InvalidImageContentException"/> apa
    /// adanya kalau <paramref name="originalBytes"/> bukan gambar valid - SENGAJA tidak ditangkap
    /// di sini (caller/PhotoUploadedConsumer yang memutuskan itu kegagalan PERMANEN, bukan retry).
    /// </summary>
    public static Result Process(byte[] originalBytes, bool geotagAllowed)
    {
        using var original = new MemoryStream(originalBytes);
        using var image = Image.Load(original);

        if (!geotagAllowed)
        {
            image.Metadata.ExifProfile = null;
            image.Metadata.IptcProfile = null;
            image.Metadata.XmpProfile = null;
        }

        var processedBytes = EncodeWithSizeTarget(image);

        using var thumb = image.Clone(ctx => ctx.Resize(ThumbnailWidth, 0));
        using var thumbStream = new MemoryStream();
        thumb.SaveAsJpeg(thumbStream, new JpegEncoder { Quality = 80 });

        return new Result(processedBytes, thumbStream.ToArray());
    }

    /// <summary>Loop kompresi target &lt;=200KB (AC): mulai quality 85, turun 10 tiap gagal, lantai
    /// 35 (drpd loop tanpa henti/kualitas hancur total). Kalau bahkan di lantai masih &gt;200KB
    /// (sumber sangat besar/beresolusi ekstrem), diterima apa adanya (best-effort, bukan janji keras) -
    /// hasil percobaan TERAKHIR (kualitas terendah yg dicoba) yang dipakai.</summary>
    private static byte[] EncodeWithSizeTarget(Image image)
    {
        var lastAttempt = Array.Empty<byte>();
        for (var quality = 85; quality >= 35; quality -= 10)
        {
            using var stream = new MemoryStream();
            image.SaveAsJpeg(stream, new JpegEncoder { Quality = quality });
            lastAttempt = stream.ToArray();
            if (lastAttempt.Length <= MaxBytes)
            {
                return lastAttempt;
            }
        }

        return lastAttempt;
    }
}
