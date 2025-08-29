namespace SmallShopBigAmbitions.Application.Billing.Payments.CreatePaymentIntent.Observability;

public static class ActivityNames
{
    public const string PaymentCreateIntent = "payments.intent.create";
    public const string PricingCalculate = "payments.pricing.calculate";
    public const string InventoryReserve = "inventory.reserve";
    public const string ProviderCreateIntent = "payments.provider.create_intent";
    public const string PersistPaymentIntent = "payments.intent.persist";
}

public static class Attr
{
    public const string CartId = "cart.id";
    public const string UserId = "user.id";
    public const string ItemsCount = "cart.items_count";
    public const string Amount = "payment.amount";
    public const string Currency = "payment.currency";
    public const string Provider = "payment.provider";
    public const string Method = "payment.method";
    public const string ReservationId = "inventory.reservation_id";
    public const string Idempotency = "idempotency.key";
    public const string Country = "cart.country";
    public const string Region = "cart.region";
    public const string Error = "error.message";
}