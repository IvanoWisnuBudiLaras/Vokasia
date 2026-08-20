using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vokasia.Api.Auth;
using Vokasia.Api.Validation;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Api.Endpoints;

/// <summary>VOK-H2-E1 §3: CRUD siswa + import CSV (kolom umum Dapodik). Data minimal (NFR-SEC-05).</summary>
public static class StudentsEndpoints
{
    public static IEndpointRouteBuilder MapStudentsEndpoints(this IEndpointRouteBuilder app)
    {
        // VOK-H3-E3 §2: ValidationFilter global (baru CreateStudentRequest/UpdateStudentRequest tanpa
        // validator terdaftar sampai saat ini - lolos apa adanya, dicatat DECISIONS.md; ImportStudentRow
        // divalidasi manual per-baris di ImportStudentsCsv, BUKAN lewat filter ini - lihat komentarnya).
        var group = app.MapGroup("/api/students").WithTags("Students").AddEndpointFilter<ValidationFilter>();

        group.MapPost("/import", ImportStudentsCsv).RequireAuthorization(RbacPolicies.DeptHeadPlus).DisableAntiforgery();
        group.MapPost("/", CreateStudent).RequireAuthorization(RbacPolicies.DeptHeadPlus);
        group.MapPut("/{id:guid}", UpdateStudent).RequireAuthorization(RbacPolicies.DeptHeadPlus);
        group.MapGet("/", ListStudents).RequireAuthorization(RbacPolicies.TenantMember);
        group.MapGet("/majors", ListMajors).RequireAuthorization(RbacPolicies.TenantMember);

        return app;
    }

    /// <summary>
    /// Kolom CSV yang didukung (header wajib): FullName, Nisn, MajorName, Classroom.
    /// [ASSUMPTION]: MajorName dicocokkan by-name per tenant (dibuat otomatis bila belum ada) —
    /// belum ada mapping resmi kolom Dapodik penuh di PRD, jadi dipilih subset paling umum.
    /// dryRun=true → validasi saja, tidak ada baris ditulis ke DB.
    /// </summary>
    private static async Task<IResult> ImportStudentsCsv(
        IFormFile file, [FromQuery] bool dryRun, VokasiaDbContext db, ITenantContext tenant,
        IValidator<ImportStudentRow> rowValidator, CancellationToken ct)
    {
        if (!tenant.TenantId.HasValue)
        {
            return Results.Forbid();
        }

        var errors = new List<ImportRowError>();
        var imported = 0;
        var majorCache = await db.Majors.Where(m => m.TenantId == tenant.TenantId).ToDictionaryAsync(m => m.Name, m => m.Id, StringComparer.OrdinalIgnoreCase, ct);

        using var reader = new StreamReader(file.OpenReadStream());
        var headerLine = await reader.ReadLineAsync(ct);
        if (headerLine is null)
        {
            return Results.Ok(new ImportResultDto(0, [new ImportRowError(0, "*", "File CSV kosong.")]));
        }

        var headers = headerLine.Split(',').Select(h => h.Trim()).ToArray();
        int idx(string name) => Array.FindIndex(headers, h => string.Equals(h, name, StringComparison.OrdinalIgnoreCase));
        var (iFullName, iNisn, iMajor, iClassroom) = (idx("FullName"), idx("Nisn"), idx("MajorName"), idx("Classroom"));

        if (iFullName < 0 || iMajor < 0 || iClassroom < 0)
        {
            return Results.Ok(new ImportResultDto(0, [new ImportRowError(0, "header", "Kolom wajib: FullName, MajorName, Classroom.")]));
        }

        var rowNum = 1;
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            rowNum++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var cols = line.Split(',').Select(c => c.Trim()).ToArray();
            var fullName = cols.ElementAtOrDefault(iFullName) ?? "";
            var majorName = cols.ElementAtOrDefault(iMajor) ?? "";
            var classroom = cols.ElementAtOrDefault(iClassroom) ?? "";
            var nisn = iNisn >= 0 ? cols.ElementAtOrDefault(iNisn) : null;

            // VOK-H3-E3 §2: ImportStudentRowValidator dipanggil manual per baris (bukan lewat
            // ValidationFilter global — baris ini dikonstruksi dari teks CSV, bukan argumen endpoint
            // yang diikat framework). Semua field yang gagal pada baris ini dikumpulkan sekaligus
            // (bukan berhenti di kegagalan pertama seperti inline check lama) — lebih informatif
            // utk staf yang memperbaiki CSV, kontrak ImportResultDto tak berubah (tetap List<ImportRowError>).
            var rowResult = await rowValidator.ValidateAsync(new ImportStudentRow(fullName, nisn, majorName, classroom), ct);
            if (!rowResult.IsValid)
            {
                foreach (var failure in rowResult.Errors)
                {
                    errors.Add(new ImportRowError(rowNum, failure.PropertyName, failure.ErrorMessage));
                }
                continue;
            }

            if (!majorCache.TryGetValue(majorName, out var majorId))
            {
                var major = new Major { Id = Guid.NewGuid(), TenantId = tenant.TenantId.Value, Name = majorName };
                if (!dryRun)
                {
                    db.Majors.Add(major);
                }

                majorId = major.Id;
                majorCache[majorName] = majorId;
            }

            if (!dryRun)
            {
                db.Students.Add(new Student
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.TenantId.Value,
                    FullName = fullName,
                    Nisn = string.IsNullOrWhiteSpace(nisn) ? null : nisn,
                    MajorId = majorId,
                    Classroom = classroom,
                });
            }

            imported++;
        }

        if (!dryRun && imported > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return Results.Ok(new ImportResultDto(imported, errors));
    }

    private static async Task<IResult> CreateStudent(CreateStudentRequest req, VokasiaDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        if (!tenant.TenantId.HasValue)
        {
            return Results.Forbid();
        }

        var student = new Student
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.TenantId.Value,
            FullName = req.FullName,
            Nisn = req.Nisn,
            MajorId = req.MajorId,
            Classroom = req.Classroom,
        };
        db.Students.Add(student);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/students/{student.Id}", ToDto(student));
    }

    private static async Task<IResult> UpdateStudent(Guid id, UpdateStudentRequest req, VokasiaDbContext db, CancellationToken ct)
    {
        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (student is null)
        {
            return Results.NotFound();
        }

        student.FullName = req.FullName;
        student.Nisn = req.Nisn;
        student.MajorId = req.MajorId;
        student.Classroom = req.Classroom;
        await db.SaveChangesAsync(ct);

        return Results.Ok(ToDto(student));
    }

    private static async Task<IResult> ListStudents(
        VokasiaDbContext db, CancellationToken ct,
        [FromQuery] Guid? majorId = null, [FromQuery] string? classroom = null, [FromQuery] string? search = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var query = db.Students.AsNoTracking().AsQueryable();
        if (majorId.HasValue)
        {
            query = query.Where(s => s.MajorId == majorId.Value);
        }

        if (!string.IsNullOrWhiteSpace(classroom))
        {
            query = query.Where(s => s.Classroom == classroom);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(s => EF.Functions.ILike(s.FullName, $"%{search}%"));
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(s => s.FullName).Skip((page - 1) * pageSize).Take(pageSize).Select(s => ToDto(s)).ToListAsync(ct);

        return Results.Ok(new Paged<StudentDto>(items, page, pageSize, total));
    }

    private static async Task<IResult> ListMajors(VokasiaDbContext db, CancellationToken ct)
    {
        var items = await db.Majors.AsNoTracking().OrderBy(m => m.Name).Select(m => new MajorOptionDto(m.Id, m.Name)).ToListAsync(ct);
        return Results.Ok(items);
    }

    private static StudentDto ToDto(Student s) => new(s.Id, s.FullName, s.Nisn, s.MajorId, s.Classroom, s.UserId);
}
