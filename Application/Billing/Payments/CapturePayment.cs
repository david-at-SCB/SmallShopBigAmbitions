namespace SmallShopBigAmbitions.Application.Billing.Payments;

using LanguageExt;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Application._Policy;
using SmallShopBigAmbitions.Monads.TraceableTransformer;
using static LanguageExt.Prelude;

public sealed record CapturePaymentCommand(Guid PaymentIntentId) : IFunctionalRequest<Unit>;

public sealed class CapturePaymentPolicy : IAuthorizationPolicy<CapturePaymentCommand>
{
    public Fin<Unit> Authorize(CapturePaymentCommand request, TrustedContext context)
        => context.IsAuthenticated ? FinSucc(Unit.Default) : FinFail<Unit>(Error.New("Unauthorized"));
}

public interface IPaymentCaptureService
{
    IO<Fin<Unit>> Capture(Guid paymentIntentId);
}

public sealed class CapturePaymentHandler(
    IPaymentCaptureService capture,
    ILogger<CapturePaymentHandler> logger
) : IFunctionalHandler<CapturePaymentCommand, Unit>
{
    private readonly IPaymentCaptureService _capture = capture;

    public IO<Fin<Unit>> Handle(CapturePaymentCommand request, TrustedContext context, CancellationToken ct)
    {
        var flow = TraceableTLifts
            .FromIOFin(_capture.Capture(request.PaymentIntentId), "payment.capture");

        return flow.RunTraceable(ct);
    }
}
