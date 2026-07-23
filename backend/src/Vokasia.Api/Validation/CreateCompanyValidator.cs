using FluentValidation;
using Vokasia.Api.Endpoints;

namespace Vokasia.Api.Validation;

/// <summary>VOK-H6-E1 §2 — CreateCompany (SA): Name wajib.</summary>
public class CreateCompanyValidator : AbstractValidator<CreateCompanyRequest>
{
    public CreateCompanyValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Nama perusahaan wajib diisi.").MaximumLength(200);
    }
}

/// <summary>VOK-H6-E1 §2 — MergeCompanies: SourceId/TargetId wajib ada.</summary>
public class MergeCompaniesValidator : AbstractValidator<MergeCompaniesRequest>
{
    public MergeCompaniesValidator()
    {
        RuleFor(x => x.SourceId).NotEmpty();
        RuleFor(x => x.TargetId).NotEmpty();
    }
}
