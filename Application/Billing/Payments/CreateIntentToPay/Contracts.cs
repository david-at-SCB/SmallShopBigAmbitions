using LanguageExt;
using SmallShopBigAmbitions.Application.Billing.Payments.CreatePaymentIntent;
using static LanguageExt.Prelude;

namespace SmallShopBigAmbitions.Application.Billing.Payments.CreateIntentToPay;

public enum PaymentMethod
{
    Card,
    Swish,
    PayPal
    // ...
}

public sealed record Money(string Currency, decimal Amount)
{
    public static Money GetFromFin(Fin<Money> money)
    {
        return money.Match(
            Succ: m => m,
            Fail: _ => new Money("SEK", 0) // Default value in case of failure
        );
    }
}

public sealed record CartItem(Guid ProductId, int Quantity, Money UnitPrice);

public sealed record CartSnapshot(Guid CartId, Guid UserId, Seq<CartItem> Items, Money Subtotal, string Country, string Region);

public interface ICartQueries
{
    IO<Fin<CartSnapshot>> GetCart(Guid cartId);
}

public interface IInventoryService
{
    IO<Fin<Unit>> EnsureAvailable(Seq<CartItem> items);
    IO<Fin<Unit>> Reserve(Seq<CartItem> items, Guid reservationId, TimeSpan ttl);
    IO<Fin<Unit>> Release(Guid reservationId);
}

public interface IPricingService
{
    IO<Fin<Money>> CalculateShipping(CartSnapshot cart);
    IO<Fin<Money>> CalculateDiscounts(CartSnapshot cart);
    IO<Fin<Money>> CalculateTaxes(CartSnapshot cart, Money totalBeforeTax);
}

public interface ICurrencyService
{
    IO<Fin<Money>> Convert(Money from, string toCurrency);
}

public interface IPaymentProvider
{
    string Name { get; }

    // Creates a provider-specific intent (e.g., Stripe PaymentIntent)
    IO<Fin<ProviderIntent>> CreateIntent(ProviderIntentRequest req);

    // Optional: confirm/cancel/expire hooks as needed
}

public sealed record ProviderIntent(string Provider, string ProviderIntentId, string ClientSecret, Money Amount, Map<string, string> ProviderMetadata);

public sealed record ProviderIntentRequest(string Description, Money Amount, Guid PaymentIntentId, Guid CartId, Guid UserId, PaymentMethod Method, Map<string, string> Metadata);

public interface IPaymentProviderSelector
{
    Fin<IPaymentProvider> Resolve(PaymentMethod method);
}

public interface IPaymentIntentRepository
{
    IO<Fin<PaymentIntent>> Insert(PaymentIntent intent);
    IO<Fin<Option<PaymentIntent>>> GetById(Guid id);
    IO<Fin<Unit>> Update(PaymentIntent intent);
    IO<Fin<Option<PaymentIntent>>> GetIdempotent(string idempotencyKey);
    IO<Fin<Unit>> SaveIdempotency(string idempotencyKey, Guid paymentIntentId);
}

public interface IIdempotencyStore
{
    IO<Fin<Option<Guid>>> TryGet(string key);
    IO<Fin<Unit>> Put(string key, Guid intentId);
}
