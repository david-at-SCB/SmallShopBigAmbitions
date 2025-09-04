namespace SmallShopBigAmbitions.Application.Billing.Payments.CreatePaymentIntent.PaymentProviders;
using LanguageExt;
using SmallShopBigAmbitions.Application._Abstractions;
using static LanguageExt.Prelude;



public sealed class StripePaymentProvider : IPaymentProvider
{
    public string Name => "stripe";

    public IO<Fin<ProviderIntent>> CreateIntent(ProviderIntentRequest req) =>
        // Wrap the real Stripe SDK call and map response
        // Ensure you do not leak exceptions: return Fin<ProviderIntent>
        // Add WithSpan/Attributes in the Handler around this call (already shown)
        IO.lift<Fin<ProviderIntent>>(FinSucc(
            new ProviderIntent(
                Provider: Name,
                ProviderIntentId: Guid.NewGuid().ToString("N"),
                ClientSecret: "cs_test_...",
                Amount: req.Amount,
                ProviderMetadata: Map<string, string>()
            )
        ));
}