using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Monads.TraceableTransformer;

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
    IO<Fin<TResponse>> Dispatch<TResponse>(IFunctionalRequest<TResponse> request, CancellationToken ct);
    IO<Fin<TResponse>> BuildPipeline<TResponse>(
        IFunctionalRequest<TResponse> request,
        TrustedContext context,
        object handlerType,
        object handlerInstance,
        CancellationToken ct
        );

}

/// <summary>
/// Meadiator-like dispatcher for functional requests.
/// </summary>
public class FunctionalDispatcher : IFunctionalDispatcher
{
    private readonly IServiceProvider _provider;

    public FunctionalDispatcher(IServiceProvider provider)
    {
        _provider = provider;
    }

    public IO<Fin<TResponse>> Dispatch<TResponse>(IFunctionalRequest<TResponse> request, CancellationToken ct)
    {
        var requestType = request.GetType();
        var handlerType = typeof(IFunctionalHandler<,>).MakeGenericType(requestType, typeof(TResponse));
        var handler = _provider.GetService(handlerType);

        if (handler is null)
            return IO.lift<Fin<TResponse>>(() => Fin<TResponse>.Fail(Error.New($"No handler found for {requestType.Name}")));

        var context = _provider.GetService<TrustedContext>() ?? new TrustedContext(); // fallback if not registered
        var pipeline = BuildPipeline(request, context, handler, handlerType, ct);
        return pipeline;
    }

    public IO<Fin<TResponse>> BuildPipeline<TResponse>(
        IFunctionalRequest<TResponse> request, TrustedContext context, object handlerType, object handlerInstance, CancellationToken ct)
    {
        var requestType = request.GetType();
        var behaviorType = typeof(IFunctionalPipelineBehavior<,>).MakeGenericType(requestType, typeof(TResponse));
        var behaviors = _provider.GetServices(behaviorType).Cast<dynamic>().ToList();

        Func<dynamic, TrustedContext, CancellationToken, IO<Fin<TResponse>>> finalHandler = (req, ctx, token) =>
            ((dynamic)handlerInstance).Handle((dynamic)req, ctx, token);

        foreach (var behavior in behaviors.AsEnumerable().Reverse())
        {
            var next = finalHandler;
            finalHandler = (req, ctx, token) => behavior.Handle((dynamic)req, ctx, token, next);
        }

        return finalHandler((dynamic)request, context, ct);
    }
    public static TraceableT<Fin<TResponse>> DispatchWithTracing<TRequest, TResponse>(
        IFunctionalDispatcher dispatcher,
        TRequest request,
        string spanName,
        CancellationToken ct
    ) where TRequest : IFunctionalRequest<TResponse>
    {
        var dispatchIO = dispatcher.Dispatch<TResponse>(request, ct);
        return TraceableTLifts.FromIO<Fin<TResponse>>(dispatchIO, spanName);
    }

}
