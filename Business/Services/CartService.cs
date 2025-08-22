using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.Monads.TraceableTransformer;
using SmallShopBigAmbitions.TracingSources;

namespace SmallShopBigAmbitions.Business.Services;

public class CartService
{
    private readonly ILogger<CartService> _logger;

    public CartService(ILogger<CartService> logger)
    {
        _logger = logger;
    }

    public TraceableT<CustomerCart> GetCartForUser(Guid userId)
    {
        var tracedCart = TraceableTLifts.FromIO(
            IO.lift(() =>
            {
                // Simulate fetching from DB
                return new CustomerCart(
                    Id: Guid.NewGuid(),
                    UserId: userId,
                    Items: Option<string[]>.None
                );
            }),
            "cart.fetch",
            attributes: cart =>
            [
                new KeyValuePair<string, object>("cart.id", cart.Id),
                new KeyValuePair<string, object>("user.id", cart.UserId),
                new KeyValuePair<string, object>("cart.item.count", cart.Items.Match(items => items.Length, () => 0))
            ]
        ).WithLogging(_logger);

        return tracedCart;
    }

    public TraceableT<CustomerCart> AddItems(CustomerCart cart, IEnumerable<string> items)
    {
        return TraceableTLifts.FromIO(
            IO.lift(() => cart with
            {
                Items = Option<string[]>.Some(items.ToArray())
            }),
            spanName: "cart.add_items",
            attributes: c =>
            [
                new KeyValuePair<string, object>("cart.id", c.Id),
                new KeyValuePair<string, object>("user.id", c.UserId),
                new KeyValuePair<string, object>("cart.item.count", c.Items.Match(xs => xs.Length, () => 0))
            ]
        ).WithLogging(_logger);
    }

    public static TraceableT<CustomerCart> GetCartForUser_first_iteration(Guid userId, ILogger logger)
    {
        return new TraceableT<CustomerCart>(
            Effect: IO.lift(() =>
            {
                Thread.Sleep(1500); // Simulate DB or API call
                return new CustomerCart(
                    Id: Guid.NewGuid(),
                    UserId: userId,
                    Items: Option<string[]>.Some(["item1", "item2"]) 
                );
            }),
            SpanName: "CartService.GetCartForUser",
            Attributes: cart =>
            [
                new KeyValuePair<string, object>("cart.id", cart.Id),
                new KeyValuePair<string, object>("user.id", cart.UserId),
                new KeyValuePair<string, object>("cart.item.count", cart.Items.Match(items => items.Length, () => 0))
            ]
        ).WithLogging(logger);
    }
}