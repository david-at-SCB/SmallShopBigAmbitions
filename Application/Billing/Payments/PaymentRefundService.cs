using LanguageExt;
using SmallShopBigAmbitions.Application.Billing.Payments;
using SmallShopBigAmbitions.Application.Billing.Payments.CreatePaymentIntent.Repo;
using SmallShopBigAmbitions.Application.Billing.Payments.CreatePaymentIntent;
using static LanguageExt.Prelude;

namespace SmallShopBigAmbitions.Application.Billing.Payments;

public sealed class PaymentRefundService(IPaymentIntentRepository repo, ILogger<PaymentRefundService> logger) : IPaymentRefundService
{
    private readonly IPaymentIntentRepository _repo = repo;
    private readonly ILogger _logger = logger;

    public IO<Fin<Unit>> Refund(Guid paymentIntentId, decimal amount, RefundReason reason)
    {
        return IO.lift<Fin<Unit>>(() =>
        {
            var intentFin = _repo.GetById(paymentIntentId).RunTraceable().Run();
            return intentFin.Bind(opt => opt.Match(
                Some: intent =>
                {
                    if (intent.Status != PaymentIntentStatus.Succeeded)
                        return Fin<Unit>.Fail(Error.New("Only succeeded intents can be refunded"));
                    if (amount <= 0 || amount > intent.Amount)
                        return Fin<Unit>.Fail(Error.New("Invalid refund amount"));
                    // For demo just log; a real impl would persist refund record & adjust status if full refund.
                    _logger.LogInformation("Refunding {Amount} {Currency} for intent {IntentId} reason {Reason}", amount, intent.Currency, intent.Id, reason);
                    return Fin<Unit>.Succ(unit);
                },
                None: () => Fin<Unit>.Fail(Error.New("PaymentIntent not found"))
            ));
        });
    }
}
