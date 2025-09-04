// Async/IO checklist (Razor Pages + LanguageExt)
// - Handlers return Task/Task<IActionResult>; always await RunTraceable(ct).RunAsync().
// - Avoid .Run(), .Result, .Wait() on IO/Aff in web code.
// - Accept and pass CancellationToken to all effects/HTTP calls.
// - Services: expose composable TraceableT/IO; add Task wrappers (e.g., GetXAsync) for UI.
// - Use Fin/Option for errors; handle via Match in handlers and return proper IActionResult.
// - Map DTO -> domain in a mapper; keep IO layer DTO-focused.
// - Ensure DI registers FunctionalHttpClient and service; no static singletons.
// - Add .WithLogging and telemetry attributes where useful.
using SmallShopBigAmbitions.Database;
using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.Monads.TraceableTransformer;
using LanguageExt;

namespace SmallShopBigAmbitions.Business.Services;

public interface ICartService
{
    TraceableT<Cart> GetCartForUser(Guid userId);

    TraceableT<Cart> AddItems(Cart cart, HashMap<ProductId, CartLine> items);

    Cart GetCartByUserId(Guid userId);
}

public class CartService(ILogger<CartService> logger, IDataAccess _dtAccss) : ICartService
{
    private readonly ILogger<CartService> _logger = logger;
    private readonly IDataAccess _DataAccess = _dtAccss;

    public TraceableT<Cart> GetCartForUser(Guid userId)
    {
        var tracedCart = TraceableTLifts.FromIO(
            IO.lift(() =>
            {
                return _DataAccess.GetCustomerCart(userId);
            }),
            "cart.fetch",
            attributes: cart =>
            [
                new KeyValuePair<string, object>("cart.id", cart.Id),
                new KeyValuePair<string, object>("user.id", cart.CustomerId),
                new KeyValuePair<string, object>("cart.item.count", cart.Lines.Count)
            ]
        ).WithLogging(_logger);

        return tracedCart;
    }

    // Merge the provided items into the cart's lines map (sum quantities for duplicates)
    public TraceableT<Cart> AddItems(Cart cart, HashMap<ProductId, CartLine> items)
    {
        return TraceableTLifts.FromIO(
            IO.lift(() =>
            {
                var toAdd = items.Fold(new HashMap<ProductId, CartLine>(), (acc, kv) =>
                {
                    var product = kv.ProductId; 
                    var quantity = kv.Quantity;
                    var price = kv.UnitPrice;

                    var pid = new ProductId(Guid.NewGuid()); // TODO map FakeStoreProduct -> ProductId
                    var existing = cart.Lines.Find(pid).Match(l => l.Quantity, () => 0);
                    return acc.Add(pid, new CartLine(pid, existing + quantity, price));
                });
                return cart with { Lines = cart.Lines + toAdd };
            }),
            spanName: "cart.add_items",
            attributes: c =>
            [
                new KeyValuePair<string, object>("cart.id", c.Id),
                new KeyValuePair<string, object>("customer.id", c.CustomerId),
                new KeyValuePair<string, object>("cart.item.count", c.Lines.Count)
            ]
        ).WithLogging(_logger);
    }

    public Cart GetCartByUserId(Guid customerId) => _DataAccess.GetCustomerCart(customerId);

    internal static Cart GetCartByCartId(Guid cartId)
    {
        throw new NotImplementedException();
    }

    public TraceableT<Cart> AddItems(Cart cart, Map<FakeStoreProduct, int> items)
    {
        throw new NotImplementedException();
    }
}