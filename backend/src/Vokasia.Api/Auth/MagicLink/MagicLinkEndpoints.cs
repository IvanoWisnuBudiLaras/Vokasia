using Microsoft.AspNetCore.Mvc;

namespace Vokasia.Api.Auth.MagicLink;

/// <summary>
/// VOK-H2-E3 §3 — permukaan REST utk create+validate. Exchange SENGAJA tidak di sini (lihat
/// AuthorizationController: exchange lewat /connect/token grant kustom, bukan endpoint REST
/// terpisah, supaya satu jalur penerbitan token).
/// </summary>
public static class MagicLinkEndpoints
{
    public static IEndpointRouteBuilder MapMagicLinkEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/mentor-invites").WithTags("MagicLink");

        // TeacherPlus (TenantAdmin/DeptHead/Teacher): guru yang membimbing placement sehari-hari
        // biasanya yang mengundang mentor DUDI-nya, sama semangat dgn AssignTeacher/H2-E1.
        group.MapPost("/", CreateInvite).RequireAuthorization(RbacPolicies.TeacherPlus);

        // Anonim BY DESIGN: dipanggil halaman publik /mentor-invite (FE) SEBELUM mentor punya
        // sesi apa pun — mustahil digerbangi RBAC (mentor belum jadi AppUser saat validasi).
        group.MapGet("/validate", Validate);

        return app;
    }

    private static async Task<IResult> CreateInvite(CreateMentorInviteRequest req, MagicLinkService svc, CancellationToken ct)
    {
        var (ok, invite, error) = await svc.CreateInviteAsync(req.PlacementId, req.MentorName, ct);
        return ok ? Results.Created($"/api/mentor-invites/{invite!.Id}", invite) : Results.BadRequest(new { message = error });
    }

    private static async Task<IResult> Validate([FromQuery] string token, MagicLinkService svc, CancellationToken ct)
    {
        var valid = await svc.ValidateAsync(token, ct);
        return Results.Ok(new { valid });
    }
}
