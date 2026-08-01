using FluentValidation;
using Vokasia.Api.Endpoints;

namespace Vokasia.Api.Validation;

/// <summary>VOK-H6-E1 §6 — Headline≤120 (ticket literal), SampleJournalIds≤6 (ticket literal), tanpa duplikat.</summary>
public class UpdatePortfolioValidator : AbstractValidator<UpdatePortfolioRequest>
{
    public UpdatePortfolioValidator()
    {
        RuleFor(x => x.Headline).MaximumLength(120).WithMessage("Headline maksimal 120 karakter.");
        RuleFor(x => x.SampleJournalIds)
            .Must(ids => ids == null || ids.Count <= 6)
            .WithMessage("Maksimal 6 sampel jurnal.")
            .Must(ids => ids == null || ids.Distinct().Count() == ids.Count)
            .WithMessage("SampleJournalIds tidak boleh berisi duplikat.");
    }
}
