//using SmallShopBigAmbitions.TracingSources;
//using SmallShopBigAmbitions.FunctionalDispatcher;
//using System.Diagnostics;
//using SmallShopBigAmbitions.Auth;

//namespace SmallShopBigAmbitions.Application._PipelineBehaviours;

// replaced with ObservabilityBehavior
//public class TracingBehavior<TRequest, TResponse> : IFunctionalPipelineBehavior<TRequest, TResponse>
//    where TRequest : IFunctionalRequest<TResponse>
//{
//    public IO<Fin<TResponse>> Handle(
//        TRequest request,
//        TrustedContext context,
//        Func<TRequest, TrustedContext, CancellationToken, IO<Fin<TResponse>>> next,
//        CancellationToken ct)
//    {
//        // Make this effect truly async to avoid blocking. Also record error/success attributes.
//        return IO.liftAsync(async () =>
//        {
//            ct.ThrowIfCancellationRequested();
//            using var activity = ShopActivitySource.Instance.StartActivity(typeof(TRequest).Name, ActivityKind.Internal);

//            var result = await next(request, context, ct).RunAsync();

//            if (result.IsFail)
//            {
//                // Use Option<string> to avoid returning null
//                var errOpt = result.Match(
//                    Succ: _ => Option<string>.None,
//                    Fail: e => Option<string>.Some(e.Message)
//                );
//                errOpt.IfSome(err => activity?.SetTag("error", err));
//            }
//            else
//            {
//                var valOpt = result.Match(
//                    Succ: v => v is not null ? Option<string>.Some(v.ToString()!) : Option<string>.None,
//                    Fail: _ => Option<string>.None
//                );
//                valOpt.IfSome(val => activity?.SetTag("result", val));
//            }

//            return result;
//        });
//    }
//}
