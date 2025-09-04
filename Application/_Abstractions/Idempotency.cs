namespace SmallShopBigAmbitions.Application._Abstractions;

public enum IdempotencyState
{
    Acquired,            // You own the slot; run the effect and Complete() later
    DuplicateSameDone,   // Same fingerprint, cached result available
    DuplicateSameBusy,   // Same fingerprint, currently Processing
    DuplicateDifferent   // Same key reused, but payload differs
}

public sealed record IdemLookup<T>(
    IdempotencyState State,
    T? Response = default // present only for DuplicateSameDone
);

public interface IIdempotencyStore
{
    IO<Fin<IdemLookup<string>>> TryAcquire(
        string scope,
        string key,
        string fingerprint,
        TimeSpan ttl,
        CancellationToken ct = default
    );

    IO<Fin<Unit>> Complete<T>(
        string scope,
        string key,
        T response,                 // will be serialized to JSON
        CancellationToken ct = default
    );

    IO<Fin<Unit>> Abandon(
        string scope,
        string key,
        CancellationToken ct = default
    );
}