using FluentValidation;
using Vokasia.Api.Endpoints;

namespace Vokasia.Api.Validation;

public sealed class CreateStudentRequestValidator : AbstractValidator<CreateStudentRequest>
{
    public CreateStudentRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Nisn).MaximumLength(20).When(x => !string.IsNullOrWhiteSpace(x.Nisn));
        RuleFor(x => x.MajorId).NotEmpty();
        RuleFor(x => x.Classroom).NotEmpty().MaximumLength(50);
    }
}

public sealed class UpdateStudentRequestValidator : AbstractValidator<UpdateStudentRequest>
{
    public UpdateStudentRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Nisn).MaximumLength(20).When(x => !string.IsNullOrWhiteSpace(x.Nisn));
        RuleFor(x => x.MajorId).NotEmpty();
        RuleFor(x => x.Classroom).NotEmpty().MaximumLength(50);
    }
}
