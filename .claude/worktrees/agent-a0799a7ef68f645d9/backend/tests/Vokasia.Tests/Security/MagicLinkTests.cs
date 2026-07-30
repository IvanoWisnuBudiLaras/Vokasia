using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vokasia.Api.Auth;
using Vokasia.Api.Auth.MagicLink;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Identity;
using Vokasia.Infrastructure.Persistence;
using Vokasia.Tests.Auth;

namespace Vokasia.Tests.Security;

/// <summary>
/// AC VOK-H2-E3 §4 (slice yang sempat ditunda ke sesi ini, per catatan ticket sendiri: "magic link
/// boleh geser pagi H3 — lapor, jangan diam"): dipakai 2× → tolak; >72 jam → tolak; happy path →
/// session mentor. Diuji lewat HTTP NYATA ke /connect/token grant kustom (bukan cuma panggil
/// MagicLinkService in-process) — cermin disiplin PkceRequiredTest/ClaimsContentTest (buktikan
/// lewat request sungguhan, bukan hanya konfigurasi).
/// </summary>
public class MagicLinkTests : IClassFixture<VokasiaApiFactory>
{
    private readonly VokasiaApiFactory _factory;

    public MagicLinkTests(VokasiaApiFactory factory) => _factory = factory;

    private static string HashForTestFixture(string rawToken) =>
        // Replikasi SENGAJA dari MagicLinkService.Hash (private) — hanya utk menyusun fixture
        // "invite sudah kedaluwarsa" langsung di DB (skenario yang tidak bisa dicapai lewat
        // CreateInviteAsync publik, yang selalu set ExpiresAt = now+72h).
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    private async Task<(Guid PlacementId, string MentorEmail)> SeedPlacementAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();

        var placement = new Placement
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            PeriodId = Guid.NewGuid(),
            TeacherId = Guid.NewGuid(),
            MentorEmail = $"mentor-{Guid.NewGuid():N}@dudi.test",
            Status = PlacementStatus.Active,
        };
        db.Placements.Add(placement);
        await db.SaveChangesAsync();

        return (placement.Id, placement.MentorEmail!);
    }

    private async Task<string> ExchangeTokenGrantRawAsync(HttpClient client, string rawToken)
    {
        var response = await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = OpenIddictSetup.MagicLinkGrantType,
            ["token"] = rawToken,
            ["client_id"] = OpenIddictSetup.BffClientId,
            ["client_secret"] = "dev-only-secret-change-me",
        }));

        var body = await response.Content.ReadAsStringAsync();
        return response.IsSuccessStatusCode ? body : $"__FAILED__{(int)response.StatusCode}__{body}";
    }

    [Fact]
    public async Task HappyPath_CreateThenExchange_IssuesSessionAndLinksMentor()
    {
        var (placementId, mentorEmail) = await SeedPlacementAsync();
        string rawToken;
        MentorInviteDto? invite;

        using (var scope = _factory.Services.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<MagicLinkService>();
            var (ok, dto, error) = await svc.CreateInviteAsync(placementId, "Budi Santoso (Mentor)", CancellationToken.None);
            Assert.True(ok, error);
            invite = dto;
        }

        var query = QueryHelpers.ParseQuery(new Uri(invite!.MagicLinkUrl).Query);
        rawToken = query["token"]!;

        var client = _factory.CreateClient();
        var result = await ExchangeTokenGrantRawAsync(client, rawToken);

        Assert.False(result.StartsWith("__FAILED__"), $"Expected successful exchange, got: {result}");
        using var json = JsonDocument.Parse(result);
        Assert.True(json.RootElement.TryGetProperty("access_token", out _), "Response harus memuat access_token.");

        using var verifyScope = _factory.Services.CreateScope();
        var userManager = verifyScope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var db = verifyScope.ServiceProvider.GetRequiredService<VokasiaDbContext>();

        var mentorUser = await userManager.FindByEmailAsync(mentorEmail);
        Assert.NotNull(mentorUser);
        Assert.Equal(UserRole.IndustryMentor, mentorUser!.Role);
        Assert.Null(mentorUser.TenantId);
        Assert.Equal("Budi Santoso (Mentor)", mentorUser.FullName);

        var placement = await db.Placements.FirstAsync(p => p.Id == placementId);
        Assert.Equal(mentorUser.Id, placement.MentorUserId);
    }

    [Fact]
    public async Task UsedTwice_SecondExchange_IsRejected()
    {
        var (placementId, _) = await SeedPlacementAsync();
        MentorInviteDto? invite;

        using (var scope = _factory.Services.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<MagicLinkService>();
            var (ok, dto, error) = await svc.CreateInviteAsync(placementId, "Dua Kali Uji", CancellationToken.None);
            Assert.True(ok, error);
            invite = dto;
        }

        var rawToken = QueryHelpers.ParseQuery(new Uri(invite!.MagicLinkUrl).Query)["token"]!;
        var client = _factory.CreateClient();

        var first = await ExchangeTokenGrantRawAsync(client, rawToken!);
        Assert.False(first.StartsWith("__FAILED__"), $"Pemakaian pertama harus sukses, got: {first}");

        var second = await ExchangeTokenGrantRawAsync(client, rawToken!);
        Assert.True(second.StartsWith("__FAILED__"), "Pemakaian kedua (token sama) harus ditolak — sekali pakai.");
    }

    [Fact]
    public async Task ExpiredInvite_Exchange_IsRejected()
    {
        var (placementId, mentorEmail) = await SeedPlacementAsync();
        const string rawToken = "raw-token-kedaluwarsa-fixture-uji";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            db.MentorInvites.Add(new MentorInvite
            {
                Id = Guid.NewGuid(),
                PlacementId = placementId,
                Email = mentorEmail,
                MentorName = "Kedaluwarsa Uji",
                TokenHash = HashForTestFixture(rawToken),
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1), // sudah lewat 72 jam (bahkan lewat sama sekali).
            });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var result = await ExchangeTokenGrantRawAsync(client, rawToken);

        Assert.True(result.StartsWith("__FAILED__"), $"Token kedaluwarsa harus ditolak, got: {result}");
    }

    [Fact]
    public async Task UnknownGarbageToken_Exchange_IsRejected()
    {
        var client = _factory.CreateClient();
        var result = await ExchangeTokenGrantRawAsync(client, "token-ngarang-tidak-pernah-ada");

        Assert.True(result.StartsWith("__FAILED__"), $"Token tak dikenal harus ditolak, got: {result}");
    }

    [Fact]
    public async Task ValidateEndpointReflectsUsableState_WithoutConsumingToken()
    {
        var (placementId, _) = await SeedPlacementAsync();
        MentorInviteDto? invite;

        using (var scope = _factory.Services.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<MagicLinkService>();
            var (ok, dto, error) = await svc.CreateInviteAsync(placementId, "Validasi Tanpa Konsumsi", CancellationToken.None);
            Assert.True(ok, error);
            invite = dto;
        }

        var rawToken = QueryHelpers.ParseQuery(new Uri(invite!.MagicLinkUrl).Query)["token"]!;

        using (var scope = _factory.Services.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<MagicLinkService>();
            var validBefore = await svc.ValidateAsync(rawToken!, CancellationToken.None);
            Assert.True(validBefore, "Token yang belum dipakai harus valid.");
        }

        // ValidateAsync TIDAK boleh mengkonsumsi — exchange sungguhan sesudahnya harus tetap sukses.
        var client = _factory.CreateClient();
        var exchangeResult = await ExchangeTokenGrantRawAsync(client, rawToken!);
        Assert.False(exchangeResult.StartsWith("__FAILED__"), "Exchange pasca-validate (belum pernah dikonsumsi) harus tetap sukses.");

        using var scopeAfter = _factory.Services.CreateScope();
        var svcAfter = scopeAfter.ServiceProvider.GetRequiredService<MagicLinkService>();
        var validAfter = await svcAfter.ValidateAsync(rawToken!, CancellationToken.None);
        Assert.False(validAfter, "Setelah dikonsumsi, token yang sama harus tidak lagi valid.");
    }
}
