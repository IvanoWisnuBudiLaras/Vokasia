using FluentValidation;
using Vokasia.Api.Endpoints;

namespace Vokasia.Api.Validation;

public sealed class LearningAssessmentDraftRequestValidator : AbstractValidator<LearningAssessmentDraftRequest>
{
    public LearningAssessmentDraftRequestValidator()
    {
        RuleFor(request => request.OverallNote).MaximumLength(1500).When(request => request.OverallNote is not null);
        RuleFor(request => request.Criteria).Must(criteria => criteria.Select(item => item.CriterionSnapshotId).Distinct().Count() == criteria.Count)
            .WithMessage("Setiap kriteria hanya boleh dikirim sekali.");
        RuleForEach(request => request.Criteria).ChildRules(criterion =>
        {
            criterion.RuleFor(item => item.CriterionSnapshotId).NotEmpty();
            criterion.RuleFor(item => item.Score).InclusiveBetween(1, 5).When(item => item.Score.HasValue);
            criterion.RuleFor(item => item.Comment).MaximumLength(2000).When(item => item.Comment is not null);
            criterion.RuleFor(item => item.JournalEntryIds).Must(ids => ids.Distinct().Count() == ids.Count)
                .WithMessage("Evidence jurnal pada satu kriteria tidak boleh duplikat.");
        });
    }
}
