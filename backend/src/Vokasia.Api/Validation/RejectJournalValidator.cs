using FluentValidation;
using Vokasia.Api.Endpoints;

namespace Vokasia.Api.Validation;

/// <summary>VOK-H3-E3 §2. Reason 5–300 karakter (persis AC ticket) — MinimumLength(5) juga menolak kosong/whitespace, gantikan inline check lama di RejectJournal.</summary>
public class RejectJournalValidator : AbstractValidator<RejectJournalRequest>
{
    public RejectJournalValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Alasan penolakan wajib diisi.")
            .Length(5, 300).WithMessage("Alasan penolakan harus 5 sampai 300 karakter.");
    }
}
