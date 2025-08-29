namespace SmallShopBigAmbitions.Application.Billing.Payments.CreatePaymentIntent;

using SmallShopBigAmbitions.Application.Billing.Payments.CreateIntentToPay;

public static class Mapping
{
    public static IntentToPayDto ToDto(PaymentIntent intent, Map<string, string> providerMeta) =>
        new(
            intent.Id,
            intent.Provider,
            intent.ProviderIntentId,
            intent.ClientSecret.IfNone(string.Empty),
            intent.Currency,
            intent.Amount,
            intent.Status.ToString(),
            providerMeta
        );
}