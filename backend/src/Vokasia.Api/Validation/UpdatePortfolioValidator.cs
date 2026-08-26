using FluentValidation;
using Vokasia.Api.Endpoints;

namespace Vokasia.Api.Validation;

/// <summary>Slice 6 — the student may edit only the short portfolio headline; evidence is server-owned.</summary>
public class UpdatePortfolioValidator : AbstractValidator<UpdatePortfolioRequest>
{
    public UpdatePortfolioValidator()
    {
        RuleFor(x => x.Headline).MaximumLength(120).WithMessage("Headline maksimal 120 karakter.");
    }
}
