using SmallShopBigAmbitions.FunctionalDispatcher;
using LanguageExt;
using SmallShopBigAmbitions.Auth;

namespace SmallShopBigAmbitions.Application._Behaviours;

public class LoggingBehavior<TRequest, TResponse> : IFunctionalPipelineBehavior<TRequest, TResponse>
    where TRequest : IFunctionalRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public IO<Fin<TResponse>> Handle(
        TRequest request,
        TrustedContext context,
        Func<TRequest, TrustedContext, CancellationToken, IO<Fin<TResponse>>> next,
        CancellationToken ct)
    {
        return IO<Fin<TResponse>>.Lift(() =>
        {
            _logger.LogInformation("Request: {RequestType}, Caller: {CallerId}", typeof(TRequest).Name, context.CallerId);
            var runNextResult = next(request, context, ct).Run();

            //var resultResult = runNextResult.Match(
            //    Succ: value => { _logger.LogInformation("Response: {Result}", value); return Unit.Default; },
            //    Fail: err => { _logger.LogError("Request failed: {Error}", err.Message); return Unit.Default; }
            //);

            //return IO<Fin<TResponse>>.Lift(resultResult);
            return runNextResult;
        });
    }
}

