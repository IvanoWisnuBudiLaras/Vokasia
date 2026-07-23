using FluentValidation;
using Vokasia.Api.Endpoints;

namespace Vokasia.Api.Validation;

/// <summary>VOK-H6-E1 §3 — paket langganan: nama wajib, angka non-negatif.</summary>
public class PlanRequestValidator : AbstractValidator<PlanRequest>
{
    public PlanRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Nama plan wajib diisi.").MaximumLength(100);
        RuleFor(x => x.PriceMonthly).GreaterThanOrEqualTo(0).WithMessage("PriceMonthly tidak boleh negatif.");
        RuleFor(x => x.MaxStudents).GreaterThan(0).WithMessage("MaxStudents harus lebih dari 0.");
        RuleFor(x => x.MaxPlacements).GreaterThan(0).WithMessage("MaxPlacements harus lebih dari 0.");
    }
}
