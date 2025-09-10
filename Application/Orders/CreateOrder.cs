namespace SmallShopBigAmbitions.Application.Orders;

using LanguageExt;
using SmallShopBigAmbitions.Application._Abstractions; // IIdempotencyStore
using SmallShopBigAmbitions.Application._Policy;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.Monads.TraceableTransformer;
using static LanguageExt.Prelude;

public sealed record CreateOrderCommand(Guid CartId, Guid UserId, string Currency, string? IdempotencyKey = null)
    : IFunctionalRequest<OrderSnapshot>;

public sealed class CreateOrderPolicy : IAuthorizationPolicy<CreateOrderCommand>
{
    public Fin<Unit> Authorize(CreateOrderCommand request, TrustedContext context)
        => context.IsAuthenticated
            ? FinSucc(Unit.Default)
            : FinFail<Unit>(Error.New("Unauthorized"));
}

public interface IOrderRepository
{
    IO<Fin<OrderSnapshot>> Insert(OrderSnapshot order);
}

public sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly Dictionary<Guid, OrderSnapshot> _store = new();

    public IO<Fin<OrderSnapshot>> Insert(OrderSnapshot order) => IO.lift<Fin<OrderSnapshot>>(() =>
    {
        _store[order.OrderId] = order;
        return FinSucc(order);
    });
}

internal static class OrderIdem
{
    public const string Scope = "order.create";

    public static string ComputeFingerprint(Guid cartId, Guid userId, Money total) =>
         $"order|{cartId}|{userId}|{total.Amount:0.00}|{total.Currency}";

    public static Guid GuidFromFingerprint(string fingerprint)
    {
        // Deterministic Guid from fingerprint (e.g., SHA-1 → Guid); placeholder below:
        return GuidUtility.Create(GuidUtility.UrlNamespace, fingerprint);
    }

    public static class GuidUtility
    {
        // Implement your deterministic GUID generation (e.g., from SHA-1). Placeholder:
        public static readonly Guid UrlNamespace = Guid.Parse("6ba7b811-9dad-11d1-80b4-00c04fd430c8");

        public static Guid Create(Guid ns, string name)
        {
            using var sha1 = System.Security.Cryptography.SHA1.Create();
            var nsBytes = ns.ToByteArray();
            var nameBytes = System.Text.Encoding.UTF8.GetBytes(name);
            var data = nsBytes.Concat(nameBytes).ToArray();
            var hash = sha1.ComputeHash(data);
            var bytes = new byte[16];
            System.Array.Copy(hash, 0, bytes, 0, 16); // Explicitly specify System.Array to avoid ambiguity with LanguageExt.Prelude.Array

            // set version & variant bits if you care; keeping simple here
            return new Guid(bytes);
        }
    }
}

public static class TraceableTExtensionsOrder
{
    public static TraceableT<T> FromIOFin<T>(IO<Fin<T>> io, string spanName) => default!;

    public static TraceableT<T> FromFin<T>(Fin<T> fin, string spanName, Func<T, Seq<(string, object)>> attrs) => default!;

    public static TraceableT<T> WithLogging<T>(this TraceableT<T> t, ILogger logger) => t;

    public static IO<Fin<T>> RunTraceable<T>(this TraceableT<T> t, CancellationToken ct) => default!;
}

public sealed class CreateOrderHandler(
    ICartQueries carts,
    IPricingService pricing,
    IOrderRepository orders,
    IIdempotencyStore idem,
    ILogger<CreateOrderHandler> logger) : IFunctionalHandler<CreateOrderCommand, OrderSnapshot>
{
    private readonly ICartQueries _carts = carts;
    private readonly IPricingService _pricing = pricing;
    private readonly IOrderRepository _orders = orders;
    private readonly IIdempotencyStore _idem = idem;
    private readonly ILogger _logger = logger;

    public IO<Fin<OrderSnapshot>> Handle(CreateOrderCommand request, TrustedContext context, CancellationToken ct)
    {
        var flow =
            // 1) Fetch cart snapshot (unwrapped)
            from cart in TraceableTLifts.FromIOFinThrowing(
                _carts.GetCart(request.CartId),
                "order.cart.fetch",
                c => [KVP("cart.id", c.CartId), KVP("user.id", c.UserId)])

                // 2) Pricing (unwrapped monies)
            from shipping in TraceableTLifts.FromIOFinThrowing(
                _pricing.CalculateShipping(cart),
                "order.pricing.shipping",
                m => [KVP("shipping", m.Amount), KVP("currency", m.Currency)])

            from discounts in TraceableTLifts.FromIOFinThrowing(
                _pricing.CalculateDiscounts(cart),
                "order.pricing.discounts",
                m => [KVP("discount", m.Amount), KVP("currency", m.Currency)])

            let taxableBase = cart.Subtotal.Plus(shipping).Minus(discounts)

            from tax in TraceableTLifts.FromIOFinThrowing(
                    _pricing.CalculateTaxes(cart, taxableBase),
                    "order.pricing.tax",
                    m => [KVP("tax", m.Amount), KVP("currency", m.Currency)])

            let total = taxableBase.Plus(tax)

            // 3) Idempotency
            let fingerprint = OrderIdem.ComputeFingerprint(cart.CartId, cart.UserId, total)
            let key = string.IsNullOrWhiteSpace(request.IdempotencyKey)
                      ? $"{cart.UserId}:{cart.CartId}"
                      : request.IdempotencyKey!
            let ttl = TimeSpan.FromMinutes(30)

            from lookup in TraceableTLifts.FromIOFinThrowing(
                _idem.TryAcquire(OrderIdem.Scope, key, fingerprint, ttl, ct),
                "order.idem.try_acquire",
                l => [KVP("idem.state", l.State.ToString()), KVP("key", key)])

                // 4) Handle the idempotency state
            from order in HandleIdempotencyState(lookup, cart, shipping, discounts, tax, total, key, fingerprint, ct)
            select order;

        // Finish by running and returning IO<Fin<OrderSnapshot>>
        return flow.RunTraceableFin(ct);
    }

    private static KeyValuePair<string, object> KVP(string k, object v) => new(k, v);

    private TraceableT<OrderSnapshot> HandleIdempotencyState(
           IdemLookup<string> lookup,
           CartSnapshot cart,
           Money shipping,
           Money discounts,
           Money tax,
           Money total,
           string key,
           string fingerprint,
           CancellationToken ct)
    {
        return lookup.State switch
        {
            IdempotencyState.Acquired =>
                from order in TraceableTLifts.FromIOFinThrowing(
                    _orders.Insert(CreateOrderSnapshot(cart, shipping, discounts, tax, total, fingerprint)),
                    "order.persist",
                    o => new[] { new KeyValuePair<string, object>("order.id", o.OrderId) })

                from _ in TraceableTLifts.FromIOFinThrowing(
                    _idem.Complete(
                        OrderIdem.Scope,
                        key,
                        new
                        {
                            orderId = order.OrderId,
                            total = order.Total.Amount,
                            currency = order.Total.Currency
                        },
                        ct),
                    "order.idem.complete")
                select order,

            IdempotencyState.DuplicateSameDone =>
                // Deterministic reconstruction (or fetch from repo if you store & want the canonical persisted version)
                TraceableTLifts.FromValue(
                    CreateOrderSnapshot(cart, shipping, discounts, tax, total, fingerprint),
                    "order.idem.cached"),

            IdempotencyState.DuplicateSameBusy =>
                // Someone else is creating the same order right now
                TraceableTLifts.FromIO(
                    IO.fail<OrderSnapshot>(Error.New("Order creation in progress")),
                    "order.idem.busy"),

            IdempotencyState.DuplicateDifferent =>
                TraceableTLifts.FromIO(
                    IO.fail<OrderSnapshot>(Error.New("Idempotency key reused with different payload")),
                    "order.idem.conflict"),

            _ =>
                TraceableTLifts.FromIO(
                    IO.fail<OrderSnapshot>(Error.New("Unknown idempotency state")),
                    "order.idem.unknown")
        };
    }

    private static OrderSnapshot CreateOrderSnapshot(
           CartSnapshot cart,
           Money shipping,
           Money discounts,
           Money tax,
           Money total,
           string fingerprint)
    {
        return new OrderSnapshot(
            OrderId: OrderIdem.GuidFromFingerprint(fingerprint),
            UserId: cart.UserId,
            CartSnapshot: cart,
            Subtotal: cart.Subtotal,  // items-only snapshot
            Discount: discounts,      // positive discount
            Shipping: shipping,
            Tax: tax,
            Total: total,
            Status: OrderStatus.Created,
            CreatedAt: DateTimeOffset.UtcNow
        );
    }
}