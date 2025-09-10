using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Monads.TraceableTransformer;
using System.Reflection;

namespace SmallShopBigAmbitions.FunctionalDispatcher;

/// <summary>
/// Interface for functional requests. Queries or commands must implement this.
/// </summary>
/// <typeparam name="TResponse"></typeparam>
public interface IFunctionalRequest<TResponse>
{ }

/// <summary>
/// Interface for functional handlers.
/// Handlers process requests and return a result wrapped in IO. They must implement this interface.
/// </summary>
/// <typeparam name="TRequest"></typeparam>
/// <typeparam name="TResponse"></typeparam>
/// <typeparam name="TrustedContext"</typeparam>
public interface IFunctionalHandler<TRequest, TResponse>
    where TRequest : IFunctionalRequest<TResponse>
{
    IO<Fin<TResponse>> Handle(TRequest request, TrustedContext context, CancellationToken ct);
}

/// <summary>
/// Inject behaviour into the functional pipeline.
/// We can use this to add cross-cutting concerns like logging, validation, etc.
/// </summary>
/// <typeparam name="TRequest"></typeparam>
/// <typeparam name="TResponse"></typeparam>
public interface IFunctionalPipelineBehavior<TRequest, TResponse>
    where TRequest : IFunctionalRequest<TResponse>
{
    IO<Fin<TResponse>> Handle(
        TRequest request,
        TrustedContext context,
        Func<TRequest, TrustedContext, CancellationToken, IO<Fin<TResponse>>> next,
        CancellationToken ct
    );
}

public interface IFunctionalDispatcher
{
    [Obsolete("Use the strongly-typed generic Dispatch<TRequest, TResponse> method instead.")]
    IO<Fin<TResponse>> Dispatch<TResponse>(IFunctionalRequest<TResponse> request, CancellationToken ct);
    IO<Fin<TResponse>> Dispatch<TRequest, TResponse>(TRequest request, CancellationToken ct)
        where TRequest : IFunctionalRequest<TResponse>;
}

public class FunctionalDispatcher : IFunctionalDispatcher
{
    private readonly IServiceProvider _provider;

    public FunctionalDispatcher(IServiceProvider provider) => _provider = provider;

    // Existing (runtime-type) entry point kept for backwards compatibility
    [Obsolete("Use the strongly-typed generic Dispatch<TRequest, TResponse> method instead.")]
    public IO<Fin<TResponse>> Dispatch<TResponse>(IFunctionalRequest<TResponse> request, CancellationToken ct)
    {
        var context = _provider.GetService<TrustedContext>() ?? new TrustedContext(); // should we maybe short-cicuit if no context?
        var requestType = request.GetType();

        // Use reflection to call the strongly typed generic overload
        var method = typeof(FunctionalDispatcher)
            .GetMethod(nameof(Dispatch), BindingFlags.Public | BindingFlags.Instance, new[] { requestType, typeof(CancellationToken) });

        if (method is null || !method.IsGenericMethodDefinition)
            return IO.lift<Fin<TResponse>>(() => Fin<TResponse>.Fail(Error.New("Dispatch method resolution failure")));

        var generic = method.MakeGenericMethod(requestType, typeof(TResponse));
        return (IO<Fin<TResponse>>)generic.Invoke(this, new object[] { request, ct })!;
    }

    // Preferred strongly-typed generic API
    public IO<Fin<TResponse>> Dispatch<TRequest, TResponse>(TRequest request, CancellationToken ct)
        where TRequest : IFunctionalRequest<TResponse>
    {
        var context = _provider.GetService<TrustedContext>() ?? new TrustedContext();
        var handler = _provider.GetService<IFunctionalHandler<TRequest, TResponse>>();

        if (handler is null)
            return IO.lift<Fin<TResponse>>(() => Fin<TResponse>.Fail(Error.New($"No handler found for {typeof(TRequest).Name}")));

        return BuildPipeline<TRequest, TResponse>(request, context, handler, ct);
    }

    private IO<Fin<TResponse>> BuildPipeline<TRequest, TResponse>(
        TRequest request,
        TrustedContext context,
        IFunctionalHandler<TRequest, TResponse> handler,
        CancellationToken ct
    ) where TRequest : IFunctionalRequest<TResponse>
    {
        var behaviors = _provider
            .GetServices<IFunctionalPipelineBehavior<TRequest, TResponse>>()
            .ToList();

        Func<TRequest, TrustedContext, CancellationToken, IO<Fin<TResponse>>> next =
            (req, ctx, token) => handler.Handle(req, ctx, token);

        // Wrap in reverse so first registered runs outermost (same semantics as original)
        foreach (var behavior in behaviors.AsEnumerable().Reverse())
        {
            var inner = next;
            next = (req, ctx, token) => behavior.Handle(req, ctx, inner, token);
        }

        return next(request, context, ct);
    }

    public static TraceableT<Fin<TResponse>> DispatchWithTracing<TRequest, TResponse>(
        IFunctionalDispatcher dispatcher,
        TRequest request,
        string spanName,
        CancellationToken ct
    ) where TRequest : IFunctionalRequest<TResponse>
    {
        var dispatchIO = dispatcher.Dispatch<TRequest, TResponse>(request, ct);
        return TraceableTLifts.FromIO<Fin<TResponse>>(dispatchIO, spanName);
    }
}
