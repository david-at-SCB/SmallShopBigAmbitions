using LanguageExt;
using SmallShopBigAmbitions.Application.Billing.Payments.CreatePaymentIntent;
using SmallShopBigAmbitions.Models;
using static LanguageExt.Prelude;

namespace SmallShopBigAmbitions.Application._Abstractions;

public enum PaymentMethod
{
    Card,
    Swish,
    PayPal
    // ...
}

public interface ICartQueries
{
    IO<Fin<CartSnapshot>> GetCart(Guid cartId);
}

public interface IInventoryService
{
    IO<Fin<Unit>> EnsureAvailable(Seq<CartLine> items);
    IO<Fin<Unit>> Reserve(CartSnapshot cart, Guid reservationId, TimeSpan ttl);
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




