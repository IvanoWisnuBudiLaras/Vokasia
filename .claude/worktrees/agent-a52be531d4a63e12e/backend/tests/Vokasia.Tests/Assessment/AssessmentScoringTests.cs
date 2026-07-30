using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;

namespace Vokasia.Tests.Assessment;

/// <summary>
/// VOK-H5-E1 §3/DoD: "Unit ComputeWeightedScore dulu (test-first)". Fungsi murni — test langsung
/// panggil <see cref="AssessmentScoring.ComputeWeightedScore"/> dgn data contoh, tanpa DbContext/mock.
/// 3 kasus manual (AC: "hitungan manual kasus uji") + 1 kasus kelengkapan.
/// </summary>
public class AssessmentScoringTests
{
    private static RubricAspect Aspect(string name, RubricAspectKind kind, int weight) =>
        new() { Id = Guid.NewGuid(), RubricTemplateId = Guid.Empty, Name = name, Kind = kind, Weight = weight };

    [Fact]
    public void ComputeWeightedScore_TwoAspectsEvenSplit_ReturnsExactWeightedAverage()
    {
        // Kasus manual #1: 50/50, nilai 80 & 90 -> (80*50 + 90*50)/100 = 85.00
        var teknis = Aspect("Teknis", RubricAspectKind.Teknis, 50);
        var softskill = Aspect("Softskill", RubricAspectKind.Softskill, 50);
        var scores = new Dictionary<Guid, decimal> { [teknis.Id] = 80m, [softskill.Id] = 90m };

        var result = AssessmentScoring.ComputeWeightedScore([teknis, softskill], scores);

        Assert.Equal(85.00m, result);
    }

    [Fact]
    public void ComputeWeightedScore_ThreeAspectsRealisticRubric_ReturnsExactWeightedAverage()
    {
        // Kasus manual #2: rubrik realistis 40/40/20 (Teknis+Kehadiran sisi mentor, Softskill sisi guru).
        // (85*40 + 90*40 + 75*20)/100 = (3400+3600+1500)/100 = 85.00
        var teknis = Aspect("Teknis", RubricAspectKind.Teknis, 40);
        var softskill = Aspect("Softskill", RubricAspectKind.Softskill, 40);
        var kehadiran = Aspect("Kehadiran", RubricAspectKind.Kehadiran, 20);
        var scores = new Dictionary<Guid, decimal> { [teknis.Id] = 85m, [softskill.Id] = 90m, [kehadiran.Id] = 75m };

        var result = AssessmentScoring.ComputeWeightedScore([teknis, softskill, kehadiran], scores);

        Assert.Equal(85.00m, result);
    }

    [Fact]
    public void ComputeWeightedScore_MidpointResult_RoundsAwayFromZero_NotToEven()
    {
        // Kasus manual #3: satu aspek weight=100, value=87.125 -> 8712.5/100 = 87.125 -> desimal ke-3
        // = 5 tepat di tengah. AwayFromZero => 87.13. Default .NET Math.Round (ToEven/banker's) akan
        // memberi 87.12 (2 genap) - inilah yg PROMPT D buktikan beda di bawah.
        var aspect = Aspect("Tunggal", RubricAspectKind.Teknis, 100);
        var scores = new Dictionary<Guid, decimal> { [aspect.Id] = 87.125m };

        var result = AssessmentScoring.ComputeWeightedScore([aspect], scores);

        Assert.Equal(87.13m, result);
    }

    [Fact]
    public void ComputeWeightedScore_MissingAspectScore_ThrowsExplicitException_NotSilentZero()
    {
        var teknis = Aspect("Teknis", RubricAspectKind.Teknis, 60);
        var softskill = Aspect("Softskill", RubricAspectKind.Softskill, 40);
        // Softskill sengaja tidak diisi.
        var scores = new Dictionary<Guid, decimal> { [teknis.Id] = 80m };

        var ex = Assert.Throws<AssessmentScoring.IncompleteScoresException>(
            () => AssessmentScoring.ComputeWeightedScore([teknis, softskill], scores));

        Assert.Contains("Softskill", ex.Message);
        Assert.Contains("Softskill", ex.MissingAspectNames);
    }
}
