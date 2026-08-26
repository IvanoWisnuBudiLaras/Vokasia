namespace Vokasia.Api.Validation;

public static class RubricValidation
{
    public static bool HasValidWeights(IReadOnlyCollection<int> weights) =>
        weights.Count > 0 && weights.All(weight => weight is >= 0 and <= 100) && weights.Sum() == 100;
}
