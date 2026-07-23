using Microsoft.AspNetCore.Diagnostics;
using Vokasia.Domain.Common;

namespace Vokasia.Api.Middleware;

/// <summary>
/// VOK-H6-E1 §5 (FR-BIL-03): memetakan <see cref="QuotaExceededException"/> ke 402 Payment Required
/// + body konsisten <c>{ code, message }</c> — pola SAMA PERSIS dgn DomainImmutableExceptionHandler
/// (H3-E3 §1), 402 dipilih (bukan 409) krn ticket literal "402/409" & 402 lebih presisi mengungkap
/// SEBAB (kuota paket langganan, bukan sekadar konflik state data biasa).
/// </summary>
public class QuotaExceededExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not QuotaExceededException ex)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status402PaymentRequired;
        await httpContext.Response.WriteAsJsonAsync(new { code = "quota-exceeded", message = ex.Message }, cancellationToken);
        return true;
    }
}
