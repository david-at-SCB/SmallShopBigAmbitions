using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.Monads.TraceableTransformer;
using SmallShopBigAmbitions.TracingSources;

namespace SmallShopBigAmbitions.Business.Services;

public class BillingService
{
    private readonly ILogger<BillingService> _logger;

    public BillingService(ILogger<BillingService> logger)
    {
        _logger = logger;
    }

    public TraceableT<Fin<ChargeResult>> ChargeCustomer(Guid cartId, Guid userId)
    {
        return new TraceableT<Fin<ChargeResult>>(
            Effect: IO.lift(() =>
            {
                Thread.Sleep(1200); // Simulate billing delay

                var transactionId = Guid.NewGuid();
                var receiptId = Guid.NewGuid();

                var res = new ChargeResult(
                    Message: Option<string>.Some("Charge successful"),
                    Cart: cartId,
                    User: userId,
                    Transaction: transactionId,
                    Receipt: receiptId
                );
                // TODO: Replace with actual billing logic, OR:
                // TODO: Make None path as well, to simulate a failure case.
                return Fin<ChargeResult>.Succ(res);
            }).Map(Fin<ChargeResult>.Succ),
            SpanName: "BillingService.ChargeCustomer",
            Attributes: fin =>
            {
                return fin.Match(
                    Succ: r => new[]
                    {
                        new KeyValuePair<string, object>("billing.success", true),
                        new KeyValuePair<string, object>("billing.message", r.Message),
                        new KeyValuePair<string, object>("cart.id", r.Cart),
                        new KeyValuePair<string, object>("user.id", r.User),
                        new KeyValuePair<string, object>("transaction.id", r.Transaction),
                        new KeyValuePair<string, object>("receipt.id", r.Receipt)
                    },
                    Fail: err => new[]
                    {
                        new KeyValuePair<string, object>("billing.success", false),
                        new KeyValuePair<string, object>("billing.error", err.Message)
                    }
                );
            }
        ).WithLogging(_logger);
    }
}