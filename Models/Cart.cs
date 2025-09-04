using SmallShopBigAmbitions.Application._Abstractions;
using SmallShopBigAmbitions.Monads.TraceableTransformer;
using SmallShopBigAmbitions.Models.Validation;
using LanguageExt;
namespace SmallShopBigAmbitions.Models;

public sealed record Cart(Guid Id, Guid CustomerId, LanguageExt.HashMap<ProductId, CartLine> Lines)
{
    public static Cart Empty(Guid customerId) => new(Guid.NewGuid(), customerId, new LanguageExt.HashMap<ProductId, CartLine>());

    /// <summary>
    /// Add quantity of a product to the cart. If the product exists, merge quantities; otherwise insert a new line.
    /// </summary>
    public Cart Add(ProductId id, int qty, Money price) =>
        qty <= 0
        ? this
        : this with
        {
            Lines = Lines.Find(id).Match(
                Some: line => Lines.SetItem(id, line with { Quantity = line.Quantity + qty }),
                None: () => Lines.Add(id, new CartLine(id, qty, price)))
        };

    /// <summary>
    /// Set quantity for a product; if qty <= 0, remove the line.
    /// </summary>
    public Cart SetQuantity(ProductId id, int qty) =>
        qty <= 0
        ? this with { Lines = Lines.Remove(id) }
        : this with
        {
            Lines = Lines.Find(id).Match(
                line => Lines.SetItem(id, line with { Quantity = qty }),
                () => Lines)  // keep as-is if product not present
        };

    /// <summary>
    /// Compute total in the requested currency (simple sum of line UnitPrice * Quantity).
    /// </summary>
    public Money Total(string currency) =>
        new(currency, Lines.Values.Sum(l => l.UnitPrice.Amount * l.Quantity));

    /// <summary>
    /// Declarative checker: all lines have qty > 0 and price > 0 and total > 0.
    /// Returns true when valid.
    /// </summary>
    public bool ValidTotal() =>
        !Lines.IsEmpty
        && Lines.Values.ForAll(l => l.Quantity > 0 && l.UnitPrice.Amount > 0)
        && Lines.Values.Sum(l => l.UnitPrice.Amount * l.Quantity) > 0m;

    /// <summary>
    /// Fin-based validation suitable for policies: Fail with a helpful error or Succ(Unit).
    /// Delegates to CartValidator (Validation monad) and converts to Fin.
    /// </summary>
    public Fin<Unit> ValidateForCharge()
    {
        var validation = CartValidator.ValidateForCharge(this);
        return validation.Match(
            Succ: _ => Fin<Unit>.Succ(Unit.Default),
            Fail: errs => Fin<Unit>.Fail(Error.New(string.Join("; ", errs.Map(e => e.Message))))
        );
    }

    // --------------------------------------------------------------------
    // Composing IO<Fin<T>> correctly (non-traced)
    // --------------------------------------------------------------------
    public static IO<Fin<Cart>> AddToCart(
        Cart cart,
        ExternalProductRef ext,
        int qty,
        string currency,
        IProductCatalog catalog) =>
            // First: map external ref -> internal ProductId
            from pidFin in catalog.EnsureMapped(ext)         // pidFin : Fin<ProductId>
            from result in pidFin.Match<IO<Fin<Cart>>>(
                pid =>
                    // Next: get current price for pid
                    from priceFin in catalog.GetCurrentPrice(pid, currency) // priceFin : Fin<Money>
                                                                            // Finally: construct the new cart inside Fin-map
                    select priceFin.Map(price => cart.Add(pid, qty, price)),

                err => IO.lift<Fin<Cart>>(() => Fin<Cart>.Fail(err)))
            select result;

    // --------------------------------------------------------------------
    // Same as above, but traced with your lifters (unwrapped values)
    // --------------------------------------------------------------------
    public static TraceableT<Cart> AddToCartTraced(
        Cart cart,
        ExternalProductRef ext,
        int qty,
        string currency,
        IProductCatalog catalog) =>
            from pid in TraceableTLifts.FromIOFinThrowingTracableT(
                            catalog.EnsureMapped(ext),
                            "cart.map_ext_to_internal",
                            id => new[] { KVP("product.id", id.Value) })
            from price in TraceableTLifts.FromIOFinThrowingTracableT(
                            catalog.GetCurrentPrice(pid, currency),
                            "cart.get_price",
                            m => new[] { KVP("price", m.Amount), KVP("currency", m.Currency) })
            select cart.Add(pid, qty, price);

    private static KeyValuePair<string, object> KVP(string k, object v) => new(k, v);
}