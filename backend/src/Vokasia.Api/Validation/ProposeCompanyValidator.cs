using FluentValidation;
using Vokasia.Api.Endpoints;

namespace Vokasia.Api.Validation;

/// <summary>VOK-H3-E3 §2. Name wajib; field opsional dibatasi panjang wajar (cegah payload berlebihan, bukan format ketat — data DUDI eksternal bentuknya beragam).</summary>
public class ProposeCompanyValidator : AbstractValidator<ProposeCompanyRequest>
{
    public ProposeCompanyValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Nama DUDI wajib diisi.").MaximumLength(200);
        RuleFor(x => x.Sector).MaximumLength(100);
        RuleFor(x => x.City).MaximumLength(100);
        RuleFor(x => x.Address).MaximumLength(300);
        RuleFor(x => x.ContactPerson).MaximumLength(150);
    }
}
