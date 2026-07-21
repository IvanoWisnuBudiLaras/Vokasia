using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vokasia.Infrastructure;
using Vokasia.Worker;
using Vokasia.Worker.Jobs;

var builder = Host.CreateApplicationBuilder(args);

// DbContext (Npgsql, fallback conn string sama dgn Vokasia.Api) + Redis + MinIO client — satu
// titik registrasi dipakai bersama, lihat Vokasia.Infrastructure/DependencyInjection.cs. Worker
// TIDAK punya AmbientTenantContext yang di-set (tak ada HTTP request) -> query filter tenant di
// VokasiaDbContext otomatis "mati", cron LINTAS SEMUA TENANT by design (lihat doc-comment
// JournalCronJobs).
builder.Services.AddVokasiaInfrastructure(builder.Configuration);

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

host.Run();
