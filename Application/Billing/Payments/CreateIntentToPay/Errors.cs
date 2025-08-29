using LanguageExt;

namespace SmallShopBigAmbitions.Application.Billing.Payments.CreatePaymentIntent;

public static class PaymentErrors
{
    public static readonly LanguageExt.Common.Error CartNotFound = Error.New("cart.not_found", new Exception("Cart does not exist"));
    public static readonly Error CartEmpty = Error.New("cart.empty", new Exception("Cart is empty"));
    public static readonly Error MethodNotSupported = Error.New("payment.method_unsupported", new Exception("Payment method not supported"));
    public static readonly Error InventoryUnavailable = Error.New("inventory.unavailable", new Exception("Some items are out of stock"));
    public static readonly Error PricingFailed = Error.New("pricing.failed", new Exception("Pricing calculation failed"));
    public static readonly Error ProviderUnavailable = Error.New("provider.unavailable", new Exception("Payment provider cannot be resolved"));
    public static readonly Error ProviderFailed = Error.New("provider.failed", new Exception("Payment provider failed to create intent"));
    public static readonly Error IdempotentConflict = Error.New("idempotency.conflict", new Exception("An intent already exists for this key"));
}