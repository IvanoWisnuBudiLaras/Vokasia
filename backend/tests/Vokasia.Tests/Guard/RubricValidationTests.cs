using Vokasia.Api.Security;

namespace Vokasia.Tests.Guard;

public sealed class RubricValidationTests
{
    [Fact]
    public void WeightsMustSumTo100AndNeverBeNegative()
    {
        Assert.True(RubricValidation.HasValidWeights([40, 40, 20]));
        Assert.False(RubricValidation.HasValidWeights([200, -100]));
        Assert.False(RubricValidation.HasValidWeights([100, 1]));
    }
}
