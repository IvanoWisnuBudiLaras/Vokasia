using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using Vokasia.Infrastructure.Messaging;

namespace Vokasia.Tests.Async;

/// <summary>
/// VOK-H4-E3 §1 PoisonMessageTests — "payload rusak/consumer throw permanen -> retry sesuai policy
/// -> masuk `_error` queue (DLQ); message lain tetap mengalir". Pakai bus FastPoison (retry
/// dipercepat, BUKAN MessagingDefaults produksi - lihat doc-comment AsyncTestFixture utk alasan).
///
/// Nama queue DLQ = nama endpoint consumer + <see cref="MessagingDefaults.DeadLetterQueueSuffix"/>
/// ("_error") - KONVENSI BAWAAN MassTransit/RabbitMQ, bukan sesuatu yang kami konfigurasi (lihat
/// doc-comment MessagingDefaults). Nama endpoint sendiri diambil via
/// <see cref="KebabCaseEndpointNameFormatter"/> (formatter DEFAULT MassTransit, dipakai
/// AddVokasiaMassTransit/ConfigureEndpoints() tanpa override) - bukan ditebak manual, supaya tak
/// rapuh kalau versi MassTransit mengubah aturan format nama.
/// </summary>
[Collection("AsyncTests")]
public class PoisonMessageTests(AsyncTestFixture fixture)
{
    internal static string PoisonQueueName => AsyncTestFixture.PoisonQueueName;
    internal static string PoisonDlqName => MessagingDefaults.DeadLetterQueueNameFor(PoisonQueueName);

    private IConnection CreateConnection()
    {
        var factory = new ConnectionFactory
        {
            HostName = fixture.RabbitMqHost,
            Port = fixture.RabbitMqPort,
            UserName = "guest",
            Password = "guest",
        };
        return factory.CreateConnectionAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task PermanentThrow_RetryExhausted_MessageLandsInDeadLetterQueue()
    {
        var poisonId = Guid.NewGuid();
        PoisonTestConsumer.ShouldThrowFor[poisonId] = true;

        using (var pubScope = fixture.FastPoison.CreateScope())
        {
            await pubScope.ServiceProvider.GetRequiredService<IPublishEndpoint>().Publish(new PoisonTestEvent { Id = poisonId });
        }

        // Retry FastPoison habis jauh di bawah 5 dtk secara teori, tetapi round-trip jaringan
        // Testcontainers nyata (bukan in-memory) tetap diberi margin.
        var found = await WaitForMessageInQueueAsync(PoisonDlqName, poisonId, TimeSpan.FromSeconds(40));

        Assert.True(found, $"Pesan {poisonId} diharapkan berakhir di DLQ '{PoisonDlqName}' setelah retry habis, tapi tak ditemukan.");
        Assert.DoesNotContain(poisonId, PoisonTestConsumer.Processed); // TAK PERNAH sukses diproses (poison permanen).
    }

    [Fact]
    public async Task HealthyMessage_PublishedAlongsidePoisonOnes_StillFlowsNormally()
    {
        // AC: "message lain tetap mengalir" - pesan SEHAT (ShouldThrowFor tak di-set = false) harus
        // tetap sukses diproses walau ada pesan poison lain melintas di consumer/queue yang SAMA.
        var healthyId = Guid.NewGuid();
        using (var pubScope = fixture.FastPoison.CreateScope())
        {
            await pubScope.ServiceProvider.GetRequiredService<IPublishEndpoint>().Publish(new PoisonTestEvent { Id = healthyId });
        }

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (DateTime.UtcNow < deadline && !PoisonTestConsumer.Processed.Contains(healthyId))
        {
            await Task.Delay(100);
        }

        Assert.Contains(healthyId, PoisonTestConsumer.Processed);
    }

    /// <summary>
    /// [PERBAIKAN #1, ditemukan lewat kegagalan nyata]: percobaan awal memakai SATU channel dipakai
    /// ulang lintas iterasi loop - AMQP protocol error (BasicGet ke queue yang BELUM ADA, mis. DLQ blm
    /// pernah terisi sama sekali) MENUTUP channel-nya di sisi SERVER; channel yang sudah mati itu terus
    /// dipakai ulang iterasi berikutnya, membuat loop TERLIHAT "coba lagi" padahal sebenarnya tak pernah
    /// benar2 mengecek lagi. Fix: channel BARU tiap iterasi (connection tetap satu, lihat di bawah).
    ///
    /// [PERBAIKAN #2, ROOT CAUSE gagalnya DlqReplayTests yang sebenarnya - ditemukan SETELAH #1
    /// terbukti tak cukup bahkan di timeout 60 dtk]: versi lama nack(requeue:true) LANGSUNG stlh
    /// SATU BasicGet, tiap iterasi. Kalau DLQ berbagi nama yg SAMA (PoisonDlqName, literal tetap)
    /// dgn test LAIN yang sudah lebih dulu naruh pesan di sana (PermanentThrow_... hanya
    /// mengintip via BasicGet+Nack(requeue:true) - TAK PERNAH benar2 mengonsumsi pesannya, jadi
    /// pesan lama itu TETAP ada di DLQ selamanya), pesan LAMA itu kembali ke DEKAT KEPALA antrean
    /// stlh di-requeue - sweep BERIKUTNYA (test lain, cari ID BARU) SELALU dapat pesan LAMA itu
    /// lagi di posisi pertama, tak pernah cocok, di-nack-requeue lagi, dan TAK PERNAH maju ke
    /// pesan BARU yang menunggu di belakangnya - loop bisa nunggu berapa lamapun, tetap gagal
    /// (PERSIS gejala nyata: gagal identik di timeout 20/40/60 dtk, bukan soal kurang waktu).
    /// Fix: SATU sweep = BasicGet BERULANG (tanpa nack di antaranya - pesan yg sudah diambil tapi
    /// blm di-ack dianggap "in flight" oleh RabbitMQ, BasicGet berikutnya otomatis maju ke pesan
    /// SELANJUTNYA, bukan mengulang yg sama) sampai antrean kosong (dibatasi 200 pesan/sweep utk
    /// jaga2), BARU nack(requeue:true) SEMUANYA di akhir sweep - mengembalikan tiap pesan PERSIS
    /// spt semula (tak ada yg hilang/berubah urutan permanen), sambil tetap bisa MENEMUKAN target
    /// yang ada di posisi manapun dlm antrean pada sweep itu.
    /// </summary>
    internal async Task<bool> WaitForMessageInQueueAsync(string queueName, Guid expectedId, TimeSpan timeout)
    {
        using var connection = CreateConnection(); // SATU connection lintas iterasi (channel baru tiap iterasi/sweep, lihat #1 di atas).

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var channel = await connection.CreateChannelAsync();
                var seen = new List<ulong>();
                var found = false;
                for (var i = 0; i < 200; i++)
                {
                    var result = await channel.BasicGetAsync(queueName, autoAck: false);
                    if (result is null) break; // antrean sudah kosong utk sweep ini.
                    seen.Add(result.DeliveryTag);
                    var bodyText = System.Text.Encoding.UTF8.GetString(result.Body.ToArray());
                    if (bodyText.Contains(expectedId.ToString()))
                    {
                        found = true; // tetap lanjut drain+nack semua di bawah - jangan return dulu, supaya tak ada pesan lain yg nyangkut ter-ack sebagian.
                    }
                }
                foreach (var tag in seen)
                {
                    await channel.BasicNackAsync(tag, false, requeue: true); // kembalikan semua - jangan konsumsi, cuma mengintip.
                }
                if (found) return true;
            }
            catch (global::RabbitMQ.Client.Exceptions.OperationInterruptedException)
            {
                // Queue belum ada (belum ada pesan yang pernah masuk DLQ ini sama sekali) - normal di awal, coba lagi.
            }
            await Task.Delay(300);
        }
        return false;
    }
}
