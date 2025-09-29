using SmallShopBigAmbitions.Application._Abstractions;
using SmallShopBigAmbitions.Monads.TraceableTransformer;
using SmallShopBigAmbitions.Monads.TraceableTransformer.Extensions.BaseLinq;
using SmallShopBigAmbitions.Models.Validation;
using LanguageExt;
namespace SmallShopBigAmbitions.Models;

/// <summary>
/// TODO!
/// </summary>
/// <param name="Id"></param>
/// <param name="CustomerId"> TODO: Make this to be string(email) or Guid</param>
/// <param name="Items"></param>
/// <param name="Currency"></param>
public sealed record Cart(Guid Id, Guid CustomerId, CartItems Items, Option<string> Currency)
{
    public static Cart Empty(Guid customerId) => new(Guid.NewGuid(), customerId, CartItems.Empty, None);

    public Cart Add(ProductId id, int qty, Money price) => this with { Items = Items.Add(id, qty, price) };

    public Cart SetQuantity(ProductId id, int qty) => this with { Items = Items.SetQuantity(id, qty) };

    //public Money Total(string currency) => new(currency, Items.Values.Sum(l => l.UnitPrice.Amount * l.Quantity));

    public Money GetTotal() =>
        new(
            Currency.Match(
                Some: c => c,
                None: "SEK"
            ),
            Items.Values.Sum(l => l.UnitPrice.Amount * l.Quantity)
        );

    public string GetCartCurrency() =>
        Currency.Match(
            Some: c => c,
            None: "SEK"
        );

    public int GetAmountOfItems() => Items.Values.Sum(l => l.Quantity); 

    public bool ValidTotal() =>
        !Items.IsEmpty
        && Items.Values.All(l => l.Quantity > 0 && l.UnitPrice.Amount > 0)
        && Items.Values.Sum(l => l.UnitPrice.Amount * l.Quantity) > 0m;

    public Fin<CartSnapshot> ValidateForCharge(Guid customerId)
    {
        var validation = CartValidator.ValidateForCharge(this);
        var errors = validation.Match(
            Succ: _ => [],
            Fail: errs => errs.Map(e => e.Message).ToArray()
        );
        var isValid = errors.Length == 0;

        var snapshot = new CartSnapshot(
            this,
            new RegisteredCustomerId(customerId),
            GetTotal(),
            "Sweden",
            "Europe",
            isValid,
            errors
        );

        return isValid
            ? Fin<CartSnapshot>.Succ(snapshot)
            : Fin<CartSnapshot>.Fail(Error.New(string.Join("; ", errors)));
    }

    public static IO<Fin<Cart>> AddToCart(
        Cart cart,
        Application._Abstractions.ExternalProductRef ext,
        int qty,
        string currency,
        IProductCatalog catalog) =>
            from pidFin in catalog.EnsureMapped(ext)
            from result in pidFin.Match<IO<Fin<Cart>>>(
                pid =>
                    from priceFin in catalog.GetCurrentPrice(pid, currency)
                    select priceFin.Map(price => cart.Add(pid, qty, price)),
                err => IO.lift<Fin<Cart>>(() => Fin<Cart>.Fail(err)))
            select result;

    public static TraceableT<Cart> AddToCartTraced(
        Cart cart,
        Application._Abstractions.ExternalProductRef ext,
        int qty,
        string currency,
        IProductCatalog catalog) =>
            from pid in TraceableTLifts.FromIOFinThrowing(
                            catalog.EnsureMapped(ext),
                            "cart.map_ext_to_internal",
                            id => new[] { KVP("product.id", id.Value) })
            from price in TraceableTLifts.FromIOFinThrowing(
                            catalog.GetCurrentPrice(pid, currency),
                            "cart.get_price",
                            m => new[] { KVP("price", m.Amount), KVP("currency", m.Currency) })
            select cart.Add(pid, qty, price);

    private static KeyValuePair<string, object> KVP(string k, object v) => new(k, v);

    //private CartSnapshot ToSnapshotWithValidation(Guid CustomerId, string country, string region)
    //{
    //    var customer = new RegisteredCustomerId(CustomerId);
    //    var validation = CartValidator.ValidateForCharge(this);
    //    var errors = validation.Match(
    //        Succ: _ => [],
    //        Fail: errs => errs.Map(e => e.Message).ToArray()
    //    );
    //    var isValid = errors.Length == 0;
    //    // but if we have errors... should we have a total? what if its negative? what if its zero?
    //    // we should freeze the items in the cart, not the total, right?
    //    return new CartSnapshot(this, customer, GetTotal(), country, region, isValid, errors);
    //}
}