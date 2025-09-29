namespace SmallShopBigAmbitions.Application.Billing.Payments.CreateIntentToPay;

using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Application._PipelineBehaviours;
using SmallShopBigAmbitions.Application._Abstractions;
using SmallShopBigAmbitions.Models;

public sealed record IntentToPayCommand(
    Guid CartId,
    CustomerId CustomerId,
    PaymentMethod Method,
    string? IdempotencyKey,
    Money Amount,              // e.g., "SEK"
    string? ShippingAddress,      // you can expand as needed
    Map<string, string> Metadata   // any client metadata
) : IFunctionalRequest<IntentToPayDto>, IIdempotentRequest
{
    public string IdempotencyScope => "payment_intent";
    public Guid IntentId = new();
}