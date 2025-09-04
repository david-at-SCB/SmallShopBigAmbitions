using SmallShopBigAmbitions.Application._Abstractions;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Database.Idempotency;
using SmallShopBigAmbitions.FunctionalDispatcher;
using System.Text.Json;

namespace SmallShopBigAmbitions.Application._PipelineBehaviours;

public interface IIdempotentRequest
{
    string? IdempotencyKey { get; }
    string IdempotencyScope { get; }
}

public interface IHasCorrelationId
{
    Guid CorrelationId { get; }
}

public class IdempotencyBehavior<TRequest, TResponse>(IIdempotencyStore store) : IFunctionalPipelineBehavior<TRequest, TResponse>
    where TRequest : IFunctionalRequest<TResponse>, IIdempotentRequest
{
    private readonly IIdempotencyStore _store = store;

    public IO<Fin<TResponse>> Handle(
        TRequest request,
        TrustedContext context,
        Func<TRequest, TrustedContext, CancellationToken, IO<Fin<TResponse>>> next,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            return next(request, context, ct);
        }

        var scope = string.IsNullOrWhiteSpace(request.IdempotencyScope)
            ? "default"
            : request.IdempotencyScope;
        var key = request.IdempotencyKey!;

        // Use the request payload as the fingerprint to detect duplicate with different payload
        var fingerprint = JsonSerializer.Serialize(request);
        var ttl = TimeSpan.FromMinutes(10);

        return IdempotencyOps.WithIdempotency(
            _store,
            scope,
            key,
            fingerprint,
            ttl,
            next(request, context, ct),
            ct);
    }
}