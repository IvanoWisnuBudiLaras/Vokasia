using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Identity;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Api.Auth.MagicLink;

public record CreateMentorInviteRequest(Guid PlacementId, string MentorName);
public record MentorInviteDto(Guid Id, Guid PlacementId, string Email, DateTimeOffset ExpiresAt, string MagicLinkUrl);

/// <summary>
/// VOK-H2-E3 §3 — CreateMentorInvite / ValidateMagicToken / ExchangeMagicToken (nama fungsi persis
/// ticket). Satu pintu untuk seluruh siklus hidup token magic link; dipakai
/// <see cref="MagicLinkEndpoints"/> (create+validate, HTTP biasa) dan
/// <see cref="AuthorizationController"/> (exchange, dipanggil dari cabang grant kustom
/// <c>OpenIddictSetup.MagicLinkGrantType</c> di endpoint OAuth <c>/connect/token</c> — BUKAN
/// endpoint REST terpisah, supaya penerbitan token tetap satu jalur OpenIddict, bukan jalur sesi
/// paralel ad-hoc).
/// </summary>
public class MagicLinkService
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(72); // AC ticket §3: TTL 72 jam persis.

    private readonly VokasiaDbContext _db;
    private readonly UserManager<AppUser> _userManager;
    private readonly IConfiguration _config;
    private readonly ILogger<MagicLinkService> _logger;

    public MagicLinkService(VokasiaDbContext db, UserManager<AppUser> userManager, IConfiguration config, ILogger<MagicLinkService> logger)
    {
        _db = db;
        _userManager = userManager;
        _config = config;
        _logger = logger;
    }

    private static string Hash(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    private static string GenerateRawToken()
    {
        // 32 byte acak -> base64url tanpa padding (aman ditaruh di query string URL email).
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    /// <summary>CreateMentorInvite(placementId, email) per ticket — email TIDAK diminta ulang, diambil dari Placement.MentorEmail (satu sumber, sudah diisi CreatePlacement H2-E1) supaya tak ada risiko ketik-ulang beda dgn yang tersimpan.</summary>
    public async Task<(bool Ok, MentorInviteDto? Invite, string? Error)> CreateInviteAsync(Guid placementId, string mentorName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(mentorName))
        {
            return (false, null, "Nama mentor wajib diisi.");
        }

        // Query lewat DbContext yang sama -> otomatis tenant-scoped (global filter Placement aktif
        // H2-E3); placement milik tenant lain = "tidak ditemukan", bukan bocor lintas-tenant.
        var placement = await _db.Placements.FirstOrDefaultAsync(p => p.Id == placementId, ct);
        if (placement is null)
        {
            return (false, null, "Placement tidak ditemukan.");
        }

        if (string.IsNullOrWhiteSpace(placement.MentorEmail))
        {
            return (false, null, "Placement ini belum punya email mentor — set dulu saat buat/ubah placement.");
        }

        var raw = GenerateRawToken();
        var invite = new MentorInvite
        {
            Id = Guid.NewGuid(),
            PlacementId = placementId,
            Email = placement.MentorEmail,
            MentorName = mentorName.Trim(),
            TokenHash = Hash(raw),
            ExpiresAt = DateTimeOffset.UtcNow.Add(Ttl),
        };
        _db.MentorInvites.Add(invite);

        // AC ticket §3: "publish MentorInvited (outbox; email terkirim H4, sementara log dev)".
        _db.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = "MentorInvited",
            PayloadJson = JsonSerializer.Serialize(new { invite.Id, invite.PlacementId, invite.Email, invite.ExpiresAt }),
        });

        await _db.SaveChangesAsync(ct);

        var appUrl = _config["Frontend:PublicUrl"] ?? "http://localhost:3000";
        var magicLinkUrl = $"{appUrl}/mentor-invite?token={raw}";

        // [GAP dicatat eksplisit, bukan diam-diam distub] SendEmail infra (SMTP/Resend) = H4-E3,
        // belum ada. Sementara: log dev + kembalikan URL di response API (staf yang buat undangan
        // meneruskan manual ke mentor via WhatsApp/dll) - lihat DECISIONS.md entry ticket ini.
        _logger.LogInformation(
            "[dev-only, tanpa infra email sampai H4-E3] Magic link mentor {Email}: {Url}",
            invite.Email, magicLinkUrl);

        return (true, new MentorInviteDto(invite.Id, invite.PlacementId, invite.Email, invite.ExpiresAt, magicLinkUrl), null);
    }

    private async Task<MentorInvite?> FindUsableAsync(string rawToken, CancellationToken ct)
    {
        var hash = Hash(rawToken);
        var invite = await _db.MentorInvites.FirstOrDefaultAsync(i => i.TokenHash == hash, ct);
        if (invite is null) return null;
        if (invite.UsedAt is not null) return null; // sekali pakai
        if (invite.ExpiresAt < DateTimeOffset.UtcNow) return null; // TTL 72 jam
        return invite;
    }

    /// <summary>ValidateMagicToken — TANPA konsumsi (dipakai halaman konfirmasi FE sebelum exchange, supaya email-scanner/link-preview yang auto-fetch URL tidak diam-diam membakar token sekali-pakai). Alasan gagal GENERIK by design (ticket §3: "jangan bocorkan mana yang salah") — caller hanya dapat true/false.</summary>
    public async Task<bool> ValidateAsync(string rawToken, CancellationToken ct) =>
        await FindUsableAsync(rawToken, ct) is not null;

    /// <summary>ExchangeMagicToken — tandai sekali pakai, buat/tautkan AppUser mentor, tautkan Placement.MentorUserId. TIDAK menerbitkan token OpenIddict sendiri (tanggung jawab caller: AuthorizationController.Exchange(), grant kustom) — supaya satu jalur penerbitan token, bukan dua.</summary>
    public async Task<(bool Ok, AppUser? User, string? Error)> ExchangeAsync(string rawToken, CancellationToken ct)
    {
        var invite = await FindUsableAsync(rawToken, ct);
        if (invite is null)
        {
            // Pesan SAMA utk notfound/used/expired -> tak bocorkan yang mana (ticket §3).
            return (false, null, "Tautan tidak valid atau sudah kedaluwarsa.");
        }

        var user = await _userManager.FindByEmailAsync(invite.Email);
        if (user is null)
        {
            user = new AppUser
            {
                Id = Guid.NewGuid(),
                UserName = invite.Email,
                Email = invite.Email,
                EmailConfirmed = true, // magic link ITU SENDIRI bukti kepemilikan email (FR-AUTH-03).
                FullName = invite.MentorName,
                Role = UserRole.IndustryMentor,
                TenantId = null, // mentor lintas-tenant by design (lihat PlacementScopeHandler).
                IsActive = true,
            };

            var create = await _userManager.CreateAsync(user); // TANPA password (passwordless by design, FR-AUTH-03).
            if (!create.Succeeded)
            {
                var msg = string.Join("; ", create.Errors.Select(e => e.Description));
                return (false, null, $"Gagal membuat akun mentor: {msg}");
            }
        }
        else if (user.Role != UserRole.IndustryMentor)
        {
            // Email sudah dipakai role lain -> tolak eksplisit, JANGAN diam-diam timpa role (privilege confusion).
            return (false, null, "Email ini sudah terdaftar dengan peran lain.");
        }
        else if (!user.IsActive)
        {
            return (false, null, "Akun mentor ini nonaktif.");
        }

        invite.UsedAt = DateTimeOffset.UtcNow; // sekali pakai (AC ticket §3) — ditulis SEBELUM SaveChanges tunggal di bawah (atomic dgn tautan placement).

        // Tanpa IgnoreQueryFilters(): request /connect/token ini anonim (belum ada klaim tenant_id
        // sama sekali), jadi filter Placement (!_tenantContext.TenantId.HasValue || ...) otomatis
        // menonaktifkan diri sendiri -> query ini tetap menjangkau placement tenant mana pun.
        var placement = await _db.Placements.FirstOrDefaultAsync(p => p.Id == invite.PlacementId, ct);
        if (placement is not null)
        {
            placement.MentorUserId = user.Id;
        }

        await _db.SaveChangesAsync(ct);

        return (true, user, null);
    }
}
