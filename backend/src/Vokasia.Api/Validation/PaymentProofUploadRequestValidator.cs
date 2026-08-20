using FluentValidation;
using Vokasia.Api.Endpoints;

namespace Vokasia.Api.Validation;

public class PaymentProofUploadRequestValidator : AbstractValidator<PaymentProofUploadRequest>
{
    private static readonly string[] AllowedContentTypes =
        ["image/jpeg", "image/png", "application/pdf"];

    private const long MaxSizeBytes = 10_000_000;

    public PaymentProofUploadRequestValidator()
    {
        RuleFor(request => request.FileName)
            .NotEmpty().WithMessage("Nama berkas wajib diisi.")
            .MaximumLength(255);

        RuleFor(request => request.ContentType)
            .Must(AllowedContentTypes.Contains)
            .WithMessage("Bukti harus JPG, PNG, atau PDF.");

        RuleFor(request => request.SizeBytes)
            .GreaterThan(0).WithMessage("Ukuran berkas tidak valid.")
            .LessThanOrEqualTo(MaxSizeBytes).WithMessage("Ukuran bukti maksimal 10 MB.");
    }
}
