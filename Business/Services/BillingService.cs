// Async/IO checklist (Razor Pages + LanguageExt)
// - Handlers return Task/Task<IActionResult>; always await RunTraceable(ct).RunAsync().
// - Avoid .Run(), .Result, .Wait() on IO/Aff in web code.
// - Accept and pass CancellationToken to all effects/HTTP calls.
// - Services: expose composable TraceableT/IO; add Task wrappers (e.g., GetXAsync) for UI.
// - Use Fin/Option for errors; handle via Match in handlers and return proper IActionResult.
// - Map DTO -> domain in a mapper; keep IO layer DTO-focused.
// - Ensure DI registers FunctionalHttpClient and service; no static singletons.
// - Add .WithLogging and telemetry attributes where useful.
using SmallShopBigAmbitions.Application.Billing.CheckoutUser;
using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.Monads.TraceableTransformer;
using LanguageExt;

namespace SmallShopBigAmbitions.Business.Services;

public class BillingService
{
    private readonly ILogger<BillingService> _logger;

    public BillingService(ILogger<BillingService> logger)
    {
        _logger = logger;
    }

    // Return a traceable billing attempt result
    public TraceableT<Fin<ChargeResult>> ChargeCustomer(Cart cart)
    {
        return new TraceableT<Fin<ChargeResult>>(
            Effect: IO.lift<Fin<ChargeResult>>(() =>
            {
                Thread.Sleep(1200); // Simulate billing delay

                var transactionId = Guid.NewGuid();
                var receiptId = Guid.NewGuid();

                var res = new ChargeResult(
                    Message: Prelude.Some("Charge successful"),
                    Cart: cart.Id,
                    Customer: cart.CustomerId,
                    Transaction: transactionId,
                    Receipt: receiptId
                );

                return Fin<ChargeResult>.Succ(res);
            }),
            SpanName: "BillingService.ChargeCustomer",
            Attributes: fin =>
            {
                return fin.Match(
                    Succ: r =>
                    [
                        new KeyValuePair<string, object>("billing.success", true),
                        new KeyValuePair<string, object>("billing.message", r.Message),
                        new KeyValuePair<string, object>("cart.id", r.Cart),
                        new KeyValuePair<string, object>("user.id", r.Customer),
                        new KeyValuePair<string, object>("transaction.id", r.Transaction),
                        new KeyValuePair<string, object>("receipt.id", r.Receipt)
                    ],
                    Fail: err => new[]
                    {
                        new KeyValuePair<string, object>("billing.success", false),
                        new KeyValuePair<string, object>("billing.error", err.Message)
                    }
                );
            }
        ).WithLogging(_logger);
    }

    public TraceableT<CheckoutUserResultDTO> CheckoutCustomerCart(Cart cart)
    {
        // TODO: implement real logic
        var chargedResult = new ChargeResult(
            Message: Prelude.Some("Charge successful"),
            Cart: cart.Id,
            Customer: cart.CustomerId,
            Transaction: Guid.NewGuid(),
            Receipt: Guid.NewGuid()
        );

        var dto = TraceableTLifts.FromValue(
            new CheckoutUserResultDTO(
                CustomerId: cart.CustomerId,
                Cart: cart,
                Message: "Checkout completed successfully.",
                Charged: Fin<ChargeResult>.Succ(chargedResult)
            ),
            spanName: "CustomerCharged");

        return dto;
    }
}