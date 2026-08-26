using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Vokasia.Infrastructure;
using Vokasia.Infrastructure.Messaging;
using Vokasia.Infrastructure.Persistence;
using Vokasia.Worker;
using Vokasia.Worker.Consumers;
using Vokasia.Worker.Health;
using Vokasia.Worker.Jobs;

if (args is ["--healthcheck"])
{
    var markerPath = Environment.GetEnvironmentVariable("Worker__ReadinessFile")
        ?? WorkerReadinessMarker.DefaultFilePath;
    Environment.ExitCode = WorkerReadinessMarker.IsFresh(
        markerPath,
        TimeProvider.System.GetUtcNow(),
        WorkerReadinessMarker.MaxMarkerAge)
        ? 0
        : 1;
    return;
}

// VOK-H5-E1 §4/§5: QuestPDF (pre-approved PRD.md baris 82) wajib deklarasi lisensi eksplisit
// sebelum Document.Create dipanggil manapun (versi modern QuestPDF melempar exception saat
// generate kalau belum di-set) - Community cukup (non-komersial/proyek internal skala kita).
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = Host.CreateApplicationBuilder(args);

// DbContext (Npgsql, fallback conn string sama dgn Vokasia.Api) + Redis + MinIO client — satu
// titik registrasi dipakai bersama, lihat Vokasia.Infrastructure/DependencyInjection.cs. Worker
// TIDAK punya AmbientTenantContext yang di-set (tak ada HTTP request) -> query filter tenant di
// VokasiaDbContext otomatis "mati", cron LINTAS SEMUA TENANT by design (lihat doc-comment
// JournalCronJobs).
builder.Services.AddVokasiaInfrastructure(builder.Configuration, builder.Environment);

// VOK-H6-E1 §1: TenantAdminInvitedConsumer memakai UserManager<AppUser> (FindByIdAsync +
// SetAuthenticationTokenAsync utk invitation token) — AddVokasiaInfrastructure TIDAK
// mendaftarkan Identity (lihat doc-comment DependencyInjection.cs), jadi Worker wajib
// mendaftarkan core Identity sendiri; API memakainya via AddVokasiaIdentity (IdentitySetup.cs)
// yang juga memanggil AddVokasiaIdentityCore (satu sumber opsi, tanpa dobel-registrasi).
builder.Services.AddVokasiaIdentityCore();

// Readiness worker mencakup dependency yang benar-benar diperlukan. MassTransit menambahkan
// check RabbitMQ ber-tag "ready" sendiri; tiga check di bawah melengkapi Postgres, Redis, MinIO.
builder.Services.AddHealthChecks()
    .AddCheck<WorkerPostgresHealthCheck>(
        "postgres",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"],
        timeout: TimeSpan.FromSeconds(5))
    .AddCheck<WorkerRedisHealthCheck>(
        "redis",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"],
        timeout: TimeSpan.FromSeconds(5))
    .AddCheck<WorkerMinioHealthCheck>(
        "minio",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"],
        timeout: TimeSpan.FromSeconds(5));
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<WorkerReadinessMarker>();
builder.Services.AddSingleton<IHealthCheckPublisher, WorkerReadinessPublisher>();
builder.Services.Configure<HealthCheckPublisherOptions>(options =>
{
    options.Delay = TimeSpan.FromSeconds(5);
    options.Period = TimeSpan.FromSeconds(10);
    options.Timeout = TimeSpan.FromSeconds(8);
    options.Predicate = registration => registration.Tags.Contains("ready");
});

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? builder.Configuration["ConnectionStrings:Default"]
    ?? "Host=localhost;Port=5432;Database=vokasia;Username=vokasia;Password=vokasia_dev";

// AC VOK-H3-E1 §1: Hangfire + storage Postgres (paket sudah direferensi sejak scaffold H1,
// belum pernah benar2 didaftarkan sampai ticket ini — greenfield, dikonfirmasi via investigasi
// sebelum implementasi, bukan asumsi).
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString)));
builder.Services.AddHangfireServer();

builder.Services.AddScoped<JournalCronJobs>();
builder.Services.AddScoped<AssessmentCronJobs>(); // VOK-H5-E1 §3: OpenAssessmentPhase
builder.Services.AddScoped<LearningRecordReminderJobs>();
builder.Services.AddScoped<BillingCronJobs>(); // VOK-H6-E1 §5: GenerateMonthlyInvoices

// VOK-H4-E1 §1/§2: MassTransit+RabbitMQ HANYA di Worker (bukan Api - lihat doc-comment
// VokasiaMassTransit.cs). Consumer didaftar di sini (Worker) via callback - Infrastructure tak bisa
// reference tipe Consumer yang hidup di assembly Worker.
builder.Services.AddVokasiaMassTransit(builder.Configuration, x =>
{
    x.AddConsumer<JournalSubmittedConsumer>();
    x.AddConsumer<StreakCounterConsumer>();
    x.AddConsumer<PhotoUploadedConsumer>();
    x.AddConsumer<JournalApprovedConsumer>();
    x.AddConsumer<JournalRejectedConsumer>();
    x.AddConsumer<MentorInvitedConsumer>();
    x.AddConsumer<PlacementCreatedConsumer>();
    // VOK-H4-E3 §2: dua consumer BARU utk event yg SUDAH ditulis outbox sejak H4-E1 tapi belum
    // pernah punya consumer (lihat doc-comment masing-masing event, OutboxEventContracts.cs).
    x.AddConsumer<JournalReminderEmailConsumer>();
    x.AddConsumer<LearningAssessmentReminderEmailConsumer>();
    x.AddConsumer<GhostingAlertEmailConsumer>();
    // VOK-H5-E1 §4: export rekap nilai async.
    x.AddConsumer<ExportRequestedConsumer>();
    // VOK-H5-E1 §5: generate sertifikat PDF.
    x.AddConsumer<CertificateGeneratorConsumer>();
    // VOK-H6-E1 §1: email TenantAdminInvite (wizard CreateTenant).
    x.AddConsumer<TenantAdminInvitedConsumer>();
    // VOK-H6-E1 §5: email InvoiceIssued (GenerateMonthlyInvoices).
    x.AddConsumer<InvoiceIssuedConsumer>();
});

// VOK-H4-E1 §1: OutboxDispatcher (poll 2 dtk, publish OutboxMessage unpublished ke RabbitMQ) - satu2nya
// yang benar2 mempublish; endpoint Api hanya menulis baris OutboxMessage (EF Core biasa, sudah ada
// sejak H2-E1/H2-E3/H3-E1), tak pernah sentuh MassTransit langsung.
builder.Services.AddHostedService<OutboxDispatcher>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

if (args is ["--generate-next-e2e-invoices"])
{
    if (!builder.Environment.IsDevelopment() ||
        !builder.Configuration.GetValue<bool>("E2E_FIXTURES_ENABLED"))
    {
        throw new InvalidOperationException(
            "Fixture invoice E2E hanya boleh dibuat di Development dengan E2E_FIXTURES_ENABLED=true.");
    }

    await using var scope = host.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
    var latestPeriod = await db.Invoices
        .Select(invoice => (DateOnly?)invoice.PeriodMonth)
        .MaxAsync();
    var nextPeriod = (latestPeriod ?? new DateOnly(
        DateTime.UtcNow.Year,
        DateTime.UtcNow.Month,
        1)).AddMonths(1);

    await scope.ServiceProvider
        .GetRequiredService<BillingCronJobs>()
        .GenerateMonthlyInvoices(nextPeriod);
    return;
}

var readinessMarker = host.Services.GetRequiredService<WorkerReadinessMarker>();
readinessMarker.Clear();
host.Services.GetRequiredService<IHostApplicationLifetime>()
    .ApplicationStopping.Register(readinessMarker.Clear);

// GAP ditemukan+ditambal Gate M0 redeploy (DECISIONS.md D24): static RecurringJob.AddOrUpdate
// GAGAL runtime nyata ("Current JobStorage instance has not been initialized yet") di host Worker
// Service murni (Microsoft.NET.Sdk.Worker, BUKAN ASP.NET Core) — asumsi awal keliru bahwa
// AddHangfire() di atas men-set JobStorage.Current SECARA SINKRON saat registrasi; ternyata TIDAK
// (pesan exception sendiri eksplisit menyarankan solusi: pakai API berbasis service/DI, BUKAN API
// statis). Diperbaiki: resolve IRecurringJobManager dari container SETELAH host.Build(), panggil
// method instance-nya — dikonfirmasi lewat crash nyata container Worker (`docker logs`, exit 139),
// bukan diasumsikan dari baca dokumentasi Hangfire semata.
var recurringJobs = host.Services.GetRequiredService<IRecurringJobManager>();

recurringJobs.AddOrUpdate<JournalCronJobs>(
    "generate-daily-journal-slots",
    job => job.GenerateDailyJournalSlots(null),
    "0 5 * * *",
    new RecurringJobOptions { TimeZone = JournalCronJobs.JakartaTimeZone });

recurringJobs.AddOrUpdate<JournalCronJobs>(
    "remind-empty-journals",
    job => job.RemindEmptyJournals(),
    "0 19 * * *",
    new RecurringJobOptions { TimeZone = JournalCronJobs.JakartaTimeZone });

// VOK-H4-E1 §3: 21:00 WIB, setelah RemindEmptyJournals (19:00) beri siswa kesempatan isi sampai
// malam sebelum ditandai ghosting.
recurringJobs.AddOrUpdate<JournalCronJobs>(
    "flag-ghosting-students",
    job => job.FlagGhostingStudents(),
    "0 21 * * *",
    new RecurringJobOptions { TimeZone = JournalCronJobs.JakartaTimeZone });

// VOK-H5-E1 §3: 06:00 WIB, sebelum jam kerja sekolah/DUDI mulai.
recurringJobs.AddOrUpdate<AssessmentCronJobs>(
    "open-assessment-phase",
    job => job.OpenAssessmentPhase(null),
    "0 6 * * *",
    new RecurringJobOptions { TimeZone = JournalCronJobs.JakartaTimeZone });

recurringJobs.AddOrUpdate<LearningRecordReminderJobs>(
    "enqueue-learning-record-reminders",
    job => job.EnqueueMentorReminders(null),
    "0 8 * * *",
    new RecurringJobOptions { TimeZone = JournalCronJobs.JakartaTimeZone });

// VOK-H5-E1 §5: 06:30 WIB, SETELAH open-assessment-phase (06:00) - urutan tak saling bergantung
// datanya, tapi konsisten "cron pagi PKL" dijadwalkan berurutan.
recurringJobs.AddOrUpdate<AssessmentCronJobs>(
    "enqueue-certificate-batch",
    job => job.EnqueueCertificateBatch(null),
    "30 6 * * *",
    new RecurringJobOptions { TimeZone = JournalCronJobs.JakartaTimeZone });

// VOK-H6-E1 §5: cron tgl 1 tiap bulan, 02:00 WIB (jam sepi, sebelum jam kerja).
recurringJobs.AddOrUpdate<BillingCronJobs>(
    "generate-monthly-invoices",
    job => job.GenerateMonthlyInvoices(null),
    "0 2 1 * *",
    new RecurringJobOptions { TimeZone = JournalCronJobs.JakartaTimeZone });

host.Run();
