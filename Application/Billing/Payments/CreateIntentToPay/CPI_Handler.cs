namespace SmallShopBigAmbitions.Application.Billing.Payments.CreateIntentToPay;

using LanguageExt;
using SmallShopBigAmbitions.Application._Abstractions;
using SmallShopBigAmbitions.Application.Billing.Payments.CreatePaymentIntent;
using SmallShopBigAmbitions.Application.Billing.Payments.CreatePaymentIntent.Observability;
using SmallShopBigAmbitions.Application.Billing.Payments.CreatePaymentIntent.Repo;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.Monads.TraceableTransformer;
using SmallShopBigAmbitions.Monads.TraceableTransformer.Extensions.FinLinq;
using static LanguageExt.Prelude;

public class CreateIntentToPayHandler(
    CreateIntentToPayPolicy policy,
    IPricingService pricing,
    IInventoryService inventory,
    IPaymentIntentRepository repo,
    IIdempotencyStore idempotency,
    IFunctionalDispatcher dispatcher,
    IEventPublisher events) : IFunctionalHandler<IntentToPayCommand, IntentToPayDto>
{
    private readonly CreateIntentToPayPolicy _policy = policy;
    private readonly IPricingService _pricing = pricing;
    private readonly IInventoryService _inventory = inventory;
    private readonly IPaymentIntentRepository _repo = repo;
    private readonly IIdempotencyStore _idempotency = idempotency;
    private readonly IFunctionalDispatcher _dispatcher = dispatcher;
    private readonly IEventPublisher _events = events;

    // ---------------------------------
    // Stage markers (phantom types)
    // ---------------------------------
    public sealed record PolicyOk; // expose for FlowState
    private sealed record Priced;
    private sealed record Reserved;
    private sealed record ProviderCreated;
    private sealed record IntentBuilt;
    private sealed record Persisted;
    private sealed record Published;

    // Strongly-typed accessors (only available at specific stages)
    private static T Require<T>(Option<T> opt, string name) =>
        opt.Match(Some: v => v, None: () => throw new InvalidOperationException($"{name} is required at this stage"));

    private static (Money Shipping, Money Discounts, Money Tax, Money Total) RequirePricing(FlowState<Priced> st) =>
        (
            Require(st.Shipping, nameof(st.Shipping)),
            Require(st.Discounts, nameof(st.Discounts)),
            Require(st.Tax, nameof(st.Tax)),
            Require(st.Total, nameof(st.Total))
        );

    private static Guid RequireReservationId(FlowState<Reserved> st) =>
        Require(st.ReservationId, nameof(st.ReservationId));

    private static TimeSpan RequireTtl(FlowState<Reserved> st) =>
        Require(st.Ttl, nameof(st.Ttl));

    private static ProviderIntent RequireProviderIntent(FlowState<ProviderCreated> st) =>
        Require(st.ProviderIntent, nameof(st.ProviderIntent));

    private static PaymentIntent RequireIntent(FlowState<IntentBuilt> st) =>
        Require(st.Intent, nameof(st.Intent));

    private static PaymentIntent RequirePersistedIntent(FlowState<Persisted> st) =>
        Require(st.Intent, nameof(st.Intent));

    // ---------------------------------
    // Step-local wrappers (TraceableT<Fin<...>>)
    // ---------------------------------
    private TraceableT<Fin<Unit>> RequireAuth(TrustedContext context)
        => TraceableTLifts.FromIOFin(AuthorizationGuards.RequireTrustedFin(context), "auth.require_trusted");

    private TraceableT<Fin<(CartSnapshot Cart, IPaymentProvider Provider)>> Policy(IntentToPayCommand request)
        => TraceableTLifts.FromIOFin(_policy.Check(request), ActivityNames.PaymentCreateIntent)
                          .WithAttributes(fin => MakePolicyAttrs(fin, request));

    private TraceableT<Fin<Money>> Shipping(CartSnapshot cart)
        => TraceableTLifts.FromIOFin(_pricing.CalculateShipping(cart), ActivityNames.PricingCalculate + ".shipping");

    private TraceableT<Fin<Money>> Discounts(CartSnapshot cart)
        => TraceableTLifts.FromIOFin(_pricing.CalculateDiscounts(cart), ActivityNames.PricingCalculate + ".discounts");

    private TraceableT<Fin<Money>> Taxes(CartSnapshot cart, Money shipping, Money discounts)
        => TraceableTLifts.FromIOFin(
            _pricing.CalculateTaxes(cart, new Money(cart.Subtotal.Currency, cart.Subtotal.Amount + shipping.Amount - discounts.Amount)),
            ActivityNames.PricingCalculate + ".tax");

    private TraceableT<Fin<Unit>> Reserve(CartSnapshot cart, Guid reservationId, TimeSpan ttl)
        => TraceableTLifts.FromIOFin(_inventory.Reserve(cart, reservationId, ttl), ActivityNames.InventoryReserve)
                          .WithAttributes(fin => ReserveAttrs(fin, reservationId));

    private TraceableT<Fin<ProviderIntent>> CreateProviderIntent(IPaymentProvider provider, CartSnapshot cart, Money total, IntentToPayCommand request)
        => TraceableTLifts.FromIOFin(
            total is null
                ? IO.lift<Fin<ProviderIntent>>(() => Fin<ProviderIntent>.Fail(PaymentErrors.PricingFailed))
                : provider.CreateIntent(new ProviderIntentRequest(
                    Description: $"Cart {cart.Cart.Id}",
                    Amount: total,
                    PaymentIntentId: Guid.NewGuid(),
                    CartId: cart.Cart.Id,
                    UserId: cart.CustomerId.Id,
                    Method: request.Method,
                    Metadata: request.Metadata)),
            ActivityNames.ProviderCreateIntent);

    private TraceableT<Fin<PaymentIntent>> Persist(PaymentIntent intent)
        => _repo.Insert(intent).WithSpanName(ActivityNames.PersistPaymentIntent);

    private TraceableT<Fin<Unit>> Publish(PaymentIntent intent, ProviderIntent pi)
        => TraceableTLifts.FromIOFin(
            _events.Publish(new PaymentIntentCreatedEvent(
                intent.Id,
                intent.CartId,
                intent.UserId,
                intent.Provider,
                intent.ProviderIntentId,
                intent.Amount,
                intent.Currency)),
            "event.publish.payment_intent_created");

    // ---------------------------------
    // Handle: typed-stage flow
    // ---------------------------------
    public IO<Fin<IntentToPayDto>> Handle(IntentToPayCommand request, TrustedContext context, CancellationToken ct)
    {
        // Combine pricing into ONE step => reach the Priced stage safely
        var chain =
            from _ in RequireAuth(context)
            from st0 in Flow.Start(Policy(request), ActivityNames.PaymentCreateIntent, fin => MakePolicyAttrs(fin, request))

                // Pricing (combined): shipping + discounts + tax + total -> FlowState<Priced>
            from st1 in (
                from st in st0.ToTraceable()
                from priced in
                    (from ship in Shipping(st.Cart)
                     from disc in Discounts(st.Cart)
                     from tax in Taxes(st.Cart, ship, disc)
                     let total = ComputeTotal(st.Cart, ship, disc, tax)
                     select (ship, disc, tax, total))
                    .WithSpanName("pricing.compute_all")
                    .WithAttributes(fin =>
                        fin.Match(
                            Succ: t => new[]
                            {
                                KVP("pricing.shipping.amount", t.ship.Amount),
                                KVP("pricing.discounts.amount", t.disc.Amount),
                                KVP("pricing.tax.amount", t.tax.Amount),
                                KVP("payment.total.amount", t.total.Amount),
                                KVP("payment.currency", t.total.Currency)
                            },
                            Fail: e => [KVP(Attr.Error, e.Message)]
                        )
                    )
                select new FlowState<Priced>(
                    st.Cart, st.Provider,
                    Some(priced.ship), Some(priced.disc), Some(priced.tax), Some(priced.total),
                    st.ReservationId, st.Ttl, st.ProviderIntent, st.Intent)
            )

                // Plan reservation (pure, keep stage = Priced, but set ReservationId/Ttl)
            from st2 in Flow.StepPure<Priced, Priced, (Guid reservationId, TimeSpan ttl)>(
                st1.ToTraceable(),
                st => (Guid.NewGuid(), TimeSpan.FromMinutes(15)),
                (st, x) => st with { ReservationId = Some(x.reservationId), Ttl = Some(x.ttl) },
                "inventory.reserve.plan",
                (st, x) => new[]
                {
                    KVP("reservation.id", x.reservationId),
                    KVP("reservation.ttl_ms", (long)x.ttl.TotalMilliseconds)
                })

                // Reserve -> FlowState<Reserved>
            from st3 in Flow.Step<Priced, Reserved, Unit>(
                st2.ToTraceable(),
                st => Reserve(st.Cart, Require(st.ReservationId, nameof(st.ReservationId)), Require(st.Ttl, nameof(st.Ttl))),
                (st, _) => new FlowState<Reserved>(
                    st.Cart, st.Provider,
                    st.Shipping, st.Discounts, st.Tax, st.Total,
                    st.ReservationId, st.Ttl, st.ProviderIntent, st.Intent),
                ActivityNames.InventoryReserve,
                (st, fin) => ErrorAttrs(fin)
            )

                // Create provider intent -> FlowState<ProviderCreated>
            from st4 in Flow.Step<Reserved, ProviderCreated, ProviderIntent>(
                st3.ToTraceable(),
                st => CreateProviderIntent(st.Provider, st.Cart, Require(st.Total, nameof(st.Total)), request),
                (st, pi) => new FlowState<ProviderCreated>(
                    st.Cart, st.Provider,
                    st.Shipping, st.Discounts, st.Tax, st.Total,
                    st.ReservationId, st.Ttl, Some(pi), st.Intent),
                ActivityNames.ProviderCreateIntent,
                (st, fin) => ErrorAttrs(fin)
            )

                // Build PaymentIntent (pure) -> FlowState<IntentBuilt>
            from st5 in Flow.StepPure<ProviderCreated, IntentBuilt, PaymentIntent>(
                st4.ToTraceable(),
                st =>
                {
                    var pi = Require(st.ProviderIntent, nameof(st.ProviderIntent));
                    var ttl = Require(st.Ttl, nameof(st.Ttl));
                    var now = DateTimeOffset.UtcNow;
                    return new PaymentIntent(
                        Id: Guid.NewGuid(),
                        CartId: st.Cart.Cart.Id,
                        UserId: st.Cart.CustomerId.Id,
                        Provider: st.Provider.Name,
                        ProviderIntentId: pi.ProviderIntentId,
                        Currency: pi.Amount.Currency,
                        Amount: pi.Amount.Amount,
                        Status: PaymentIntentStatus.Pending,
                        ClientSecret: Some(pi.ClientSecret),
                        IdempotencyKey: Optional(request.IdempotencyKey),
                        Metadata: request.Metadata,
                        CreatedAt: now,
                        UpdatedAt: now,
                        ExpiresAt: now.Add(ttl),
                        ReservationId: Require(st.ReservationId, nameof(st.ReservationId)));
                },
                (st, intent) => new FlowState<IntentBuilt>(
                    st.Cart, st.Provider,
                    st.Shipping, st.Discounts, st.Tax, st.Total,
                    st.ReservationId, st.Ttl, st.ProviderIntent, Some(intent)),
                "payments.intent.build",
                (st, intent) => new[]
                {
                    KVP("payment.intent.amount", intent.Amount),
                    KVP("payment.intent.currency", intent.Currency.ToString())
                }
            )

                // Persist -> FlowState<Persisted>
            from st6 in Flow.Step<IntentBuilt, Persisted, PaymentIntent>(
                st5.ToTraceable(),
                st => Persist(Require(st.Intent, nameof(st.Intent))).WithAttributes(fin => PersistAttrs(fin.Map(_ => NewPlaceholder(Require(st.Intent, nameof(st.Intent)))))),
                (st, persisted) => new FlowState<Persisted>(
                    st.Cart, st.Provider,
                    st.Shipping, st.Discounts, st.Tax, st.Total,
                    st.ReservationId, st.Ttl, st.ProviderIntent, Some(persisted)),
                ActivityNames.PersistPaymentIntent,
                (st, fin) => ErrorAttrs(fin)
            )

                // Publish (no state change of interest) -> FlowState<Published>
            from st7 in Flow.Step<Persisted, Published, Unit>(
                st6.ToTraceable(),
                st => Publish(Require(st.Intent, nameof(st.Intent)), Require(st.ProviderIntent, nameof(st.ProviderIntent))),
                (st, _) => new FlowState<Published>(
                    st.Cart, st.Provider,
                    st.Shipping, st.Discounts, st.Tax, st.Total,
                    st.ReservationId, st.Ttl, st.ProviderIntent, st.Intent),
                "event.publish.payment_intent_created",
                (st, fin) => ErrorAttrs(fin)
            )

                // Map to DTO
            select Mapping.ToDto(
                Require(st6.Intent, nameof(st6.Intent)),
                Require(st4.ProviderIntent, nameof(st4.ProviderIntent)).ProviderMetadata
            );

        var traced = chain.WithSpanName("payments.intent.create.flow");
        var run = traced.RunTraceable(ct); // IO<Fin<IntentToPayDto>>

        // Idempotency logic (unchanged)
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            return run;

        // Fin<T>.Match returns IO<Fin<T>> here because both branches produce IO<Fin<IntentToPayDto>>.
        return run.Bind(
            fin => fin.Match(
                Succ: dto =>
                {
                    var scope = $"payments:{request.CartId}:intent";
                    var fingerprint = $"{request.CartId}|{request.Method}|{dto.Amount:0.00}|{dto.Currency}";
                    var cached = IO.lift<Fin<IntentToPayDto>>(() => FinSucc<IntentToPayDto>(dto));
                    return IdempotencyOps.WithIdempotency(
                    _idempotency,
                    scope,
                    request.IdempotencyKey!,
                    fingerprint,
                    TimeSpan.FromHours(15),
                    cached,
                    ct);
                },
                Fail: e => IO.lift<Fin<IntentToPayDto>>(() => FinFail<IntentToPayDto>(e))
        ));

    }

    // --------------------------
    // Attribute helpers
    // --------------------------
    private static KeyValuePair<string, object>[] MakePolicyAttrs(Fin<(CartSnapshot Cart, IPaymentProvider Provider)> fin, IntentToPayCommand request) =>
        fin.Match(
            Succ: chk => new[]
            {
                KVP(Attr.Method, request.Method.ToString()),
                KVP(Attr.CartId, chk.Cart.Cart.Id),
                KVP(Attr.UserId, chk.Cart.CustomerId.Id),
                KVP(Attr.ItemsCount, chk.Cart.GetItemsAmount()),
                KVP(Attr.Country, chk.Cart.Country),
                KVP(Attr.Region, chk.Cart.Region),
                KVP(Attr.Idempotency, request.IdempotencyKey ?? string.Empty)
            },
            Fail: _ => []);

    private static KeyValuePair<string, object>[] ReserveAttrs(Fin<Unit> fin, Guid reservationId) =>
        fin.Match(
            Succ: _ => new[] { KVP(Attr.ReservationId, reservationId) },
            Fail: _ => []);

    private static IEnumerable<KeyValuePair<string, object>> PersistAttrs(Fin<FlowStatePlaceholder> fin) =>
        fin.Match(
            Succ: state => new[]
            {
                KVP(Attr.CartId, state.Intent.CartId),
                KVP(Attr.UserId, state.Intent.UserId),
                KVP(Attr.Provider, state.Intent.Provider),
                KVP(Attr.Amount, state.Intent.Amount.ToString("0.00")),
                KVP(Attr.Currency, state.Intent.Currency)
            },
            Fail: _ => []);

    private static KeyValuePair<string, object>[] ErrorAttrs<T>(Fin<T> fin) =>
        fin.Match(
            Succ: _ => [],
            Fail: e => new[] { KVP(Attr.Error, e.Message) }
        );

    // ---------------------------------
    // Your existing helpers (unchanged)
    // ---------------------------------

    // Placeholder to satisfy PersistAttrs signature reuse. (Could refactor PersistAttrs to accept intent directly.)
    public static FlowStatePlaceholder NewPlaceholder(PaymentIntent i) => new(i);

    public record FlowStatePlaceholder(PaymentIntent Intent);

    private static Money ComputeTotal(CartSnapshot cart, Money shipping, Money discounts, Money tax)
    {
        var preTax = new Money(cart.Subtotal.Currency, cart.Subtotal.Amount + shipping.Amount - discounts.Amount);
        return new Money(preTax.Currency, preTax.Amount + tax.Amount);
    }

    private static KeyValuePair<string, object> KVP(string k, object v) => new(k, v);
}

public static class OptionExtensions
{
    public static T IfNoneThrow<T>(this Option<T> opt, string? message = null) =>
        opt.Match(Some: v => v, None: () => throw new InvalidOperationException(message ?? "Expected Some but found None"));
}