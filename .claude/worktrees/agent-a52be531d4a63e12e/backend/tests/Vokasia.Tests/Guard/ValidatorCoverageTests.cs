using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Vokasia.Api.Endpoints;
using Vokasia.Tests.Auth;

namespace Vokasia.Tests.Guard;

/// <summary>
/// AC VOK-H3-E3 §2/§4 ValidatorCoverageTests ("AllRequestsHaveValidatorsTest" per ticket). Reflection
/// thd DAFTAR TETAP 8 request type yang disebut EKSPLISIT ticket §2 - BUKAN scan otomatis atas semua
/// tipe request minimal-API di seluruh scope H1-H3 (itu butuh menyimpulkan mana argumen endpoint
/// yang "body-bound" murni via reflection route metadata, rapuh & di luar effort ticket ini). Request
/// type LAIN di scope H1-H3 yang BELUM py validator (mis. UpdatePeriodRequest, CreateStudentRequest,
/// UpdateStudentRequest, CreateMentorInviteRequest, ApproveJournalRequest, AddCommentRequest,
/// BatchApproveRequest) adalah GAP JUJUR, dicatat DECISIONS.md D25 - bukan diam-diam diklaim tercakup
/// oleh test ini.
/// </summary>
public class ValidatorCoverageTests : IClassFixture<VokasiaApiFactory>
{
    private readonly VokasiaApiFactory _factory;
    public ValidatorCoverageTests(VokasiaApiFactory factory) => _factory = factory;

    public static IEnumerable<object[]> NamedRequestTypes()
    {
        yield return [typeof(SubmitJournalRequest)];
        yield return [typeof(CreatePeriodRequest)];
        yield return [typeof(CreatePlacementRequest)];
        yield return [typeof(ImportStudentRow)];
        yield return [typeof(InviteUserRequest)];
        yield return [typeof(ProposeCompanyRequest)];
        yield return [typeof(UploadRequest)];
        yield return [typeof(RejectJournalRequest)];
    }

    [Theory]
    [MemberData(nameof(NamedRequestTypes))]
    public void RequestType_HasValidatorRegisteredInDi(Type requestType)
    {
        using var scope = _factory.Services.CreateScope();
        var validatorType = typeof(IValidator<>).MakeGenericType(requestType);

        var validator = scope.ServiceProvider.GetService(validatorType);

        Assert.NotNull(validator);
    }
}
