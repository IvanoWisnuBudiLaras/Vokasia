using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vokasia.Infrastructure;
using Vokasia.Infrastructure.Messaging;
using Vokasia.Worker;
using Vokasia.Worker.Consumers;
using Vokasia.Worker.Jobs;

var builder = Host.CreateApplicationBuilder(args);

// DbContext (Npgsql, fallback conn string sama dgn Vokasia.Api) + Redis + MinIO client — satu
// titik registrasi dipakai bersama, lihat Vokasia.Infrastructure/DependencyInjection.cs. Worker
// TIDAK punya AmbientTenantContext yang di-set (tak ada HTTP request) -> query filter tenant di
// VokasiaDbContext otomatis "mati", cron LINTAS SEMUA TENANT by design (lihat doc-comment
// JournalCronJobs).
builder.Services.AddVokasiaInfrastructure(builder.Configuration, builder.Environment);

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
    x.AddConsumer<GhostingAlertEmailConsumer>();
});

// VOK-H4-E1 §1: OutboxDispatcher (poll 2 dtk, publish OutboxMessage unpublished ke RabbitMQ) - satu2nya
// yang benar2 mempublish; endpoint Api hanya menulis baris OutboxMessage (EF Core biasa, sudah ada
// sejak H2-E1/H2-E3/H3-E1), tak pernah sentuh MassTransit langsung.
builder.Services.AddHostedService<OutboxDispatcher>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

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

host.Run();
