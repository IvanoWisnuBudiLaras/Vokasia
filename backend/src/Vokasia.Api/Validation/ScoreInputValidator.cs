using FluentValidation;
using Vokasia.Api.Endpoints;

namespace Vokasia.Api.Validation;

public sealed class ScoreInputValidator : AbstractValidator<ScoreInput>
{
    public ScoreInputValidator()
    {
        RuleFor(x => x.AspectId).NotEmpty();
        RuleFor(x => x.Value).InclusiveBetween(0, 100);
        RuleFor(x => x.Comment).MaximumLength(2000).When(x => x.Comment is not null);
    }
}
