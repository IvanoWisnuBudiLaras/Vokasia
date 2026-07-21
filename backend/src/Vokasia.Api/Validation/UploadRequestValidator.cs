using FluentValidation;
using Vokasia.Api.Endpoints;

namespace Vokasia.Api.Validation;

/// <summary>VOK-H3-E3 §2. ContentType whitelist + ukuran ≤5MB — batas dibaca dari JournalEndpoints (internal const, satu sumber kebenaran dgn dulunya inline check di GetPresignedUploadUrl, kini dihapus di sana).</summary>
public class UploadRequestValidator : AbstractValidator<UploadRequest>
{
    public UploadRequestValidator()
    {
        RuleFor(x => x.FileName).NotEmpty().WithMessage("Nama berkas wajib diisi.").MaximumLength(255);

        RuleFor(x => x.ContentType)
            .Must(ct => JournalEndpoints.AllowedContentTypes.Contains(ct))
            .WithMessage("Tipe berkas hanya image/jpeg, image/png, atau image/webp.");

        RuleFor(x => x.SizeBytes)
            .GreaterThan(0).WithMessage("Ukuran berkas tidak valid.")
            .LessThanOrEqualTo(JournalEndpoints.MaxPhotoSizeBytes).WithMessage("Ukuran berkas maksimal 5MB.");
    }
}
