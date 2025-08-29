namespace SmallShopBigAmbitions.Application.Billing.Payments.CreatePaymentIntent;

using LanguageExt;
using SmallShopBigAmbitions.Application.Billing.Payments.CreateIntentToPay;
using static LanguageExt.Prelude;

public sealed class PaymentProviderSelector : IPaymentProviderSelector
{
    private readonly Map<PaymentMethod, IPaymentProvider> _map;
    public PaymentProviderSelector(IEnumerable<IPaymentProvider> providers)
    {
        // register providers by supported method(s)
        _map = providers.Aggregate(Map<PaymentMethod, IPaymentProvider>(), (acc, p) =>
            // e.g. Stripe supports Card:
            p.Name == "stripe" ? acc.Add(PaymentMethod.Card, p) : acc);
    }

    public Fin<IPaymentProvider> Resolve(PaymentMethod method) =>
        _map.Find(method).ToFin(PaymentErrors.MethodNotSupported);
}
