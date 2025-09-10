using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace MonadPlayground
{
    /*
     * LanguageExt v5 IO examples
     * --------------------------
     * This file provides composable helpers over IO<T> (v5 unified effect type):
     *   - WithRetry   : Retry transient failures (exceptions) with backoff + predicate
     *   - Cached      : Cache successful results (do not cache failures)
     *   - WithMetrics : Time an effect and report success / failure
     *
     * All helpers are implemented purely in terms of IO<T> without introducing async/await.
     * IO<T>.Run() executes the effect synchronously; the scheduler integration is handled by
     * LanguageExt internally. If you need asynchronous work, wrap Task calls in IO.lift(() => task.Result)
     * or provide dedicated async abstractions elsewhere – for these examples we stay sync-focused.
     */

    #region Retry
    public static class IORetryExtensions
    {
        public static IO<A> WithRetry<A>(
            this IO<A> effect,
            int maxRetries,
            Func<int, TimeSpan>? backoff = null,
            Func<Error, bool>? retryOn = null)
        {
            if (maxRetries < 0) throw new ArgumentOutOfRangeException(nameof(maxRetries));
            backoff ??= _ => TimeSpan.Zero;
            retryOn ??= _ => true;

            IO<A> Attempt(int attempt) => IO.lift(() =>
            {
                try
                {
                    return effect.Run();
                }
                catch (Exception ex)
                {
                    var err = Error.New(ex);
                    if (attempt >= maxRetries || !retryOn(err))
                        throw; // give up

                    var delay = backoff(attempt);
                    if (delay > TimeSpan.Zero)
                        Thread.Sleep(delay);

                    return Attempt(attempt + 1).Run();
                }
            });

            return Attempt(0);
        }
    }
    #endregion

    #region Cache
    public static class IOCacheExtensions
    {
        private static class CacheState<A>
        {
            public static readonly ConcurrentDictionary<string, Lazy<A>> Entries = new();
        }

        // Cache only successful results (i.e. values). Exceptions are NOT cached.
        public static IO<A> Cached<A>(this IO<A> effect, string key)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));

            return IO.lift(() =>
            {
                var dict = CacheState<A>.Entries;
                if (dict.TryGetValue(key, out var existing))
                    return existing.Value; // May rethrow if previously faulted Lazy (we evict on failure below)

                var created = new Lazy<A>(() => effect.Run(), LazyThreadSafetyMode.ExecutionAndPublication);
                var lazy = dict.GetOrAdd(key, created);

                try
                {
                    return lazy.Value;
                }
                catch
                {
                    // If our newly created lazy failed, remove it so a future attempt can retry
                    if (ReferenceEquals(lazy, created))
                        dict.TryRemove(key, out _);
                    throw;
                }
            });
        }
    }
    #endregion

    #region Metrics
    public static class IOMetricsExtensions
    {
        public static IO<A> WithMetrics<A>(
            this IO<A> effect,
            string name,
            Action<string, long, bool>? sink = null)
        {
            if (name is null) throw new ArgumentNullException(nameof(name));
            sink ??= (n, ms, success) => Console.WriteLine($"[Metrics] {n} {(success ? "succeeded" : "failed")} in {ms}ms");

            return IO.lift(() =>
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    var value = effect.Run();
                    sw.Stop();
                    sink(name, sw.ElapsedMilliseconds, true);
                    return value;
                }
                catch
                {
                    sw.Stop();
                    sink(name, sw.ElapsedMilliseconds, false);
                    throw;
                }
            });
        }
    }
    #endregion

    #region Helpers
    public static class TimeSpanNumericExtensions
    {
        public static TimeSpan ms(this int v) => TimeSpan.FromMilliseconds(v);
        public static TimeSpan Seconds(this int v) => TimeSpan.FromSeconds(v);
    }

    public static class IOFin
    {
        // Convert IO<A> to IO<Fin<A>> capturing exceptions into Error
        public static IO<Fin<A>> ToFin<A>(this IO<A> effect) => IO.lift<Fin<A>>(() =>
        {
            try { return FinSucc(effect.Run()); }
            catch (Exception ex) { return FinFail<A>(Error.New(ex)); }
        });
    }
    #endregion

    #region ExampleEffects
    public static class ExampleEffects
    {
        // Simulate flaky side-effect that throws transiently a number of times
        public static IO<string> GetFlakyCustomer(int failTimes, Func<int> counter) => IO.lift(() =>
        {
            var attempt = counter();
            Console.WriteLine($"Flaky attempt #{attempt}");
            if (attempt <= failTimes)
                throw new InvalidOperationException("Transient upstream failure");
            return "RecoveredCustomer987";
        });
    }
    #endregion

    /*
     * NOTE: Deliberately no Main() here to avoid multiple entry-point conflicts with the web host.
     * Example composition (for reference):
     *
     * int attempts = 0;
     * Func<int> next = () => Interlocked.Increment(ref attempts);
     * var effect = ExampleEffects
     *      .GetFlakyCustomer(failTimes: 2, counter: next)
     *      .WithRetry(maxRetries: 5, backoff: i => (50 * (i + 1)).ms(), retryOn: e => e.Message.Contains("Transient"))
     *      .Cached("customer:current")
     *      .WithMetrics("GetCustomer");
     * var fin = effect.ToFin().Run();
     * Console.WriteLine(fin.Match(Succ: v => $"Result: {v}", Fail: err => $"Failed: {err.Message}"));
     */
}
