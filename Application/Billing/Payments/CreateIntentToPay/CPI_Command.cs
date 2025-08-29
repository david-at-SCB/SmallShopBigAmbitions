namespace SmallShopBigAmbitions.Application.Billing.Payments.CreateIntentToPay;

using SmallShopBigAmbitions.FunctionalDispatcher;

public sealed record CreateIntentToPayCommand(
    Guid CartId,
    PaymentMethod Method,
    string Currency,              // e.g., "SEK"
    string? IdempotencyKey,       // optional, from client
    string? ShippingAddress,      // you can expand as needed
    Map<string, string> Metadata   // any client metadata
) : IFunctionalRequest<IntentToPayDto>;