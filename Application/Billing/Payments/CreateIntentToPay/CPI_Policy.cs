namespace SmallShopBigAmbitions.Application.Billing.Payments.CreateIntentToPay;

using SmallShopBigAmbitions.Models;
using LanguageExt;
using SmallShopBigAmbitions.Application.Billing.Payments.CreatePaymentIntent;
using static LanguageExt.Prelude;
using SmallShopBigAmbitions.Monads.LanguageExtExtensions; // optional: LINQ helpers for IO<Fin<>
using SmallShopBigAmbitions.Application._Abstractions;

// Policy can read/query, but must not write/commit side effects.
public sealed class CreateIntentToPayPolicy
{
    private readonly ICartQueries _carts;
    private readonly IPaymentProviderSelector _providers;
    private readonly IInventoryService _inventory;

    public CreateIntentToPayPolicy(ICartQueries carts, IPaymentProviderSelector providers, IInventoryService inventory) =>
        (_carts, _providers, _inventory) = (carts, providers, inventory);

    public IO<Fin<(CartSnapshot Cart, IPaymentProvider Provider)>> Check(IntentToPayCommand cmd) =>
        IO.lift<Fin<(CartSnapshot, IPaymentProvider)>>(() =>
        {
            var cartFin = _carts.GetCart(cmd.CartId).Run();
            return cartFin.Bind(cart =>
            {
                var nonEmptyFin = EnsureCartNotEmpty(cart).Run();
                return nonEmptyFin.Bind(_ =>
                {
                    var providerFin = _providers.Resolve(cmd.Method);
                    return providerFin.Bind((IPaymentProvider provider) =>
                    {
                        var availableFin = _inventory.EnsureAvailable(cart.Items.Values.ToSeq()).Run();
                        return availableFin.Match(
                            Succ: ( discard )=> Fin<(CartSnapshot, IPaymentProvider)>.Succ((cart, provider)),
                            Fail: e => Fin<(CartSnapshot, IPaymentProvider)>.Fail(e)
                        );
                    });
                });
            });
        });

    private static IO<Fin<Unit>> EnsureCartNotEmpty(CartSnapshot cart) =>
        cart.Items.IsEmpty
            ? IO.lift<Fin<Unit>>(() => Fin<Unit>.Fail(PaymentErrors.CartEmpty))
            : IO.lift<Fin<Unit>>(() => Fin<Unit>.Succ(unit));
}