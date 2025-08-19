using SmallShopBigAmbitions.Monads.TraceableTransformer;
using System.Diagnostics;

namespace SmallShopBigAmbitions.Business.Services;

public class BillingService
{
    private static readonly ActivitySource ActivitySource = new("MyShop.BillingService");

    public static TraceableT<ChargeResult> ChargeCustomer(Guid cartId, Guid userId, ILogger logger)
    {
        return new TraceableT<ChargeResult>(
            Effect: IO.lift(() =>
            {
                Thread.Sleep(120);
                var transactionId = Guid.NewGuid();
                var receiptId = Guid.NewGuid();

                return new ChargeResult
                {
                    Success = true,
                    Message = "Charge successful",
                    Cart = cartId,
                    User = userId,
                    Transaction = transactionId,
                    Receipt = receiptId
                };
            }),
            SpanName: "BillingService.ChargeCustomer",
            Attributes: result => new[]
            {
                new KeyValuePair<string, object>("billing.success", result.Success),
                new KeyValuePair<string, object>("billing.message", result.Message),
                new KeyValuePair<string, object>("cart.id", result.Cart),
                new KeyValuePair<string, object>("user.id", result.User),
                new KeyValuePair<string, object>("transaction.id", result.Transaction),
                new KeyValuePair<string, object>("receipt.id", result.Receipt)
            }
        ).WithLogging(logger);
    }

    public record ChargeResult
    {
        public Fin<bool> Success { get; init; }
        public Option<string> Message { get; init; } = string.Empty;
        public Guid Cart { init; get; }
        public Guid User { init; get; }
        public Guid Transaction { init; get; }
        public Guid Receipt { init; get; }
    }
}