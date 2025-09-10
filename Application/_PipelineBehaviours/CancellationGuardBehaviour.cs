using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.FunctionalDispatcher;

namespace SmallShopBigAmbitions.Application._PipelineBehaviours;

/// <summary>
/// Early-cancellation pipeline behaviour.  If the token is already cancelled it fails fast without invoking inner behaviours or handler.
/// If cancellation occurs during execution, OperationCanceledException will surface from inner calls unless translated by another behaviour.
/// </summary>
/// <typeparam name="TRequest"></typeparam>
/// <typeparam name="TResponse"></typeparam>
public sealed class CancellationGuardBehaviour<TRequest, TResponse> : IFunctionalPipelineBehavior<TRequest, TResponse>
    where TRequest : IFunctionalRequest<TResponse>
{
    public IO<Fin<TResponse>> Handle(
        TRequest request,
        TrustedContext context,
        Func<TRequest, TrustedContext, CancellationToken, IO<Fin<TResponse>>> next,
        CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
        {
            return IO.lift<Fin<TResponse>>(() => Fin<TResponse>.Fail(Error.New("OperationCanceled")));
        }

        // Let downstream components observe cancellation naturally
        return IO.liftAsync(async () => await next(request, context, ct).RunAsync());
    }
}
