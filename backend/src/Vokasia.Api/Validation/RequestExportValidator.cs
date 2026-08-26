using FluentValidation;
using Vokasia.Api.Endpoints;

namespace Vokasia.Api.Validation;

public sealed class RequestExportValidator : AbstractValidator<RequestExportRequest>
{
    public RequestExportValidator()
    {
        RuleFor(x => x.Format)
            .IsInEnum()
            .WithMessage("Format export harus XLSX atau PDF.");
    }
}
