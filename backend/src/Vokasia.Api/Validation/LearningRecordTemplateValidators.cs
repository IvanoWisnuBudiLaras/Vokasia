using FluentValidation;
using Vokasia.Api.Endpoints;

namespace Vokasia.Api.Validation;

public sealed class CreateLearningRecordTemplateRequestValidator : AbstractValidator<CreateLearningRecordTemplateRequest>
{
    public CreateLearningRecordTemplateRequestValidator()
    {
        RuleFor(request => request.CompanyId).NotEmpty();
        RuleFor(request => request.Criteria).NotEmpty().Must(criteria => criteria.Count <= 20)
            .WithMessage("Template Learning Record maksimal memiliki 20 kriteria.");
        RuleForEach(request => request.Criteria).ChildRules(criterion =>
        {
            criterion.RuleFor(item => item.Name).NotEmpty().MaximumLength(200);
            criterion.RuleFor(item => item.Description).NotEmpty().MaximumLength(1000);
        });
    }
}

public sealed class UpdateLearningRecordTemplateRequestValidator : AbstractValidator<UpdateLearningRecordTemplateRequest>
{
    public UpdateLearningRecordTemplateRequestValidator()
    {
        RuleFor(request => request.Criteria).NotEmpty().Must(criteria => criteria.Count <= 20)
            .WithMessage("Template Learning Record maksimal memiliki 20 kriteria.");
        RuleForEach(request => request.Criteria).ChildRules(criterion =>
        {
            criterion.RuleFor(item => item.Name).NotEmpty().MaximumLength(200);
            criterion.RuleFor(item => item.Description).NotEmpty().MaximumLength(1000);
        });
    }
}
