using LanguageExt;
using static LanguageExt.Prelude;

namespace SmallShopBigAmbitions.Monads;

public static class RetryMonadAsync
{
    public static IO<T> WithRetry<T>(IO<T> io, int maxRetries, TimeSpan? delay = null)
    {
        async Task<T> Retry(int retriesLeft)
        {
            var result = await Lift<T>(io);

            return await result.Match(
                Succ: val => Task.FromResult(val),
                Fail: async err =>
                {
                    if (retriesLeft <= 0)
                        throw new Exception($"Retry limit reached. Last error: {err}");

                    if (delay.HasValue)
                        await Task.Delay(delay.Value);

                    return await Retry(retriesLeft - 1);
                });
        }

        return IO<T>.LiftAsync(() => Retry(maxRetries));
    }
}