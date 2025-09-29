namespace SmallShopBigAmbitions.Application.Orders;

using LanguageExt;
using SmallShopBigAmbitions.Application._Abstractions; // IIdempotencyStore
using SmallShopBigAmbitions.Application._Policy;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.Monads.TraceableTransformer;
using SmallShopBigAmbitions.Monads.TraceableTransformer.Extensions.BaseLinq;
using System.Collections.Generic;
using static LanguageExt.Prelude;

public sealed record CreateOrderCommand(
    Guid CartId,
    CustomerId Customer,
    string Currency,
    string IdempotencyKey // required, always present
) : IFunctionalRequest<OrderSnapshot>;

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
    private readonly Dictionary<Guid, OrderSnapshot> _store = [];

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

    public static Guid GuidFromFingerprint(string fingerprint) =>
        GuidUtility.Create(GuidUtility.UrlNamespace, fingerprint);

    public static class GuidUtility
    {
        public static readonly Guid UrlNamespace = Guid.Parse("6ba7b811-9dad-11d1-80b4-00c04fd430c8");

        public static Guid Create(Guid ns, string name)
        {
            var nsBytes = ns.ToByteArray();
            var nameBytes = System.Text.Encoding.UTF8.GetBytes(name);
            var data = nsBytes.Concat(nameBytes).ToArray();
            var hash = System.Security.Cryptography.SHA1.HashData(data);
            var bytes = new byte[16];
            System.Array.Copy(hash, 0, bytes, 0, 16);
            return new Guid(bytes);
        }
    }
}

public sealed class CreateOrderHandler(
    ICartQueries carts,
    IPricingService pricing,
    IOrderRepository orders,
    IIdempotencyStore idem
) : IFunctionalHandler<CreateOrderCommand, OrderSnapshot>
{
    private readonly ICartQueries _carts = carts;
    private readonly IPricingService _pricing = pricing;
    private readonly IOrderRepository _orders = orders;
    private readonly IIdempotencyStore _idem = idem;

    public IO<Fin<OrderSnapshot>> Handle(CreateOrderCommand request, TrustedContext context, CancellationToken ct)
    {
        // Flow returns TraceableT<Fin<OrderSnapshot>>.
        var flow =
            TraceableTLifts.FromIOFin(_carts.GetCart(request.CartId), "order.cart.fetch")
                .BindFin(cart =>
                    cart.Valid
                        ? TraceableTLifts.FromFin(FinSucc(cart), "order.cart.valid", _ => System.Array.Empty<KeyValuePair<string, object>>())
                        : TraceableTLifts.FromFin(FinFail<CartSnapshot>(Error.New("cart.invalid")), "order.cart.invalid", _ => System.Array.Empty<KeyValuePair<string, object>>()))
                .BindFin(cart =>
                    TraceableTLifts.FromIOFin(_pricing.CalculateShipping(cart), "order.shipping")
                        .BindFin(shipping =>
                            TraceableTLifts.FromIOFin(_pricing.CalculateDiscounts(cart), "order.discounts")
                                .BindFin(discounts =>
                                {
                                    var taxableBase = cart.Subtotal.Plus(shipping).Minus(discounts);
                                    return TraceableTLifts.FromIOFin(_pricing.CalculateTaxes(cart, taxableBase), "order.taxes")
                                        .BindFin(tax =>
                                        {
                                            var total = taxableBase.Plus(tax);
                                            var fingerprint = OrderIdem.ComputeFingerprint(cart.Cart.Id, cart.CustomerId.Id, total);
                                            var key = request.IdempotencyKey;
                                            var ttl = TimeSpan.FromMinutes(30);

                                            return TraceableTLifts.FromIOFin(_idem.TryAcquire(OrderIdem.Scope, key, fingerprint, ttl, ct), "order.idem.acquire")
                                                .BindFin(lookup =>
                                                    HandleIdempotencyState(lookup, cart, shipping, discounts, tax, total, key, fingerprint, ct)
                                                        .Map(o => FinSucc(o))
                                                );
                                        });
                                }))
                .WithSpanName("order.create.flow"));

        return flow.RunTraceable(ct);
    }

    private TraceableT<OrderSnapshot> HandleIdempotencyState(
           IdemLookup<string> lookup,
           CartSnapshot cart,
           Money shipping,
           Money discounts,
           Money tax,
           Money total,
           string key,
           string fingerprint,
           CancellationToken ct) =>
        lookup.State switch
        {
            IdempotencyState.Acquired =>
                (from order in TraceableTLifts.FromIOFinThrowing(
                        _orders.Insert(CreateOrderSnapshot(cart, shipping, discounts, tax, total, fingerprint)),
                        "order.persist",
                        o => new[] { new KeyValuePair<string, object>("order.id", o.OrderId) })
                 from _ in TraceableTLifts.FromIOFinThrowing(
                        _idem.Complete(
                            OrderIdem.Scope,
                            key,
                            new { orderId = order.OrderId, total = order.Total.Amount, currency = order.Total.Currency },
                            ct),
                        "order.idem.complete")
                 select order),

            IdempotencyState.DuplicateSameDone =>
                TraceableTLifts.FromValue(
                    CreateOrderSnapshot(cart, shipping, discounts, tax, total, fingerprint),
                    "order.idem.cached"),

            IdempotencyState.DuplicateSameBusy =>
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

    private static OrderSnapshot CreateOrderSnapshot(
           CartSnapshot cart,
           Money shipping,
           Money discounts,
           Money tax,
           Money total,
           string fingerprint) => new(
               OrderId: OrderIdem.GuidFromFingerprint(fingerprint),
               UserId: cart.CustomerId.Id, // fix: pass Guid not CustomerId
               CartSnapshot: cart,
               Subtotal: cart.Subtotal,
               Discount: discounts,
               Shipping: shipping,
               Tax: tax,
               Total: total,
               Status: OrderStatus.Created,
               CreatedAt: DateTimeOffset.UtcNow);
}