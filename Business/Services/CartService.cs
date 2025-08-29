// Async/IO checklist (Razor Pages + LanguageExt)
// - Handlers return Task/Task<IActionResult>; always await RunTraceable(ct).RunAsync().
// - Avoid .Run(), .Result, .Wait() on IO/Aff in web code.
// - Accept and pass CancellationToken to all effects/HTTP calls.
// - Services: expose composable TraceableT/IO; add Task wrappers (e.g., GetXAsync) for UI.
// - Use Fin/Option for errors; handle via Match in handlers and return proper IActionResult.
// - Map DTO -> domain in a mapper; keep IO layer DTO-focused.
// - Ensure DI registers FunctionalHttpClient and service; no static singletons.
// - Add .WithLogging and telemetry attributes where useful.
using Microsoft.AspNetCore.Http.Features;
using SmallShopBigAmbitions.Application.Billing.CheckoutUser;
using SmallShopBigAmbitions.Application.Cart.AddItemToCart;
using SmallShopBigAmbitions.Database;
using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.Monads.TraceableTransformer;
using SmallShopBigAmbitions.TracingSources;
using LanguageExt;
using static LanguageExt.Prelude;

namespace SmallShopBigAmbitions.Business.Services;

public class CartService(ILogger<CartService> logger, IDataAccess _dtAccss)
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
                new KeyValuePair<string, object>("cart.item.count", cart.Items.Count)
            ]
        ).WithLogging(_logger);

        return tracedCart;
    }

    // Merge the provided items into the cart's items map
    public TraceableT<Cart> AddItems(Cart cart, Map<FakeStoreProduct, int> items)
    {
        return TraceableTLifts.FromIO(
            IO.lift(() => cart with
            {
                Items = cart.Items + items
            }),
            spanName: "cart.add_items",
            attributes: c =>
            [
                new KeyValuePair<string, object>("cart.id", c.Id),
                new KeyValuePair<string, object>("user.id", c.CustomerId),
                new KeyValuePair<string, object>("cart.item.count", c.Items.Count)
            ]
        ).WithLogging(_logger);
    }

    public static TraceableT<Cart> GetCartForUser_first_iteration(Guid userId, ILogger logger)
    {
        return new TraceableT<Cart>(
            Effect: IO.lift(() =>
            {
                Thread.Sleep(1500); // Simulate DB or API call
                return Cart.Empty(userId);
            }),
            SpanName: "CartService.GetCartForUser",
            Attributes: cart =>
            [
                new KeyValuePair<string, object>("cart.id", cart.Id),
                new KeyValuePair<string, object>("user.id", cart.CustomerId),
                new KeyValuePair<string, object>("cart.item.count", cart.Items.Count)
            ]
        ).WithLogging(logger);
    }

    internal Cart GetCartByUserId(Guid userId)
    {
        // TODO: Trace?
        return _DataAccess.GetCustomerCart(userId);
    }

    internal static Cart GetCartById(Guid cartId)
    {
        throw new NotImplementedException();
    }
}