using FluentValidation;
using Vokasia.Api.Endpoints;

namespace Vokasia.Api.Validation;

public sealed class CreateRubricRequestValidator : AbstractValidator<CreateRubricRequest>
{
    public CreateRubricRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Aspects).NotEmpty().Must(items => items.Count <= 30).WithMessage("Rubrik maksimal memiliki 30 aspek.");
        RuleForEach(x => x.Aspects).ChildRules(aspect =>
        {
            aspect.RuleFor(item => item.Name).NotEmpty().MaximumLength(200);
            aspect.RuleFor(item => item.Description).MaximumLength(1000).When(item => item.Description is not null);
        });
    }
}

public sealed class UpdateRubricRequestValidator : AbstractValidator<UpdateRubricRequest>
{
    public UpdateRubricRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Aspects).NotEmpty().Must(items => items.Count <= 30).WithMessage("Rubrik maksimal memiliki 30 aspek.");
        RuleForEach(x => x.Aspects).ChildRules(aspect =>
        {
            aspect.RuleFor(item => item.Name).NotEmpty().MaximumLength(200);
            aspect.RuleFor(item => item.Description).MaximumLength(1000).When(item => item.Description is not null);
        });
    }
}
