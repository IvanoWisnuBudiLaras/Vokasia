namespace Vokasia.Tests.Integration;

/// <summary>
/// VOK-H5-E3 §2 — tunggu efek ASYNC (outbox -&gt; publish -&gt; consumer -&gt; tulis DB) secara
/// DETERMINISTIK: poll `assert` tiap `interval` sampai TIDAK throw (dianggap sukses) atau `timeout`
/// habis (lempar exception assert TERAKHIR apa adanya, bukan pesan generik "timeout" — supaya pesan
/// gagal tetap informatif). SENGAJA bukan `Thread.Sleep` tetap (flaky: kadang kelamaan/kadang
/// kurang) — pola sama semangatnya dgn AsyncTestFixture (H4-E3) tapi diekstrak jadi util bersama
/// krn dipakai LEBIH dari satu suite Integration/ (Journal, Certificate, MagicLink dst.).
/// </summary>
public static class PollUntil
{
    public static async Task<T> SucceedsAsync<T>(Func<Task<T>> assert, TimeSpan? timeout = null, TimeSpan? interval = null)
    {
        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(10);
        var effectiveInterval = interval ?? TimeSpan.FromMilliseconds(200);
        var deadline = DateTime.UtcNow + effectiveTimeout;
        Exception? lastFailure = null;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                return await assert();
            }
            catch (Exception ex)
            {
                lastFailure = ex;
                await Task.Delay(effectiveInterval);
            }
        }

        throw lastFailure ?? new TimeoutException($"PollUntil: timeout {effectiveTimeout} habis tanpa sekali pun assert dicoba.");
    }

    public static Task SucceedsAsync(Func<Task> assert, TimeSpan? timeout = null, TimeSpan? interval = null) =>
        SucceedsAsync(async () => { await assert(); return true; }, timeout, interval);
}
