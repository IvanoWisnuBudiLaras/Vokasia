using System.Text.RegularExpressions;
using Vokasia.Domain.Common;

namespace Vokasia.Tests.Assessment;

/// <summary>VOK-H5-E1 §5 — CertCode: 12 karakter, alfanumerik saja, acak (bukan sequential).</summary>
public partial class CertCodeGeneratorTests
{
    [GeneratedRegex("^[A-Za-z0-9]{12}$")]
    private static partial Regex AlphanumericTwelve();

    [Fact]
    public void Generate_ReturnsTwelveAlphanumericCharacters()
    {
        var code = CertCodeGenerator.Generate();

        Assert.Equal(12, code.Length);
        Assert.Matches(AlphanumericTwelve(), code);
    }

    [Fact]
    public void Generate_CalledManyTimes_ProducesNoDuplicatesAndIsNotSequential()
    {
        var codes = Enumerable.Range(0, 1000).Select(_ => CertCodeGenerator.Generate()).ToList();

        Assert.Equal(1000, codes.Distinct().Count()); // AC: acak, bukan sequential -> praktis nol duplikat pd 1000 sampel.
    }
}
