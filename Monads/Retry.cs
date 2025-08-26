using LanguageExt;
using static LanguageExt.Prelude;

namespace SmallShopBigAmbitions.Monads;

public static class RetryIO
{
    public static IO<A> WithRetry<A>(IO<A> io, int maxRetries, TimeSpan? delay = null)
    {
        return IO.liftAsync(async () =>
        {
            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    return io.Run();
                }
                catch (Exception ex)
                {
                    if (attempt == maxRetries)
                        throw new Exception($"Retry limit reached. Last error: {ex.Message}", ex);

                    if (delay.HasValue)
                        await Task.Delay(delay.Value);
                }
            }
            throw new InvalidOperationException("Unreachable");
        });
    }
}
