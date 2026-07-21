using FluentValidation;
using Vokasia.Api.Endpoints;

namespace Vokasia.Api.Validation;

/// <summary>VOK-H3-E3 §2. Guid FK wajib terisi (bukan Guid.Empty); MentorEmail format email bila diisi.</summary>
public class CreatePlacementValidator : AbstractValidator<CreatePlacementRequest>
{
    public CreatePlacementValidator()
    {
        RuleFor(x => x.StudentId).NotEqual(Guid.Empty).WithMessage("StudentId wajib diisi.");
        RuleFor(x => x.CompanyId).NotEqual(Guid.Empty).WithMessage("CompanyId wajib diisi.");
        RuleFor(x => x.PeriodId).NotEqual(Guid.Empty).WithMessage("PeriodId wajib diisi.");
        RuleFor(x => x.TeacherId).NotEqual(Guid.Empty).WithMessage("TeacherId wajib diisi.");

        RuleFor(x => x.MentorEmail)
            .EmailAddress().WithMessage("Format email mentor tidak valid.")
            .When(x => !string.IsNullOrWhiteSpace(x.MentorEmail));
    }
}
