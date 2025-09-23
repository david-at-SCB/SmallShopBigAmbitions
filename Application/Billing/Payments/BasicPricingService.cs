using LanguageExt;
using SmallShopBigAmbitions.Application._Abstractions;
using SmallShopBigAmbitions.Models;

namespace SmallShopBigAmbitions.Application.Billing.Payments;

public sealed class BasicPricingService : IPricingService
{
    public IO<Fin<Money>> CalculateDiscounts(CartSnapshot cart) => IO.lift<Fin<Money>>(() => Fin<Money>.Succ(new Money("SEK", 0)));
    public IO<Fin<Money>> CalculateShipping(CartSnapshot cart) => IO.lift<Fin<Money>>(() => Fin<Money>.Succ(new Money("SEK", 50)));
    public IO<Fin<Money>> CalculateTaxes(CartSnapshot cart, Money totalBeforeTax) => IO.lift<Fin<Money>>(() => Fin<Money>.Succ(new Money(totalBeforeTax.Currency, totalBeforeTax.Amount * 0.25m)));
}
