using SmallShopBigAmbitions.Application._Abstractions;
using SmallShopBigAmbitions.Monads.TraceableTransformer;
using SmallShopBigAmbitions.Monads.TraceableTransformer.Extensions.BaseLinq;
using SmallShopBigAmbitions.Models.Validation;
using LanguageExt;
namespace SmallShopBigAmbitions.Models;

public sealed record Cart(Guid Id, Guid CustomerId, CartItems Items)
{
    public static Cart Empty(Guid customerId) => new(Guid.NewGuid(), customerId, CartItems.Empty);

    public Cart Add(ProductId id, int qty, Money price) => this with { Items = Items.Add(id, qty, price) };

    public Cart SetQuantity(ProductId id, int qty) => this with { Items = Items.SetQuantity(id, qty) };

    public Money Total(string currency) => new(currency, Items.Values.Sum(l => l.UnitPrice.Amount * l.Quantity));

    public bool ValidTotal() =>
        !Items.IsEmpty
        && Items.Values.All(l => l.Quantity > 0 && l.UnitPrice.Amount > 0)
        && Items.Values.Sum(l => l.UnitPrice.Amount * l.Quantity) > 0m;

    public Fin<Unit> ValidateForCharge()
    {
        var validation = CartValidator.ValidateForCharge(this);
        return validation.Match(
            Succ: _ => Fin<Unit>.Succ(Unit.Default),
            Fail: errs => Fin<Unit>.Fail(Error.New(string.Join("; ", errs.Map(e => e.Message))))
        );
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
}