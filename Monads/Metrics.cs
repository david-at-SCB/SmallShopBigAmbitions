using LanguageExt;
using SmallShopBigAmbitions.Monads.ReturnTypes;
using System.Diagnostics;
using static LanguageExt.Prelude;

namespace SmallShopBigAmbitions.Monads;

public static class MetricsMonadAsync
{
    public static Aff<ComputationResultWithMetrics<T>> WithMetrics<T>(Aff<T> aff, string name) =>
        Aff(async () =>
        {
            var sw = Stopwatch.StartNew();
            var result = await aff.Run();
            sw.Stop();

            // Create the ComputationResultWithMetrics based on the result
            var metrics = result.Match(
                Succ: value => new ComputationResultWithMetrics<T>(
                    Value: value,
                    IsSuccess: true,
                    Error: null,
                    ExecutionTime: sw.ElapsedMilliseconds,
                    Message: $"[Metrics] {name} succeeded in {sw.ElapsedMilliseconds}ms"
                ),
                Fail: error => new ComputationResultWithMetrics<T>(
                    Value: default,
                    IsSuccess: false,
                    Error: error.ToString(),
                    ExecutionTime: sw.ElapsedMilliseconds,
                    Message: $"[Metrics] {name} failed in {sw.ElapsedMilliseconds}ms: {error}"
                )
            );

            return metrics;
        });

    //public static Aff<T> WithRetryAndMetrics<T>(Aff<T> aff, string name, int maxRetries, TimeSpan? delay = null) =>
    //    WithMetrics(WithRetry(aff, maxRetries, delay), name);
}