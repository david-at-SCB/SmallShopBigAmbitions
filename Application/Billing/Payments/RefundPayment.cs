namespace SmallShopBigAmbitions.Application.Billing.Payments;

using LanguageExt;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Application._Policy;
using SmallShopBigAmbitions.Monads.TraceableTransformer;
using static LanguageExt.Prelude;

public enum RefundReason { CustomerRequest, FraudSuspected, Duplicate, Other }

public sealed record RefundPaymentCommand(Guid PaymentIntentId, decimal Amount, RefundReason Reason)
    : IFunctionalRequest<Unit>;

public sealed class RefundPaymentPolicy : IAuthorizationPolicy<RefundPaymentCommand>
{
    public Fin<Unit> Authorize(RefundPaymentCommand request, TrustedContext context)
        => context.IsAuthenticated ? FinSucc(Unit.Default) : FinFail<Unit>(Error.New("Unauthorized"));
}

public interface IPaymentRefundService
{
    IO<Fin<Unit>> Refund(Guid paymentIntentId, decimal amount, RefundReason reason);
}

public sealed class RefundPaymentHandler(
    IPaymentRefundService refunds,
    ILogger<RefundPaymentHandler> logger
) : IFunctionalHandler<RefundPaymentCommand, Unit>
{
    private readonly IPaymentRefundService _refunds = refunds;
    private readonly ILogger _logger = logger;

    public IO<Fin<Unit>> Handle(RefundPaymentCommand request, TrustedContext context, CancellationToken ct)
    {
        var flow = TraceableTLifts
            .FromIOFinRawTracableT(_refunds.Refund(request.PaymentIntentId, request.Amount, request.Reason), "payment.refund")
            .WithLogging(_logger);

        return flow.RunTraceable(ct);
    }
}
