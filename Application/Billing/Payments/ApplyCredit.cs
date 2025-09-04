namespace SmallShopBigAmbitions.Application.Billing.Payments;

using LanguageExt;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Application._Policy;
using SmallShopBigAmbitions.Monads.TraceableTransformer;
using static LanguageExt.Prelude;

public sealed record ApplyCreditCommand(Guid OrderId, string Code) : IFunctionalRequest<Unit>;

public sealed class ApplyCreditPolicy : IAuthorizationPolicy<ApplyCreditCommand>
{
    public Fin<Unit> Authorize(ApplyCreditCommand request, TrustedContext context)
        => context.IsAuthenticated ? FinSucc(Unit.Default) : FinFail<Unit>(Error.New("Unauthorized"));
}

public interface ICreditService
{
    IO<Fin<Unit>> Apply(Guid orderId, string code);
}

public sealed class ApplyCreditHandler(
    ICreditService credits,
    ILogger<ApplyCreditHandler> logger
) : IFunctionalHandler<ApplyCreditCommand, Unit>
{
    private readonly ICreditService _credits = credits;
    private readonly ILogger _logger = logger;

    public IO<Fin<Unit>> Handle(ApplyCreditCommand request, TrustedContext context, CancellationToken ct)
    {
        var flow = TraceableTLifts
            .FromIOFinRawTracableT(_credits.Apply(request.OrderId, request.Code), "order.apply_credit")
            .WithLogging(_logger);

        return flow.RunTraceable(ct);
    }
}
