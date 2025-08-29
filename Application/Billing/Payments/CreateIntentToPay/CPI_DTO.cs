namespace SmallShopBigAmbitions.Application.Billing.Payments.CreateIntentToPay;

public sealed record IntentToPayDto(
    Guid PaymentIntentId,
    string Provider,
    string ProviderIntentId,
    string ClientSecret,
    string Currency,
    decimal Amount,
    string Status,                         // mirrors domain status
    Map<string, string> ProviderMetadata
);