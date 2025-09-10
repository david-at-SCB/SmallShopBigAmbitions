namespace SmallShopBigAmbitions.Application.Billing.Payments;

using LanguageExt;
using LanguageExt.Common;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Application._Policy;
using SmallShopBigAmbitions.Monads.TraceableTransformer;
using static LanguageExt.Prelude;

public enum RefundReason { CustomerRequest, FraudSuspected, Duplicate, Other }

public sealed record RefundPaymentCommand(Guid PaymentIntentId, decimal Amount, RefundReason Reason)
    : IFunctionalRequest<Unit>;

public static class RefundPaymentValidator
{
    public static Validation<Seq<Error>, Unit> Validate(RefundPaymentCommand cmd, TrustedContext ctx) =>
        RuleCombiner.Apply(
            Rule.From("auth", () => ctx.IsAuthenticated, ErrorCodes.Auth_Unauthorized),
            Rule.From("amt_pos", () => cmd.Amount > 0m, ErrorCodes.Payment_Refund_AmountNonPositive)
        );
}

public sealed class RefundPaymentPolicy : IAuthorizationPolicy<RefundPaymentCommand>
{
    public Fin<Unit> Authorize(RefundPaymentCommand request, TrustedContext context) =>
        RefundPaymentValidator.Validate(request, context).ToFin();
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
            .FromIOFin(_refunds.Refund(request.PaymentIntentId, request.Amount, request.Reason), "payment.refund")
            .WithLogging(_logger);

        return flow.RunTraceable(ct);
    }
}
