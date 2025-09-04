using SmallShopBigAmbitions.Monads;
using System.Text.Json;
using LanguageExt;
using static LanguageExt.Prelude;

namespace SmallShopBigAmbitions.Application._Abstractions;

public static class IdempotencyOps
{
    /// <summary>
    /// Declarative, functional idempotency wrapper.
    /// - Attempts to acquire an idempotent slot via the store
    /// - If acquired: runs the effect, completes or abandons accordingly
    /// - If duplicate (same) and completed: returns cached response
    /// - If duplicate (same) but busy: fails with appropriate error
    /// - If duplicate (different): fails to signal misuse of the key
    /// </summary>
    public static IO<Fin<T>> WithIdempotency<T>(
        IIdempotencyStore store,
        string scope,
        string key,
        string fingerprint,
        TimeSpan ttl,
        IO<Fin<T>> effect,
        CancellationToken ct = default)
        => store
            .TryAcquire(scope, key, fingerprint, ttl, ct)
            .Bind(acqFin => acqFin.Match(
                Succ: lookup => lookup.State switch
                {
                    IdempotencyState.Acquired =>
                        // We own the slot; run the effect and persist the result
                        effect.Bind(resultFin => resultFin.Match(
                            Succ: result =>
                                store.Complete(scope, key, result, ct)
                                     .Map(compFin => compFin.Bind(_ => FinSucc(result))),
                            Fail: err =>
                                // Best-effort abandon on failure, but always return the original error
                                store.Abandon(scope, key, ct)
                                     .Map(_ => FinFail<T>(err))
                        )),

                    IdempotencyState.DuplicateSameDone =>
                        // Deserialize the cached response
                        IO.lift<Fin<T>>(() =>
                        {
                            try
                            {
                                if (lookup.Response is null)
                                    return FinFail<T>(Error.New("Cached idempotent response was null"));

                                var value = JsonSerializer.Deserialize<T>(lookup.Response);
                                return value is not null
                                    ? FinSucc(value)
                                    : FinFail<T>(Error.New("Cached idempotent response deserialized to null"));
                            }
                            catch (Exception ex)
                            {
                                return FinFail<T>(Error.New(ex));
                            }
                        }),

                    IdempotencyState.DuplicateSameBusy =>
                        IOFin.Fail<T>(Error.New("Operation already in progress for this idempotency key")),

                    IdempotencyState.DuplicateDifferent =>
                        IOFin.Fail<T>(Error.New("Idempotency key reused with different payload (fingerprint mismatch)")),

                    _ => IOFin.Fail<T>(Error.New("Unknown idempotency state"))
                },
                Fail: e => IOFin.Fail<T>(e)
            ));
}