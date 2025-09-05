using LanguageExt;
using SmallShopBigAmbitions.Application.Billing.Payments;
using SmallShopBigAmbitions.Application.Billing.Payments.CreatePaymentIntent.Repo;
using SmallShopBigAmbitions.Application.Billing.Payments.CreatePaymentIntent;
using static LanguageExt.Prelude;

namespace SmallShopBigAmbitions.Application.Billing.Payments;

public sealed class PaymentCaptureService(IPaymentIntentRepository repo, ILogger<PaymentCaptureService> logger) : IPaymentCaptureService
{
    private readonly IPaymentIntentRepository _repo = repo;
    private readonly ILogger _logger = logger;

    public IO<Fin<Unit>> Capture(Guid paymentIntentId)
    {
        return IO.lift<Fin<Unit>>(() =>
        {
            var intentFin = _repo.GetById(paymentIntentId).RunTraceable().Run();
            return intentFin.Bind(opt => opt.Match(
                Some: intent =>
                {
                    if (intent.Status != PaymentIntentStatus.Authorized && intent.Status != PaymentIntentStatus.Pending)
                        return Fin<Unit>.Fail(Error.New("Intent not capturable in current status"));
                    var updated = intent with { Status = PaymentIntentStatus.Succeeded, UpdatedAt = DateTimeOffset.UtcNow };
                    var updFin = _repo.Update(updated).RunTraceable().Run();
                    return updFin.Map(_ => unit);
                },
                None: () => Fin<Unit>.Fail(Error.New("PaymentIntent not found"))
            ));
        });
    }
}
