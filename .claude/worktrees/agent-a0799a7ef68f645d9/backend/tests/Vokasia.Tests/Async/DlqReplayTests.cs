using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;

namespace Vokasia.Tests.Async;

/// <summary>
/// VOK-H4-E3 §1 DlqReplayTests — "replay dari DLQ ke queue asal; dipakai runbook & panel health SA".
/// Mekanisme replay di sini SAMA PERSIS dgn yang dipakai <c>tools/Replay-Dlq.ps1</c> (AMQP murni:
/// BasicGet dari `_error`, BasicPublish body+properties APA ADANYA ke exchange default dgn routing
/// key = nama queue ASAL, lalu Ack pesan lama di `_error`) - BUKAN memanggil ulang
/// IPublishEndpoint.Publish() .NET (skrip PowerShell operasional TAK BISA melakukan itu, hanya
/// bicara AMQP/HTTP API - test ini SENGAJA meniru batasan yang SAMA supaya benar2 membuktikan
/// mekanisme yang akan dipakai operator, bukan jalan pintas yang cuma ada di test).
///
/// Sebelum replay, <see cref="PoisonTestConsumer.ShouldThrowFor"/> utk Id pesan itu diset FALSE
/// (mensimulasikan "penyebab kegagalan sudah diperbaiki") - membuktikan replay BENAR2 berujung
/// pesan diproses SUKSES, bukan cuma "berhasil dipindah balik ke antrean asal" tanpa bukti ia bisa
/// diproses (kalau tetap true, hasilnya cuma poison lagi - lihat PoisonMessageTests, itu skenario
/// beda yg tak menguji replay sama sekali).
/// </summary>
[Collection("AsyncTests")]
public class DlqReplayTests(AsyncTestFixture fixture)
{
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
    public async Task ReplayFromDeadLetterQueue_AfterFixingCause_MessageIsProcessedSuccessfully()
    {
        var poisonId = Guid.NewGuid();
        PoisonTestConsumer.ShouldThrowFor[poisonId] = true;

        var poisonTest = new PoisonMessageTests(fixture);
        using (var pubScope = fixture.FastPoison.CreateScope())
        {
            await pubScope.ServiceProvider.GetRequiredService<MassTransit.IPublishEndpoint>().Publish(new PoisonTestEvent { Id = poisonId });
        }

        var landedInDlq = await poisonTest.WaitForMessageInQueueAsync(PoisonMessageTests.PoisonDlqName, poisonId, TimeSpan.FromSeconds(60));
        Assert.True(landedInDlq, "Prasyarat gagal: pesan belum sampai DLQ, replay tak bisa diuji.");

        // "Bug sudah diperbaiki" - dari sini, pesan yg SAMA seharusnya sukses diproses kalau di-replay.
        PoisonTestConsumer.ShouldThrowFor[poisonId] = false;

        var moved = await MoveOneMessageAsync(PoisonMessageTests.PoisonDlqName, PoisonMessageTests.PoisonQueueName, poisonId);
        Assert.True(moved, $"Pesan {poisonId} diharapkan ditemukan+dipindah dari {PoisonMessageTests.PoisonDlqName} ke {PoisonMessageTests.PoisonQueueName}.");

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        while (DateTime.UtcNow < deadline && !PoisonTestConsumer.Processed.Contains(poisonId))
        {
            await Task.Delay(100);
        }

        Assert.Contains(poisonId, PoisonTestConsumer.Processed); // replay -> BENAR2 sukses diproses, bukan cuma "dipindah".
    }

    /// <summary>AMQP murni: BasicGet dari <paramref name="fromQueue"/>, cari body yg mengandung
    /// <paramref name="expectedId"/>, BasicPublish body+properties APA ADANYA ke
    /// <paramref name="toQueue"/> (exchange default, routing key = nama queue), BasicAck pesan lama
    /// di <paramref name="fromQueue"/>. Sama persis logika tools/Replay-Dlq.ps1.
    ///
    /// [PERBAIKAN - sama root cause dgn WaitForMessageInQueueAsync di PoisonMessageTests.cs, lihat
    /// doc-comment lengkap di sana]: versi lama BasicGet SATU pesan lalu langsung nack(requeue:true)
    /// kalau tak cocok, per iterasi - kalau ada pesan LAIN (bukan target) nyangkut di kepala antrean
    /// (mis. dari test lain yg berbagi DLQ literal yg sama & cuma "mengintip" tanpa konsumsi
    /// sungguhan), method ini akan TERUS mendapat pesan yg SAMA itu lagi tiap iterasi & TAK PERNAH
    /// maju ke pesan target yg sebenarnya menunggu di belakangnya. Fix: SATU sweep = BasicGet
    /// berulang (tanpa nack di antaranya, supaya BasicGet maju ke pesan berikutnya) sampai ketemu
    /// target ATAU antrean habis (dibatasi 200/sweep) - pesan bukan-target di-nack(requeue:true) di
    /// AKHIR sweep (dikembalikan persis spt semula), sedangkan pesan TARGET (kalau ketemu) langsung
    /// di-publish+ack dlm sweep yg sama.
    /// </summary>
    private async Task<bool> MoveOneMessageAsync(string fromQueue, string toQueue, Guid expectedId)
    {
        using var connection = CreateConnection();
        using var channel = await connection.CreateChannelAsync();

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            var notMatched = new List<ulong>();
            ulong? matchTag = null;
            byte[]? matchBody = null;
            IReadOnlyBasicProperties? matchProps = null;

            for (var i = 0; i < 200; i++)
            {
                var result = await channel.BasicGetAsync(fromQueue, autoAck: false);
                if (result is null) break; // antrean kosong utk sweep ini.

                var bodyBytes = result.Body.ToArray();
                var bodyText = System.Text.Encoding.UTF8.GetString(bodyBytes);
                if (matchTag is null && bodyText.Contains(expectedId.ToString()))
                {
                    matchTag = result.DeliveryTag;
                    matchBody = bodyBytes;
                    matchProps = result.BasicProperties;
                    break; // sudah ketemu target - sisa pesan (kalau ada) tak perlu di-drain, biarkan di antrean apa adanya.
                }
                notMatched.Add(result.DeliveryTag);
            }

            foreach (var tag in notMatched)
            {
                await channel.BasicNackAsync(tag, false, requeue: true); // kembalikan yg bukan target, persis spt semula.
            }

            if (matchTag is not null)
            {
                var mutableProps = new BasicProperties(matchProps!);
                await channel.BasicPublishAsync(exchange: "", routingKey: toQueue, mandatory: false, basicProperties: mutableProps, body: matchBody);
                await channel.BasicAckAsync(matchTag.Value, false);
                return true;
            }

            await Task.Delay(200);
        }
        return false;
    }
}
