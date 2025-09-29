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

namespace SmallShopBigAmbitions.Business.Services;

public interface ICartService
{
    TraceableT<Fin<Cart>> GetCartForUser(Guid userId);

    TraceableT<Fin<Cart>> AddItems(Cart cart, HashMap<ProductId, CartLine> items);

    Cart GetCartByUserId(Guid userId);

    TraceableT<bool> SaveCart(CartSnapshot Cart);
    void LogFailedCheckoutAttempt(Error error, Guid customer);
}

public class CartService(IDataAccess dataAccess) : ICartService
{
    private readonly IDataAccess _dataAccess = dataAccess;

    public TraceableT<Fin<Cart>> GetCartForUser(Guid userId) =>
        _dataAccess.GetCustomerCart(userId)
            .WithSpanName("cart.fetch")
            .WithAttributes(fin => fin.Match(
                Succ: c => new[]
                {
                    new KeyValuePair<string, object>("cart.id", c.Id),
                    new KeyValuePair<string, object>("user.id", c.CustomerId),
                    new KeyValuePair<string, object>("cart.item.count", c.Items.Count)
                },
                Fail: e => new[] { new KeyValuePair<string, object>("error", e.Message) }
            ));

    public TraceableT<Fin<Cart>> AddItems(Cart cart, HashMap<ProductId, CartLine> items) =>
        TraceableTLifts.FromIOFin(
                IO.lift<Fin<Cart>>(() =>
                {
                    try
                    {
                        var merged = items.Fold(cart.Items.Lines, (acc, kv) =>
                        {
                            var existingQty = acc.Find(kv.ProductId).Match(l => l.Quantity, () => 0);
                            return acc.Add(kv.ProductId, kv with { Quantity = existingQty + kv.Quantity });
                        });
                        return Fin<Cart>.Succ(cart with { Items = new CartItems(merged) });
                    }
                    catch (Exception ex)
                    {
                        return Fin<Cart>.Fail(Error.New(ex));
                    }
                }),
                spanName: "cart.add_items")
            .WithAttributes(fin => fin.Match(
                Succ: c => new[]
                {
                    new KeyValuePair<string, object>("cart.id", c.Id),
                    new KeyValuePair<string, object>("customer.id", c.CustomerId),
                    new KeyValuePair<string, object>("cart.item.count", c.Items.Count)
                },
                Fail: e => new[] { new KeyValuePair<string, object>("error", e.Message) }
            ));

    public Cart GetCartByUserId(Guid userId) =>
        GetCartForUser(userId)
            .RunTraceable()
            .Run()
            .Match(Succ: c => c, Fail: _ => Cart.Empty(userId));

    public TraceableT<bool> SaveCart(CartSnapshot Cart)
    {
        throw new NotImplementedException();
    }

    public void LogFailedCheckoutAttempt(Error error, Guid customer)
    {
        throw new NotImplementedException();
    }
}