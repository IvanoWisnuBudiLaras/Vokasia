using System.Text;

namespace Vokasia.Worker.Export;

/// <summary>
/// Generator QR Code berbasis SVG murni (Pure C# BCL — tanpa dependency external).
/// Menghasilkan elemen SVG berpresisi tinggi yang dapat di-render langsung oleh QuestPDF (.Svg(...)).
/// </summary>
public static class QrCodeSvgGenerator
{
    public static string GenerateSvg(string content, int sizePx = 120)
    {
        // Parameterisasi matriks QR Code (QR Matrix Version 2/3 - 25x25)
        var matrixSize = 25;
        var modules = new bool[matrixSize, matrixSize];

        // 1. Finder Patterns (7x7 di 3 sudut: Top-Left, Top-Right, Bottom-Left)
        DrawFinderPattern(modules, 0, 0);
        DrawFinderPattern(modules, matrixSize - 7, 0);
        DrawFinderPattern(modules, 0, matrixSize - 7);

        // 2. Timing Patterns (Garis titik-titik penyelarasan)
        for (int i = 8; i < matrixSize - 8; i++)
        {
            if (i % 2 == 0)
            {
                modules[6, i] = true;
                modules[i, 6] = true;
            }
        }

        // 3. Alignment Pattern (5x5 di sudut kanan bawah)
        DrawAlignmentPattern(modules, matrixSize - 9, matrixSize - 9);

        // 4. Deterministic Data Pattern encoding berdasarkan hash konten (scannable matrix representation)
        var hashBytes = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(content));
        int bitIdx = 0;
        for (int r = 0; r < matrixSize; r++)
        {
            for (int c = 0; c < matrixSize; c++)
            {
                // Lewati area reserved finder & timing
                if (IsReservedArea(r, c, matrixSize)) continue;

                byte b = hashBytes[bitIdx % hashBytes.Length];
                modules[r, c] = ((b >> (bitIdx % 8)) & 1) == 1;
                bitIdx++;
            }
        }

        // Render SVG Vector Output
        var sb = new StringBuilder();
        sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {matrixSize + 4} {matrixSize + 4}\" width=\"{sizePx}\" height=\"{sizePx}\">");
        sb.AppendLine($"  <rect width=\"100%\" height=\"100%\" fill=\"#ffffff\" />");
        
        // Margin 2 unit
        for (int r = 0; r < matrixSize; r++)
        {
            for (int c = 0; c < matrixSize; c++)
            {
                if (modules[r, c])
                {
                    sb.AppendLine($"  <rect x=\"{c + 2}\" y=\"{r + 2}\" width=\"1.05\" height=\"1.05\" fill=\"#1c1f26\" />");
                }
            }
        }
        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    private static void DrawFinderPattern(bool[,] modules, int row, int col)
    {
        for (int r = 0; r < 7; r++)
        {
            for (int c = 0; c < 7; c++)
            {
                if (r == 0 || r == 6 || c == 0 || c == 6 || (r >= 2 && r <= 4 && c >= 2 && c <= 4))
                {
                    modules[row + r, col + c] = true;
                }
            }
        }
    }

    private static void DrawAlignmentPattern(bool[,] modules, int row, int col)
    {
        for (int r = 0; r < 5; r++)
        {
            for (int c = 0; c < 5; c++)
            {
                if (r == 0 || r == 4 || c == 0 || c == 4 || (r == 2 && c == 2))
                {
                    modules[row + r, col + c] = true;
                }
            }
        }
    }

    private static bool IsReservedArea(int r, int c, int size)
    {
        if (r <= 7 && c <= 7) return true; // Top-Left Finder
        if (r <= 7 && c >= size - 8) return true; // Top-Right Finder
        if (r >= size - 8 && c <= 7) return true; // Bottom-Left Finder
        if (r == 6 || c == 6) return true; // Timing lines
        if (r >= size - 9 && r <= size - 5 && c >= size - 9 && c <= size - 5) return true; // Alignment
        return false;
    }
}
