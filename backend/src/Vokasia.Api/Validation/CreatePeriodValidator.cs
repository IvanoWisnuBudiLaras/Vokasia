using FluentValidation;
using Vokasia.Api.Endpoints;

namespace Vokasia.Api.Validation;

/// <summary>VOK-H3-E3 §2. Tanggal valid + Start&lt;End; ClassLevels ⊆ {X, XI, XII} & tidak kosong.</summary>
public class CreatePeriodValidator : AbstractValidator<CreatePeriodRequest>
{
    private static readonly string[] ValidClassLevels = ["X", "XI", "XII"];

    public CreatePeriodValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Nama periode wajib diisi.").MaximumLength(200);

        RuleFor(x => x.EndDate)
            .Must((req, endDate) => req.StartDate < endDate)
            .WithMessage("StartDate harus sebelum EndDate.");

        RuleFor(x => x.ClassLevels)
            .NotEmpty().WithMessage("Pilih minimal 1 tingkat kelas.")
            .Must(levels => levels.All(l => ValidClassLevels.Contains(l)))
            .WithMessage("ClassLevels hanya boleh berisi X, XI, atau XII.");
    }
}
