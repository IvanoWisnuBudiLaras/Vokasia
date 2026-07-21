using Microsoft.AspNetCore.Diagnostics;
using Vokasia.Domain.Common;

namespace Vokasia.Api.Middleware;

/// <summary>
/// VOK-H3-E3 §1: memetakan <see cref="DomainImmutableException"/> ke 409 Conflict + body konsisten
/// <c>{ code, message }</c> — BUKAN 500 generik (default ASP.NET Core utk exception tak tertangani).
/// Ini pelanggaran aturan bisnis yang diharapkan bisa terjadi (user coba ubah entry final), respons
/// harus bisa dibedakan FE dari error validasi (400, ValidationFilter) maupun error server sungguhan
/// (500). Pola <c>IExceptionHandler</c> (.NET 8+) dipakai — bukan middleware custom manual — supaya
/// konsisten dgn <c>AddProblemDetails()</c> bawaan framework utk exception lain (lihat Program.cs).
/// </summary>
public class DomainImmutableExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DomainImmutableException ex)
        {
            return false; // bukan wilayah handler ini — teruskan ke exception handler/ProblemDetails default.
        }

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        await httpContext.Response.WriteAsJsonAsync(new { code = ex.Code, message = ex.Message }, cancellationToken);
        return true;
    }
}
