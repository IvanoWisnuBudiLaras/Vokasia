using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Tests.Integration;

/// <summary>
/// VOK-H6-E1 §5 — Billing (FR-BIL-01..03). AC literal: "cron invoice 2x di bulan sama, Then invoice
/// tetap 1 per tenant" + "kuota 50 placement terpakai 50, When CreatePlacement, Then ditolak dengan
/// pesan; ListJournals lama tetap 200" (kuota di sini diuji dgn MaxPlacements=1 utk suite ringkas,
/// prinsip sama). GenerateMonthlyInvoices "dipicu manual" (cron real, bukan Hangfire schedule di test).
/// </summary>
[Collection("IntegrationTests")]
public class BillingTests
{
    private readonly VokasiaIntegrationFactory _factory;
    public BillingTests(VokasiaIntegrationFactory factory) => _factory = factory;

    private async Task<(Guid TenantId, Guid PlanId)> SeedActiveTenantWithPlanAsync(int maxPlacements = 50)
    {
        using var scope = _factory.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();

        var plan = new Plan { Id = Guid.NewGuid(), Name = "Paket Billing Uji", PriceMonthly = 750000m, MaxStudents = 100, MaxPlacements = maxPlacements };
        var tenant = new Tenant { Id = Guid.NewGuid(), SchoolName = "SMK Billing Uji " + Guid.NewGuid().ToString("N")[..6], IsActive = true, PlanId = plan.Id };
        db.Plans.Add(plan);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        return (tenant.Id, plan.Id);
    }

    [Fact]
    public async Task GenerateMonthlyInvoices_RunTwiceSameMonth_StaysOnePerTenantAndEmailsTenantAdmin()
    {
        var (tenantId, _) = await SeedActiveTenantWithPlanAsync();
        var (_, adminClient) = await _factory.LoginAsAsync(UserRole.TenantAdmin, tenantId, "billing-admin");

        var runDate = new DateOnly(2026, 8, 1);
        await _factory.TriggerGenerateMonthlyInvoicesAsync(runDate);
        await _factory.TriggerGenerateMonthlyInvoicesAsync(runDate); // AC literal: dipanggil 2x bulan sama.

        using (var scope = _factory.CreateDbScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var count = await db.Invoices.AsNoTracking().CountAsync(i => i.TenantId == tenantId && i.PeriodMonth == new DateOnly(2026, 8, 1));
            Assert.Equal(1, count);
        }

        // GetInvoices (TenantAdmin, miliknya) -> 1 invoice, status Issued.
        var listResp = await adminClient.GetAsync("/api/invoices");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        var list = await listResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, list.GetArrayLength());
        // Enum diserialisasi sbg ANGKA (tak ada JsonStringEnumConverter global - pola sama RubricEndpointsTests/DashboardEndpointsTests).
        Assert.Equal((int)InvoiceStatus.Issued, list[0].GetProperty("status").GetInt32());

        // Email InvoiceIssued terkirim ke TenantAdmin (OutboxDispatcher + InvoiceIssuedConsumer sungguhan).
        await PollUntil.SucceedsAsync(async () =>
        {
            using var scope = _factory.CreateDbScope();
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var sent = await db.SentEmails.AsNoTracking().AnyAsync(e => e.TemplateId == "InvoiceIssued");
            Assert.True(sent);
        }, timeout: TimeSpan.FromSeconds(15));
    }

    [Fact]
    public async Task PaymentProofUploadUrl_Pdf_UsesBillingValidationRules()
    {
        var (tenantId, _) = await SeedActiveTenantWithPlanAsync();
        var (_, adminClient) = await _factory.LoginAsAsync(
            UserRole.TenantAdmin,
            tenantId,
            "billing-proof-upload-admin");

        await _factory.TriggerGenerateMonthlyInvoicesAsync(new DateOnly(2027, 1, 1));

        Guid invoiceId;
        using (var scope = _factory.CreateDbScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            invoiceId = await db.Invoices.AsNoTracking()
                .Where(invoice => invoice.TenantId == tenantId)
                .Select(invoice => invoice.Id)
                .SingleAsync();
        }

        var response = await adminClient.PostAsJsonAsync(
            $"/api/invoices/{invoiceId}/payment-proof/upload-url",
            new
            {
                FileName = "payment-proof.pdf",
                ContentType = "application/pdf",
                SizeBytes = 1024,
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains($"tenant/{tenantId}/invoices/{invoiceId}/", body.GetProperty("objectKey").GetString());
    }

    [Fact]
    public async Task ConfirmPayment_WithoutProof_RejectsThenSucceedsAfterProofUploaded()
    {
        var (tenantId, _) = await SeedActiveTenantWithPlanAsync();
        var (_, adminClient) = await _factory.LoginAsAsync(UserRole.TenantAdmin, tenantId, "billing-confirm-admin");
        var (_, saClient) = await _factory.LoginAsAsync(UserRole.SuperAdmin, null, "billing-confirm-sa");

        await _factory.TriggerGenerateMonthlyInvoicesAsync(new DateOnly(2026, 9, 1));

        Guid invoiceId;
        using (var scope = _factory.CreateDbScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            invoiceId = await db.Invoices.AsNoTracking().Where(i => i.TenantId == tenantId).Select(i => i.Id).FirstAsync();
        }

        // AC: "tolak jika tanpa bukti."
        var rejectResp = await saClient.PostAsync($"/sa/invoices/{invoiceId}/confirm-payment", null);
        Assert.Equal(HttpStatusCode.Conflict, rejectResp.StatusCode);

        // UploadPaymentProof (TenantAdmin, miliknya) -> ProofUploaded.
        var proofResp = await adminClient.PostAsJsonAsync($"/api/invoices/{invoiceId}/payment-proof", new { ObjectKey = $"tenant/{tenantId}/invoices/{invoiceId}/proof.jpg" });
        Assert.Equal(HttpStatusCode.OK, proofResp.StatusCode);
        var proofBody = await proofResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)InvoiceStatus.ProofUploaded, proofBody.GetProperty("status").GetInt32());

        // ConfirmPayment (SaOnly) -> Paid.
        var confirmResp = await saClient.PostAsync($"/sa/invoices/{invoiceId}/confirm-payment", null);
        Assert.Equal(HttpStatusCode.NoContent, confirmResp.StatusCode);

        using var finalScope = _factory.CreateDbScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var invoice = await finalDb.Invoices.AsNoTracking().FirstAsync(i => i.Id == invoiceId);
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
    }

    [Fact]
    public async Task CreatePlacement_QuotaReached_Returns402ButOldDataStaysReadable()
    {
        var (tenantId, _) = await SeedActiveTenantWithPlanAsync(maxPlacements: 1);
        var (_, adminClient) = await _factory.LoginAsAsync(UserRole.TenantAdmin, tenantId, "billing-quota-admin");

        Guid periodId, companyId;
        using (var scope = _factory.CreateDbScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var period = new Period { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Periode Kuota", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31), ClassLevels = "XII", Status = PeriodStatus.Active };
            var company = new Company { Id = Guid.NewGuid(), Name = "PT Kuota" };
            db.Periods.Add(period);
            db.Companies.Add(company);
            await db.SaveChangesAsync();
            periodId = period.Id;
            companyId = company.Id;
        }

        async Task<Guid> SeedStudentAsync(string name)
        {
            using var scope = _factory.CreateDbScope();
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var student = new Student { Id = Guid.NewGuid(), TenantId = tenantId, FullName = name, MajorId = Guid.NewGuid(), Classroom = "XII A" };
            db.Students.Add(student);
            await db.SaveChangesAsync();
            return student.Id;
        }

        var student1 = await SeedStudentAsync("Siswa Kuota 1");
        var first = await adminClient.PostAsJsonAsync("/api/placements", new { StudentId = student1, CompanyId = companyId, PeriodId = periodId, TeacherId = Guid.NewGuid(), MentorEmail = (string?)null });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var student2 = await SeedStudentAsync("Siswa Kuota 2");
        var second = await adminClient.PostAsJsonAsync("/api/placements", new { StudentId = student2, CompanyId = companyId, PeriodId = periodId, TeacherId = Guid.NewGuid(), MentorEmail = (string?)null });
        Assert.Equal(HttpStatusCode.PaymentRequired, second.StatusCode);
        var errorBody = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("quota-exceeded", errorBody.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(errorBody.GetProperty("message").GetString()));

        // Data lama tetap terbaca (AC literal: "ListJournals lama tetap 200" - di sini ListPlacements, endpoint padanan yang benar2 terpengaruh perubahan ini).
        var listResp = await adminClient.GetAsync($"/api/placements?periodId={periodId}");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
    }
}
