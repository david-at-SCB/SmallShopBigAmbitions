//using SmallShopBigAmbitions.Auth;
//using SmallShopBigAmbitions.FunctionalDispatcher;
//using SmallShopBigAmbitions.Monads;

//namespace SmallShopBigAmbitions.Application._PipelineBehaviours;

// replaced with ObservabilityBehavior
//public class LoggingBehavior<TRequest, TResponse> : IFunctionalPipelineBehavior<TRequest, TResponse>
//    where TRequest : IFunctionalRequest<TResponse>
//{
//    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

//    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
//    {
//        _logger = logger;
//    }

//    public IO<Fin<TResponse>> Handle(
//            TRequest request,
//            TrustedContext context,
//            Func<TRequest, TrustedContext, CancellationToken, IO<Fin<TResponse>>> next,
//            CancellationToken ct)
//        =>
//        next(request, context, ct)
//        .TapSucc(_ => _logger.LogInformation(
//            "Handled {RequestType} for {CallerId}",
//            typeof(TRequest).Name, context.CallerId))
//        .TapFail(err => _logger.LogError(err.ToException(),
//            "Failed {RequestType} for {CallerId}: {Error}",
//            typeof(TRequest).Name, context.CallerId, err.Message));
//}