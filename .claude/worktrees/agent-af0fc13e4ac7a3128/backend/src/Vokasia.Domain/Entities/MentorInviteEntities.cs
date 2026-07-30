namespace Vokasia.Domain.Entities;

/// <summary>
/// Undangan mentor via magic link (FR-AUTH-03, VOK-H2-E3 §3 — slice yang sempat ditunda,
/// dikerjakan menyusul per catatan ticket sendiri: "magic link boleh geser pagi H3 — lapor,
/// jangan diam"). Token MENTAH tidak pernah disimpan — hanya <see cref="TokenHash"/> (SHA-256
/// hex), prinsip sama dengan refresh token reference OpenIddict: kalau tabel ini bocor, token
/// asli tetap tidak terekstrak balik.
///
/// <see cref="MentorName"/> disimpan di SINI (bukan ditambahkan ke <c>Placement</c>, yang
/// skemanya bagian dari kontrak gate M0) — snapshot nama saat undangan dibuat oleh staf yang
/// tahu identitas mentor asli (bukan dikarang/derive dari email), dipakai sbg
/// <c>AppUser.FullName</c> saat <c>ExchangeMagicToken</c> sukses membuat akun baru.
///
/// Tidak ada kolom TenantId eksplisit: isolasi tenant diwariskan dari <c>Placement</c> (yang
/// SUDAH tenant-scoped via global query filter) — <c>CreateMentorInvite</c> selalu look up
/// placement dulu lewat DbContext yang sama, jadi placement milik tenant lain otomatis
/// "tidak ditemukan" tanpa butuh kolom/filter tambahan di sini.
/// </summary>
public class MentorInvite
{
    public Guid Id { get; set; }
    public Guid PlacementId { get; set; }
    public string Email { get; set; } = default!;
    public string MentorName { get; set; } = default!;
    public string TokenHash { get; set; } = default!;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
