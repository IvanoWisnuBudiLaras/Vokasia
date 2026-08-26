using FluentValidation;
using Vokasia.Api.Endpoints;

namespace Vokasia.Api.Validation;

public class RejectPaymentRequestValidator : AbstractValidator<RejectPaymentRequest>
{
    public RejectPaymentRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Alasan penolakan pembayaran wajib diisi.")
            .Length(5, 500).WithMessage("Alasan penolakan harus antara 5 hingga 500 karakter.");
    }
}
