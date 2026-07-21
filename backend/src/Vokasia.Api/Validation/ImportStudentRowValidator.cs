using FluentValidation;
using Vokasia.Api.Endpoints;

namespace Vokasia.Api.Validation;

/// <summary>
/// VOK-H3-E3 §2. Dipakai PER BARIS CSV (StudentsEndpoints.ImportStudentsCsv memanggil
/// IValidator&lt;ImportStudentRow&gt;.ValidateAsync satu-satu di dalam loop parsing, BUKAN lewat
/// ValidationFilter global — baris dikonstruksi manual dari teks CSV, bukan argumen endpoint yang
/// diikat framework, jadi filter generik tak pernah melihatnya).
/// </summary>
public class ImportStudentRowValidator : AbstractValidator<ImportStudentRow>
{
    public ImportStudentRowValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithMessage("Wajib diisi.").MaximumLength(200);
        RuleFor(x => x.MajorName).NotEmpty().WithMessage("Wajib diisi.").MaximumLength(100);
        RuleFor(x => x.Classroom).NotEmpty().WithMessage("Wajib diisi.").MaximumLength(50);
        RuleFor(x => x.Nisn).MaximumLength(20).WithMessage("Maksimal 20 karakter.").When(x => !string.IsNullOrWhiteSpace(x.Nisn));
    }
}
