namespace SmallShopBigAmbitions.Application.Billing.Payments.CreateIntentToPay;

using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Application._PipelineBehaviours;
using SmallShopBigAmbitions.Application._Abstractions;

public sealed record 
IntentToPayCommand(
    Guid CartId,
    PaymentMethod Method,
    string Currency,              // e.g., "SEK"
    string? IdempotencyKey,       // optional, from client
    string? ShippingAddress,      // you can expand as needed
    Map<string, string> Metadata   // any client metadata
) : IFunctionalRequest<IntentToPayDto>, IIdempotentRequest
{
    public string IdempotencyScope => "payment_intent";
}