using System.Security.Cryptography;

namespace Vokasia.Domain.Common;

/// <summary>
/// VOK-H5-E1 §5 — <c>CertCode</c>: 12 karakter url-safe ACAK (BUKAN sequential/incremental -
/// AC literal ticket) dipakai publik di <c>/verify/{certCode}</c>. Alfabet sengaja HANYA
/// alfanumerik (tanpa <c>-</c>/<c>_</c> ala Base64Url) supaya nyaman diketik ulang manual dari
/// PDF cetak (siswa/HRD DUDI mengetik ulang kode dari kertas, bukan selalu scan QR) - 62^12
/// kemungkinan, jauh lebih dari cukup utk keperluan non-cryptographic-secret (kode ini memang
/// dimaksud BISA dibaca orang, bukan rahasia - keamanan sesungguhnya ada di ke-random-annya
/// utk mencegah tebak-tebakan sequential, bukan di kerahasiaan).
/// </summary>
public static class CertCodeGenerator
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    private const int Length = 12;

    public static string Generate()
    {
        Span<char> buffer = stackalloc char[Length];
        Span<byte> randomBytes = stackalloc byte[Length];
        RandomNumberGenerator.Fill(randomBytes);

        // Modulo 62 dari byte 0..255 punya bias sangat kecil (256%62=8, 8 nilai byte awal alfabet
        // sedikit lebih sering) - diterima SADAR utk kode tampilan/non-secret spt ini (bukan token
        // kriptografis), bukan diam-diam diabaikan. Rejection-sampling sempurna tak sepadan
        // kompleksitasnya utk keperluan ini.
        for (var i = 0; i < Length; i++)
        {
            buffer[i] = Alphabet[randomBytes[i] % Alphabet.Length];
        }

        return new string(buffer);
    }
}
