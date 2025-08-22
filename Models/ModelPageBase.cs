namespace SmallShopBigAmbitions.Models;

using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Monads.TraceableTransformer;

public abstract class TraceablePageModelBase(IFunctionalDispatcher dispatcher, ActivitySource activitySource) : PageModel
{
    protected readonly IFunctionalDispatcher Dispatcher = dispatcher;
    protected readonly ActivitySource ActivitySource = activitySource;

    /// <summary>
    /// Dispatch a functional request with tracing.
    /// </summary>
    protected TraceableT<Fin<TResponse>> DispatchWithTracing<TRequest, TResponse>(
        TRequest request,
        string spanName,
        CancellationToken ct = default
    ) where TRequest : IFunctionalRequest<TResponse>
    {
        var dispatchIO = Dispatcher.Dispatch<TResponse>(request, ct);
        return TraceableTLifts.FromIO<Fin<TResponse>>(dispatchIO, spanName);
    }

    /// <summary>
    /// Lift a value into a traceable request span.
    /// </summary>
    protected TraceableT<T> TraceRequest<T>(T value, string spanName) =>
        TraceableTLifts.FromIO<T>(IO.lift(() => value), spanName);
}
