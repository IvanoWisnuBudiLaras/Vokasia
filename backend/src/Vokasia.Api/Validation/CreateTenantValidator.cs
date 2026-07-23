using FluentValidation;
using Vokasia.Api.Endpoints;

namespace Vokasia.Api.Validation;

/// <summary>VOK-H6-E1 §1 — wizard CreateTenant: field wajib + format email admin.</summary>
public class CreateTenantValidator : AbstractValidator<CreateTenantRequest>
{
    public CreateTenantValidator()
    {
        RuleFor(x => x.SchoolName).NotEmpty().WithMessage("Nama sekolah wajib diisi.").MaximumLength(200);
        RuleFor(x => x.City).NotEmpty().WithMessage("Kota wajib diisi.").MaximumLength(100);
        RuleFor(x => x.AdminEmail).NotEmpty().EmailAddress().WithMessage("Format email admin tidak valid.");
        RuleFor(x => x.AdminName).NotEmpty().WithMessage("Nama admin wajib diisi.").MaximumLength(200);
        RuleFor(x => x.PlanId).NotEmpty().WithMessage("PlanId wajib diisi.");
    }
}

/// <summary>VOK-H6-E1 §1 — UpdateTenant: field wajib sama dgn Create minus data admin (tak diubah lewat endpoint ini).</summary>
public class UpdateTenantValidator : AbstractValidator<UpdateTenantRequest>
{
    public UpdateTenantValidator()
    {
        RuleFor(x => x.SchoolName).NotEmpty().WithMessage("Nama sekolah wajib diisi.").MaximumLength(200);
        RuleFor(x => x.City).NotEmpty().WithMessage("Kota wajib diisi.").MaximumLength(100);
    }
}
