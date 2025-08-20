using LanguageExt;
using static LanguageExt.Prelude;

namespace SmallShopBigAmbitions.Monads;

public static class RetryMonadAsync
{
    public static IO<T> WithRetry<T>(IO<T> io, int maxRetries, TimeSpan? delay = null)
    {
        async Task<T> Retry(int retriesLeft)
        {
            try
            {
                // IO<T> in v5 is sync, so run it; if you need async work, lift Task with IO.lift
                return io.Run();
            }
            catch (Exception ex)
            {
                if (retriesLeft <= 0)
                    throw new Exception($"Retry limit reached. Last error: {ex.Message}", ex);

                if (delay.HasValue)
                    await Task.Delay(delay.Value);

                return await Retry(retriesLeft - 1);
            }
        }

        // Wrap the async retry loop into an IO via Task result
        return IO.lift(() => Retry(maxRetries).GetAwaiter().GetResult());
    }
}