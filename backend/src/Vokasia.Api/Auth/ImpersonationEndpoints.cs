using Microsoft.EntityFrameworkCore;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Identity;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Api.Auth;

/// <summary>
/// VOK-H6-E3 §1 — pasangan StartImpersonation (AuthorizationController.Exchange(), custom grant
/// OpenIddict, lihat OpenIddictSetup.ImpersonationGrantType). EndImpersonation SENGAJA endpoint API
/// biasa (bukan grant OAuth lain) — tidak ada token baru yang perlu diterbitkan DI SINI: BFF Next.js
/// (app/api/sa/impersonate/end/route.ts) yang mengembalikan sesi ke token SuperAdmin ASLI yang sudah
/// distash saat StartImpersonation (BFF-side, lihat frontend/src/lib/bffSession.ts). Endpoint ini
/// HANYA menulis jejak audit penutup — dipanggil BFF SEBELUM token impersonasi dibuang, selagi
/// masih ada Bearer token target+impersonator_id yang valid utk mengisi ITenantContext.ImpersonatorUserId
/// (kalau dipanggil SESUDAH token dibuang, tidak ada lagi cara membuktikan siapa yang mengakhiri).
/// </summary>
public static class ImpersonationEndpoints
{
    public static IEndpointRouteBuilder MapImpersonationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/impersonation/end", EndImpersonation).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> EndImpersonation(VokasiaDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        if (!tenant.ImpersonatorUserId.HasValue || !tenant.UserId.HasValue)
        {
            // AC hanya bicara ttg impersonasi yang SEDANG berjalan — panggilan tanpa token impersonasi
            // aktif bukan error server, tapi memang tidak ada apa pun utk diakhiri/diaudit.
            return Results.BadRequest(new { message = "Tidak sedang dalam sesi impersonasi." });
        }

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.TenantId,
            ActorUserId = tenant.UserId.Value, // = user TARGET saat ini — dikoreksi otomatis jadi SA asli oleh VokasiaDbContext.SaveChangesAsync (ActingAsUserId = nilai ini).
            Action = "ImpersonationEnded",
            Entity = nameof(AppUser),
            EntityId = tenant.UserId.Value.ToString(),
            MetaJson = "{}",
        });
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }
}
