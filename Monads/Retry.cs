using LanguageExt;
using static LanguageExt.Prelude;

namespace SmallShopBigAmbitions.Monads;

public static class RetryMonadAsync
{
    public static Aff<T> WithRetry<T>(Aff<T> aff, int maxRetries, TimeSpan? delay = null) =>
         Aff<T>(async () =>
         {
             async Task<T> Retry(int retriesLeft)
             {
                 var result = await aff.Run();

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

             return await Retry(maxRetries);
         });
}