using FluentValidation;
using Vokasia.Api.Endpoints;
using Vokasia.Domain.Common;

namespace Vokasia.Api.Validation;

/// <summary>
/// VOK-H3-E3 §2. Email format + FullName wajib + Role whitelist (Teacher/DeptHead/TenantAdmin) —
/// pola sama dgn check inline SchoolUsersEndpoints.InviteSchoolUser yang SENGAJA dipertahankan
/// (bukan dihapus) sbg defense-in-depth kedua; keduanya kini menegakkan aturan identik.
/// </summary>
public class InviteUserValidator : AbstractValidator<InviteUserRequest>
{
    public InviteUserValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("Format email tidak valid.");
        RuleFor(x => x.FullName).NotEmpty().WithMessage("Nama lengkap wajib diisi.").MaximumLength(200);
        RuleFor(x => x.Role)
            .Must(r => r is UserRole.Teacher or UserRole.DeptHead or UserRole.TenantAdmin)
            .WithMessage("Hanya Teacher, DeptHead, atau TenantAdmin yang bisa diundang lewat endpoint ini.");
    }
}
