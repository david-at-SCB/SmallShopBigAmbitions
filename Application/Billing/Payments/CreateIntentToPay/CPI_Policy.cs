namespace SmallShopBigAmbitions.Application.Billing.Payments.CreateIntentToPay;

using SmallShopBigAmbitions.Models;
using LanguageExt;
using LanguageExt.Common;
using SmallShopBigAmbitions.Application.Billing.Payments.CreatePaymentIntent;
using static LanguageExt.Prelude;
using SmallShopBigAmbitions.Monads.LanguageExtExtensions; // optional: LINQ helpers for IO<Fin<>
using SmallShopBigAmbitions.Application._Abstractions;
using SmallShopBigAmbitions.Application._Policy;

public static class CreateIntentToPayValidator
{
    public static Validation<Seq<Error>, Unit> Validate(IntentToPayCommand cmd, CartSnapshot cart) =>
        RuleCombiner.Apply(
            Rule.From("method", () => cmd.Method != default, ErrorCodes.Payment_Intent_MethodRequired),
            Rule.From("cart_non_empty", () => !cart.Items.IsEmpty, ErrorCodes.Payment_Intent_CartEmpty)
        );
}

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
                var validationFin = CreateIntentToPayValidator.Validate(cmd, cart).ToFin();
                return validationFin.Bind(_ =>
                {
                    var providerFin = _providers.Resolve(cmd.Method);
                    return providerFin.Bind((IPaymentProvider provider) => // CS9236
                    {
                        var availableFin = _inventory.EnsureAvailable(cart.Items.Values.ToSeq()).Run();
                        return availableFin.Match(
                            Succ: _ => Fin<(CartSnapshot, IPaymentProvider)>.Succ((cart, provider)), // CS9236
                            Fail: e => Fin<(CartSnapshot, IPaymentProvider)>.Fail(e)); // CS9236
                    });
                });
            });
        });
}