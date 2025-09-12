namespace SmallShopBigAmbitions.Application.Billing.Payments;

using LanguageExt;
using SmallShopBigAmbitions.Application.Billing.Payments.CreateIntentToPay;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Application._Policy;
using SmallShopBigAmbitions.Monads.TraceableTransformer;
using static LanguageExt.Prelude;
using SmallShopBigAmbitions.Application._PipelineBehaviours;
using SmallShopBigAmbitions.Application._Abstractions;

public sealed record AuthorizePaymentCommand(Guid OrderId, Guid CartId, PaymentMethod Method, string Currency, string? IdempotencyKey)
    : IFunctionalRequest<IntentToPayDto>, IIdempotentRequest
{
    public string IdempotencyScope => "payment.authorize";
}

public sealed class AuthorizePaymentPolicy : IAuthorizationPolicy<AuthorizePaymentCommand>
{
    public Fin<Unit> Authorize(AuthorizePaymentCommand request, TrustedContext context)
        => context.IsAuthenticated ? FinSucc(Unit.Default) : FinFail<Unit>(Error.New("Unauthorized"));
}

public sealed class AuthorizePaymentHandler(
    IFunctionalDispatcher dispatcher,
    ILogger<AuthorizePaymentHandler> logger
) : IFunctionalHandler<AuthorizePaymentCommand, IntentToPayDto>
{
    private readonly IFunctionalDispatcher _dispatcher = dispatcher;
    private readonly ILogger _logger = logger;

    public IO<Fin<IntentToPayDto>> Handle(AuthorizePaymentCommand request, TrustedContext context, CancellationToken ct)
    {
        // Reuse CreateIntentToPayHandler via dispatcher to create a provider intent
        var cmd = new IntentToPayCommand(
            CartId: request.CartId,
            Method: request.Method,
            Currency: request.Currency,
            IdempotencyKey: request.IdempotencyKey,
            ShippingAddress: null,
            Metadata: Map<string, string>());

        var flow = TraceableTLifts
            .FromIOFin(_dispatcher.Dispatch<IntentToPayCommand, IntentToPayDto>(cmd, ct), "payment.authorize");

        return flow.RunTraceable(ct);
    }
}
