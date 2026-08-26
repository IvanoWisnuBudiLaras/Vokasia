using FluentValidation;
using Vokasia.Api.Endpoints;

namespace Vokasia.Api.Validation;

public sealed class RevokeCertificateValidator : AbstractValidator<RevokeCertificateRequest>
{
    public RevokeCertificateValidator()
    {
        RuleFor(x => x.PublicReason)
            .NotEmpty()
            .MaximumLength(240)
            .WithMessage("Alasan pencabutan publik wajib diisi dan maksimal 240 karakter.");
        RuleFor(x => x.InternalNote)
            .MaximumLength(1000)
            .WithMessage("Catatan internal maksimal 1000 karakter.");
    }
}
