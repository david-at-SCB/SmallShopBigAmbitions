using SmallShopBigAmbitions.Logic_examples;
using SmallShopBigAmbitions.Monads.Traceable;
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


    public TraceableT<Cart> GetCartForUser(Guid userId)
    {
        var tracedCart = TraceableTLifts.FromIO(
            IO.lift(() =>
            {
                // Simulate fetching from DB
                return new Cart
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Items = Option<string[]>.None
                };
            }),
            "cart.fetch",
            activitySource: Telemetry.CartSource,
            attributes: cart =>
            [
                new KeyValuePair<string, object>("cart.id", cart.Id),
                new KeyValuePair<string, object>("user.id", cart.UserId),
                new KeyValuePair<string, object>("cart.item.count", cart.Items.Match(items => items.Length, () => 0))
            ]
        ).WithLogging(_logger);

        return tracedCart;
    }

    public static TraceableT<Cart> GetCartForUser_first_iteration(Guid userId, ILogger logger)
    {
        return new TraceableT<Cart>(
            Effect: IO.lift(() =>
            {
                Thread.Sleep(1500); // Simulate DB or API call
                return new Cart
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Items = Option<string[]>.Some(["item1", "item2"])
                };
            }),
            SpanName: "CartService.GetCartForUser",
            ActivitySource: Telemetry.CartSource,
            Attributes: cart =>
            [
                new KeyValuePair<string, object>("cart.id", cart.Id),
                new KeyValuePair<string, object>("user.id", cart.UserId),
                new KeyValuePair<string, object>("cart.item.count", cart.Items.Match(items => items.Length, () => 0))
            ]
        ).WithLogging(logger);
    }

    public record Cart
    {
        public Guid Id { get; init; }
        public Guid UserId { get; init; }
        public Option<string[]> Items { get; init; }
    }
}