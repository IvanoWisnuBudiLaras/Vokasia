using Microsoft.EntityFrameworkCore;
using Vokasia.Api.Endpoints;
using Vokasia.Api.Validation;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;
using Vokasia.Infrastructure.TenantContext;

namespace Vokasia.Tests.Guard;

/// <summary>
/// AC VOK-H3-E3 §4 ValidationBoundaryTests: 500 vs 501 kar; foto ke-3 vs ke-4; content-type
/// application/x-msdownload -> tolak. Diuji LANGSUNG thd validator (bukan lewat HTTP) - lebih cepat
/// & terisolasi dari plumbing auth/tenant; batas HTTP-level yang sama sudah dibuktikan berulang di
/// Journal/JournalStudentEndpointsTests.cs (mis. SubmitJournal_TextTooLong_Returns400,
/// AttachPhoto_FourthPhoto_Returns409) - suite ini melengkapi dgn titik batas PERSIS di dua sisi
/// (N vs N+1), bukan cuma satu sisi "terlalu besar".
/// </summary>
public class ValidationBoundaryTests
{
    private static VokasiaDbContext NewInMemoryDb() =>
        new(new DbContextOptionsBuilder<VokasiaDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            new AmbientTenantContext());

    // ---------- SubmitJournalValidator: Text 500 vs 501 ----------

    [Fact]
    public async Task SubmitJournalValidator_Text500Chars_IsValid()
    {
        await using var db = NewInMemoryDb();
        var validator = new SubmitJournalValidator(db, new AmbientTenantContext());
        var req = new SubmitJournalRequest(Guid.NewGuid(), new string('a', 500), [], null);

        var result = await validator.ValidateAsync(req);

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public async Task SubmitJournalValidator_Text501Chars_IsInvalid()
    {
        await using var db = NewInMemoryDb();
        var validator = new SubmitJournalValidator(db, new AmbientTenantContext());
        var req = new SubmitJournalRequest(Guid.NewGuid(), new string('a', 501), [], null);

        var result = await validator.ValidateAsync(req);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Text");
    }

    // ---------- SubmitJournalValidator: PhotoIds ke-3 vs ke-4 ----------

    [Fact]
    public async Task SubmitJournalValidator_ThreePhotoIds_IsValid()
    {
        await using var db = NewInMemoryDb();
        var validator = new SubmitJournalValidator(db, new AmbientTenantContext());
        var req = new SubmitJournalRequest(Guid.NewGuid(), "teks", [], [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()]);

        var result = await validator.ValidateAsync(req);

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public async Task SubmitJournalValidator_FourPhotoIds_IsInvalid()
    {
        await using var db = NewInMemoryDb();
        var validator = new SubmitJournalValidator(db, new AmbientTenantContext());
        var req = new SubmitJournalRequest(Guid.NewGuid(), "teks", [], [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()]);

        var result = await validator.ValidateAsync(req);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "PhotoIds");
    }

    // ---------- SubmitJournalValidator: CompetencyIds kepemilikan major ----------

    [Fact]
    public async Task SubmitJournalValidator_CompetencyBelongsToCallerMajor_IsValid()
    {
        await using var db = NewInMemoryDb();
        var tenantId = Guid.NewGuid();
        var majorId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        db.Students.Add(new Student { Id = Guid.NewGuid(), TenantId = tenantId, UserId = userId, FullName = "Siswa Uji", MajorId = majorId, Classroom = "XII" });
        var competency = new Competency { Id = Guid.NewGuid(), TenantId = tenantId, MajorId = majorId, Name = "Kompetensi A" };
        db.Competencies.Add(competency);
        await db.SaveChangesAsync();

        var tenant = new AmbientTenantContext { UserId = userId, TenantId = tenantId };
        var validator = new SubmitJournalValidator(db, tenant);
        var req = new SubmitJournalRequest(Guid.NewGuid(), "teks", [competency.Id], null);

        var result = await validator.ValidateAsync(req);

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public async Task SubmitJournalValidator_CompetencyBelongsToDifferentMajor_IsInvalid()
    {
        await using var db = NewInMemoryDb();
        var tenantId = Guid.NewGuid();
        var callerMajor = Guid.NewGuid();
        var otherMajor = Guid.NewGuid();
        var userId = Guid.NewGuid();
        db.Students.Add(new Student { Id = Guid.NewGuid(), TenantId = tenantId, UserId = userId, FullName = "Siswa Uji", MajorId = callerMajor, Classroom = "XII" });
        var foreignCompetency = new Competency { Id = Guid.NewGuid(), TenantId = tenantId, MajorId = otherMajor, Name = "Kompetensi Jurusan Lain" };
        db.Competencies.Add(foreignCompetency);
        await db.SaveChangesAsync();

        var tenant = new AmbientTenantContext { UserId = userId, TenantId = tenantId };
        var validator = new SubmitJournalValidator(db, tenant);
        var req = new SubmitJournalRequest(Guid.NewGuid(), "teks", [foreignCompetency.Id], null);

        var result = await validator.ValidateAsync(req);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "CompetencyIds");
    }

    [Fact]
    public async Task SubmitJournalValidator_EmptyCompetencyIds_IsValid()
    {
        // [ASSUMPTION dicatat DECISIONS.md D25]: minimum-1 TIDAK ditegakkan (beda dari bacaan literal
        // "1-5" ticket) - lihat doc-comment SubmitJournalValidator.cs utk alasan lengkap.
        await using var db = NewInMemoryDb();
        var validator = new SubmitJournalValidator(db, new AmbientTenantContext());
        var req = new SubmitJournalRequest(Guid.NewGuid(), "teks", [], null);

        var result = await validator.ValidateAsync(req);

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    // ---------- UploadRequestValidator: content-type whitelist + ukuran ----------

    [Theory]
    [InlineData("application/x-msdownload")]
    [InlineData("text/html")]
    [InlineData("application/javascript")]
    public void UploadRequestValidator_DisallowedContentType_IsInvalid(string contentType)
    {
        var validator = new UploadRequestValidator();
        var req = new UploadRequest("berkas.bin", contentType, 1024);

        var result = validator.Validate(req);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "ContentType");
    }

    // JournalEndpoints.MaxPhotoSizeBytes bertipe `internal` (satu sumber kebenaran dgn
    // UploadRequestValidator DI DALAM assembly Vokasia.Api) - TIDAK terlihat dari assembly
    // Vokasia.Tests (internal = scoped per-assembly, tanpa InternalsVisibleTo yg sengaja tak
    // ditambahkan hanya demi 1 test ini). Nilai literal di bawah SENGAJA disalin persis (5MB),
    // bukan direferensi otomatis - kalau batasnya berubah di JournalEndpoints.cs, test ini perlu
    // diperbarui manual (trade-off diterima, drpd menambah surface InternalsVisibleTo).
    private const long FiveMegabytes = 5 * 1024 * 1024;

    [Fact]
    public void UploadRequestValidator_ExactlyFiveMegabytes_IsValid()
    {
        var validator = new UploadRequestValidator();
        var req = new UploadRequest("foto.jpg", "image/jpeg", FiveMegabytes);

        var result = validator.Validate(req);

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public void UploadRequestValidator_OneByteOverFiveMegabytes_IsInvalid()
    {
        var validator = new UploadRequestValidator();
        var req = new UploadRequest("foto.jpg", "image/jpeg", FiveMegabytes + 1);

        var result = validator.Validate(req);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "SizeBytes");
    }

    // ---------- RejectJournalValidator: Reason 5-300 kar ----------

    [Theory]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(300, true)]
    [InlineData(301, false)]
    public void RejectJournalValidator_ReasonLengthBoundaries(int length, bool expectedValid)
    {
        var validator = new RejectJournalValidator();
        var req = new RejectJournalRequest(new string('r', length));

        var result = validator.Validate(req);

        Assert.Equal(expectedValid, result.IsValid);
    }

    // ---------- CreatePeriodValidator: ClassLevels whitelist + Start<End ----------

    [Fact]
    public void CreatePeriodValidator_UnknownClassLevel_IsInvalid()
    {
        var validator = new CreatePeriodValidator();
        var req = new CreatePeriodRequest("Periode Uji", new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30), ["XIII"], null);

        var result = validator.Validate(req);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "ClassLevels");
    }

    [Fact]
    public void CreatePeriodValidator_StartAfterEnd_IsInvalid()
    {
        var validator = new CreatePeriodValidator();
        var req = new CreatePeriodRequest("Periode Uji", new DateOnly(2026, 6, 30), new DateOnly(2026, 1, 1), ["X"], null);

        var result = validator.Validate(req);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "EndDate");
    }

    [Fact]
    public void CreatePeriodValidator_ValidRequest_IsValid()
    {
        var validator = new CreatePeriodValidator();
        var req = new CreatePeriodRequest("Periode Uji", new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30), ["X", "XI", "XII"], null);

        var result = validator.Validate(req);

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }
}
