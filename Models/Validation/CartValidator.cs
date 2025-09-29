using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;
using SmallShopBigAmbitions.Models;

namespace SmallShopBigAmbitions.Models.Validation;

/// <summary>
/// Declarative cart validator using LanguageExt.Validation to allow error accumulation.
/// </summary>
public static class CartValidator
{
    public static Validation<Seq<Error>, Unit> NonEmpty(Cart cart) =>
        cart.Items.IsEmpty
            ? Fail<Seq<Error>, Unit>(Prelude.Seq(Error.New("Cart is empty")))
            : Success<Seq<Error>, Unit>(unit);

    public static Validation<Seq<Error>, Unit> AllQuantitiesPositive(Cart cart) =>
        cart.Items.Values.All(l => l.Quantity > 0)
            ? Success<Seq<Error>, Unit>(unit)
            : Fail<Seq<Error>, Unit>(Prelude.Seq(Error.New("Invalid quantity for one or more items")));

    public static Validation<Seq<Error>, Unit> AllPricesPositive(Cart cart) =>
        cart.Items.Values.All(l => l.UnitPrice.Amount > 0)
            ? Success<Seq<Error>, Unit>(unit)
            : Fail<Seq<Error>, Unit>(Prelude.Seq(Error.New("Invalid price for one or more items")));

    public static Validation<Seq<Error>, Unit> TotalPositive(Cart cart) =>
        cart.Items.Values.Sum(l => l.UnitPrice.Amount * l.Quantity) > 0m
            ? Success<Seq<Error>, Unit>(unit)
            : Fail<Seq<Error>, Unit>(Prelude.Seq(Error.New("Total cart amount must be greater than zero")));

    public static Validation<Seq<Error>, Unit> HasCurrency(Cart cart) =>
        cart.Currency.IsSome
            ? Success<Seq<Error>, Unit>(unit)
            : Fail<Seq<Error>, Unit>(Prelude.Seq(Error.New("Cart currency is not set")));

    /// <summary>
    /// Aggregate all validations and accumulate all failures using Applicative Apply.
    /// </summary>
    public static Validation<Seq<Error>, Unit> ValidateForCharge(Cart cart) =>
        Success<Seq<Error>, System.Func<Unit, System.Func<Unit, System.Func<Unit, System.Func<Unit, System.Func<Unit, Unit>>>>>>(_ => __ => ___ => ____ => _____ => unit)
            .Apply(HasCurrency(cart))
            .Apply(NonEmpty(cart))
            .Apply(AllQuantitiesPositive(cart))
            .Apply(AllPricesPositive(cart))
            .Apply(TotalPositive(cart));
}
