namespace SmallShopBigAmbitions.FunctionalDispatcher;
public interface IFunctionalRequest<TResponse> { }


public interface IFunctionalHandler<TRequest, TResponse>
    where TRequest : IFunctionalRequest<TResponse>
{
    IO<Fin<TResponse>> Handle(TRequest request, CancellationToken ct);
}


public interface IFunctionalPipelineBehavior<TRequest, TResponse>
    where TRequest : IFunctionalRequest<TResponse>
{
    IO<Fin<TResponse>> Handle(
        TRequest request,
        CancellationToken ct,
        Func<TRequest, CancellationToken, IO<Fin<TResponse>>> next
    );
}


public class FunctionalDispatcher
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
            return IO(Fin<TResponse>.Fail(Error.New($"No handler found for {requestType.Name}")));

        var pipeline = BuildPipeline(request, ct, handlerType, handler);
        return pipeline;
    }

    private IO<Fin<TResponse>> BuildPipeline<TResponse>(
        IFunctionalRequest<TResponse> request,
        CancellationToken ct,
        Type handlerType,
        object handlerInstance)
    {
        var requestType = request.GetType();
        var behaviorType = typeof(IFunctionalPipelineBehavior<,>).MakeGenericType(requestType, typeof(TResponse));
        var behaviors = _provider.GetServices(behaviorType).Cast<dynamic>().ToList();

        Func<dynamic, CancellationToken, IO<Fin<TResponse>>> finalHandler = (req, token) =>
            ((dynamic)handlerInstance).Handle((dynamic)req, token);

        // Compose behaviors in reverse order
        foreach (var behavior in behaviors.AsEnumerable().Reverse())
        {
            var next = finalHandler;
            finalHandler = (req, token) => behavior.Handle((dynamic)req, token, next);
        }

        return finalHandler((dynamic)request, ct);
    }
}
