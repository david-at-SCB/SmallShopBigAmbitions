namespace SmallShopBigAmbitions.Application.Billing.Payments.CreateIntentToPay;

using LanguageExt;
using SmallShopBigAmbitions.Application.Billing.Payments.CreatePaymentIntent;
using SmallShopBigAmbitions.Application.Billing.Payments.CreatePaymentIntent.Observability;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.Monads.TraceableTransformer;
using System.Diagnostics;
using static LanguageExt.Prelude;


/// <summary>
/// CreateIntentToPayHandler orchestrates the payment-intent creation as a
/// composable, traced, and effectful pipeline:
/// - Each step is a TraceableT of IO<Fin<T>> so we trace spans and keep structured failures.
/// - The "primary function stack" is expressed via Bind to keep the happy-path linear.
/// - Attributes for spans are built in dedicated helpers to avoid heavy closures and reduce allocations.
/// - Failures short-circuit via Fin and are propagated without throwing.
/// </summary>
public sealed class CreateIntentToPayHandler(
    CreateIntentToPayPolicy policy,
    IPricingService pricing,
    IInventoryService inventory,
    IPaymentIntentRepository repo,
    IIdempotencyStore idempotency,
    IFunctionalDispatcher dispatcher,
    IEventPublisher events) : IFunctionalHandler<CreateIntentToPayCommand, IntentToPayDto>
{
    private readonly CreateIntentToPayPolicy _policy = policy;
    private readonly IPricingService _pricing = pricing;
    private readonly IInventoryService _inventory = inventory;
    private readonly IPaymentIntentRepository _repo = repo;
    private readonly IIdempotencyStore _idempotency = idempotency;
    private readonly IFunctionalDispatcher _dispatcher = dispatcher;
    private readonly IEventPublisher _events = events;

    /// <summary>
    /// Immutable state carried between steps. This avoids recomputation and keeps Bind bodies small.
    /// </summary>
    private sealed record FlowState(
        CartSnapshot Cart,
        IPaymentProvider Provider,
        Money? Total = null,
        Guid? ReservationId = null,
        TimeSpan? Ttl = null,
        ProviderIntent? ProviderIntent = null,
        PaymentIntent? Intent = null
    );

    /// <summary>
    /// Primary function stack: authorization -> idempotency pre-check -> policy check
    /// -> totals -> reserve -> provider intent -> persist -> save idempotency -> publish -> DTO.
    /// Each step is traced and returns Fin; the first failure short-circuits the pipeline.
    /// </summary>
    public IO<Fin<IntentToPayDto>> Handle(CreateIntentToPayCommand request, TrustedContext context, CancellationToken ct)
    {
        var flow =
            RequireTrustedT(context) // Authorization check
            .Bind(authFin => authFin.Match(
                Succ: _ => IdempotencyCheckT(request),
                Fail: e => TraceableTLifts.FromFin(Fin<Unit>.Fail(e), "auth.failed", fin => ErrorAttrs(fin))
            ))
            .Bind(_ => PolicyCheckStep(request)) // Ensure cart + provider + inventory are valid for processing
            .Bind(stateFin => stateFin.Match(
                Succ: state => TotalsStep(state), // Shipping, discounts, tax -> Total
                Fail: e => TraceableTLifts.FromFin(Fin<FlowState>.Fail(e), ActivityNames.PricingCalculate + ".skip", fin => ErrorAttrs(fin))
            ))
            .Bind(stateFin => stateFin.Match(
                Succ: state => ReserveInventoryStep(state), // Reserve inventory for TTL
                Fail: e => TraceableTLifts.FromFin(Fin<FlowState>.Fail(e), ActivityNames.InventoryReserve + ".skip", fin => ErrorAttrs(fin))
            ))
            .Bind(stateFin => stateFin.Match(
                Succ: state => ProviderIntentStep(request, state), // Ask provider (e.g., Stripe) to create its intent
                Fail: e => TraceableTLifts.FromFin(Fin<FlowState>.Fail(e), ActivityNames.ProviderCreateIntent + ".skip", fin => ErrorAttrs(fin))
            ))
            .Bind(stateFin => stateFin.Match(
                Succ: state => PersistIntentStep(request, state), // Persist our domain PaymentIntent
                Fail: e => TraceableTLifts.FromFin(Fin<FlowState>.Fail(e), ActivityNames.PersistPaymentIntent + ".skip", fin => ErrorAttrs(fin))
            ))
            .Bind(stateFin => stateFin.Match(
                Succ: state => SaveIdempotencyStep(request, state), // Store idempotency key -> intent id
                Fail: e => TraceableTLifts.FromFin(Fin<FlowState>.Fail(e), "idempotency.save.skip", fin => ErrorAttrs(fin))
            ))
            .Bind(stateFin => stateFin.Match(
                Succ: state => PublishEventStep(state), // Notify other bounded contexts/services
                Fail: e => TraceableTLifts.FromFin(Fin<FlowState>.Fail(e), "event.publish.skip", fin => ErrorAttrs(fin))
            ))
            .Map(stateFin => stateFin.Match( // Project to a thin DTO for the outside world
                Succ: state => Fin<IntentToPayDto>.Succ(Mapping.ToDto(state.Intent!, state.ProviderIntent!.ProviderMetadata)),
                Fail: e => Fin<IntentToPayDto>.Fail(e)
            ));

        return flow.RunTraceable(ct);
    }

    // ---------------- Steps (each returns a traced IO<Fin<...>>) ----------------

    /// <summary>Require authenticated caller; adds a span and returns Fin<Unit>.</summary>
    private static TraceableT<Fin<Unit>> RequireTrustedT(TrustedContext context) =>
        TraceableTLifts.FromIOFinRawTracableT(
            AuthorizationGuards.RequireTrustedFin(context),
            spanName: "auth.require_trusted");

    /// <summary>Idempotency pre-check. If key exists → fail; else succeed.</summary>
    private TraceableT<Fin<Unit>> IdempotencyCheckT(CreateIntentToPayCommand request) =>
        TraceableTLifts.FromIOFinRawTracableT(
            TryGetIdempotent(request),
            spanName: "idempotency.check");

    /// <summary>Validate cart snapshot, provider resolution, and inventory availability.</summary>
    private TraceableT<Fin<FlowState>> PolicyCheckStep(CreateIntentToPayCommand request) =>
        TraceableTLifts
            .FromIOFinRawTracableT(_policy.Check(request), ActivityNames.PaymentCreateIntent)
            .WithAttributes(fin => MakePolicyAttrs(fin, request))
            .Map(checkFin => checkFin.Map(chk => new FlowState(chk.Cart, chk.Provider)));

    /// <summary>Compute totals: shipping + discounts + tax. Adds pricing spans and tags totals on success.</summary>
    private TraceableT<Fin<FlowState>> TotalsStep(FlowState state) =>
        TraceableTLifts
            .FromIOFinRawTracableT(_pricing.CalculateShipping(state.Cart), ActivityNames.PricingCalculate + ".shipping")
            .Bind(shipFin => shipFin.Match(
                Succ: (Money shipping) => TraceableTLifts
                    .FromIOFinRawTracableT(_pricing.CalculateDiscounts(state.Cart), ActivityNames.PricingCalculate + ".discounts")
                    .Bind(disFin => disFin.Match(
                        Succ: (Money discounts) =>
                            TraceableTLifts.FromIOFinRawTracableT(
                                _pricing.CalculateTaxes(state.Cart, new Money(state.Cart.Subtotal.Currency, state.Cart.Subtotal.Amount + shipping.Amount - discounts.Amount)),
                                ActivityNames.PricingCalculate + ".tax"
                            )
                            .Map(taxFin => taxFin.Map((Money tax) => ComputeTotal(state.Cart, shipping, discounts, tax)))
                            .WithAttributes(TotalAttrs),
                        Fail: e => TraceableTLifts.FromFin(Fin<Money>.Fail(e), ActivityNames.PricingCalculate + ".discounts.fail", fin => ErrorAttrs(fin))
                    )),
                Fail: e => TraceableTLifts.FromFin(Fin<Money>.Fail(e), ActivityNames.PricingCalculate + ".shipping.fail", fin => ErrorAttrs(fin))
            ))
            .Map(totalFin => totalFin.Map(total => state with { Total = total }));

    /// <summary>Reserve inventory for a TTL; attach reservation id as a span tag.</summary>
    private TraceableT<Fin<FlowState>> ReserveInventoryStep(FlowState state)
    {
        var reservationId = Guid.NewGuid();
        var ttl = TimeSpan.FromMinutes(15);
        return TraceableTLifts.FromIOFinRawTracableT(_inventory.Reserve(state.Cart.Items, reservationId, ttl), ActivityNames.InventoryReserve)
            .WithAttributes(fin => ReserveAttrs(fin, reservationId))
            .Map(resFin => resFin.Map(_ => state with { ReservationId = reservationId, Ttl = ttl }));
    }

    /// <summary>Create provider-specific intent (e.g., Stripe). Maps failures to ProviderFailed.</summary>
    private static TraceableT<Fin<FlowState>> ProviderIntentStep(CreateIntentToPayCommand request, FlowState state) =>
        TraceableTLifts
            .FromIOFinRawTracableT(
                state.Total is null
                    ? IO.lift<Fin<ProviderIntent>>(() => Fin<ProviderIntent>.Fail(PaymentErrors.PricingFailed))
                    : state.Provider.CreateIntent(new ProviderIntentRequest(
                        Description: $"Cart {state.Cart.CartId}",
                        Amount: state.Total,
                        PaymentIntentId: Guid.NewGuid(),
                        CartId: state.Cart.CartId,
                        UserId: state.Cart.UserId,
                        Method: request.Method,
                        Metadata: request.Metadata
                      )),
                ActivityNames.ProviderCreateIntent)
            .Map(piFin => piFin.Match(
                Succ: pi => Fin<FlowState>.Succ(state with { ProviderIntent = pi }),
                Fail: _ => Fin<FlowState>.Fail(PaymentErrors.ProviderFailed)
            ));

    /// <summary>Persist our domain PaymentIntent and tag persisted properties on success.</summary>
    private TraceableT<Fin<FlowState>> PersistIntentStep(CreateIntentToPayCommand request, FlowState state)
    {
        var intent = new PaymentIntent(
            Id: Guid.NewGuid(),
            CartId: state.Cart.CartId,
            UserId: state.Cart.UserId,
            Provider: state.Provider.Name,
            ProviderIntentId: state.ProviderIntent!.ProviderIntentId,
            Currency: state.ProviderIntent.Amount.Currency,
            Amount: state.ProviderIntent.Amount.Amount,
            Status: PaymentIntentStatus.Pending,
            ClientSecret: Some(state.ProviderIntent.ClientSecret),
            IdempotencyKey: Optional(request.IdempotencyKey),
            Metadata: request.Metadata,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow,
            ExpiresAt: DateTimeOffset.UtcNow.Add(state.Ttl ?? TimeSpan.FromMinutes(15)),
            ReservationId: state.ReservationId ?? Guid.Empty
        );

        return TraceableTLifts.FromIOFinRawTracableT(_repo.Insert(intent), ActivityNames.PersistPaymentIntent)
            .Map(insFin => insFin.Map(_ => state with { Intent = intent }))
            .WithAttributes(flowState => PersistAttrs(flowState, intent));
    }

    /// <summary>Store idempotency key → intent id mapping if provided; no-op otherwise.</summary>
    private TraceableT<Fin<FlowState>> SaveIdempotencyStep(CreateIntentToPayCommand request, FlowState state) =>
        request.IdempotencyKey is null
            ? TraceableTLifts.FromFin(Fin<Unit>.Succ(unit), "idempotency.save.skip", fin => ErrorAttrs(fin))
                .Map(_ => Fin<FlowState>.Succ(state))
            : TraceableTLifts.FromIOFinRawTracableT(_idempotency.Put(request.IdempotencyKey, state.Intent!.Id), "idempotency.save")
                .Map(_ => Fin<FlowState>.Succ(state));

    /// <summary>Publish an integration/domain event for downstream consumers.</summary>
    private TraceableT<Fin<FlowState>> PublishEventStep(FlowState state) =>
        TraceableTLifts
            .FromIOFinRawTracableT(
                _events.Publish(new PaymentIntentCreatedEvent(
                    state.Intent!.Id,
                    state.Intent.CartId,
                    state.Intent.UserId,
                    state.Intent.Provider,
                    state.Intent.ProviderIntentId,
                    state.Intent.Amount,
                    state.Intent.Currency
                )),
                spanName: "event.publish.payment_intent_created")
            .Map(_ => Fin<FlowState>.Succ(state));


    // ---------------- Attribute builders and small helpers ----------------

    private static KeyValuePair<string, object>[] ErrorAttrs<T>(Fin<T> fin) =>
        fin.Match(
            Succ: _ => [],
            Fail: e => new[] { new KeyValuePair<string, object>(Attr.Error, e.Message) }
        );

    private static Money ComputeTotal(CartSnapshot cart, Money shipping, Money discounts, Money tax)
    {
        var preTax = new Money(cart.Subtotal.Currency, cart.Subtotal.Amount + shipping.Amount - discounts.Amount);
        return new Money(preTax.Currency, preTax.Amount + tax.Amount);
    }

    private static IEnumerable<KeyValuePair<string, object>> MakePolicyAttrs(Fin<(CartSnapshot Cart, IPaymentProvider Provider)> fin, CreateIntentToPayCommand request) =>
        fin.Match(
            Succ: chk =>
            [
                new KeyValuePair<string, object>(Attr.Method, request.Method.ToString()),
                new KeyValuePair<string, object>(Attr.CartId, chk.Cart.CartId),
                new KeyValuePair<string, object>(Attr.UserId, chk.Cart.UserId),
                new KeyValuePair<string, object>(Attr.ItemsCount, chk.Cart.Items.Count),
                new KeyValuePair<string, object>(Attr.Country, chk.Cart.Country),
                new KeyValuePair<string, object>(Attr.Region, chk.Cart.Region),
                new KeyValuePair<string, object>(Attr.Idempotency, request.IdempotencyKey ?? string.Empty)
            ],
            Fail: _ => Enumerable.Empty<KeyValuePair<string, object>>()
        );

    private static IEnumerable<KeyValuePair<string, object>> TotalAttrs(Fin<Money> fin) =>
        fin.Match(
            Succ: t =>
            [
                new KeyValuePair<string, object>(Attr.Currency, t.Currency),
                new KeyValuePair<string, object>(Attr.Amount, t.Amount.ToString("0.00"))
            ],
            Fail: _ => Enumerable.Empty<KeyValuePair<string, object>>()
        );

    private static IEnumerable<KeyValuePair<string, object>> ReserveAttrs(Fin<Unit> fin, Guid reservationId) =>
        fin.Match(
            Succ: _ => [new KeyValuePair<string, object>(Attr.ReservationId, reservationId)],
            Fail: _ => Enumerable.Empty<KeyValuePair<string, object>>()
        );

    private static IEnumerable<KeyValuePair<string, object>> PersistAttrs(Fin<FlowState> fin, PaymentIntent intent) =>
        fin.Match(
            Succ: _ =>
            [
                new KeyValuePair<string, object>(Attr.CartId, intent.CartId),
                new KeyValuePair<string, object>(Attr.UserId, intent.UserId),
                new KeyValuePair<string, object>(Attr.Provider, intent.Provider),
                new KeyValuePair<string, object>(Attr.Amount, intent.Amount.ToString("0.00")),
                new KeyValuePair<string, object>(Attr.Currency, intent.Currency)
            ],
            Fail: _ => Enumerable.Empty<KeyValuePair<string, object>>()
        );

    private IO<Fin<Unit>> TryGetIdempotent(CreateIntentToPayCommand cmd) =>
        cmd.IdempotencyKey == null
            ? IO.lift<Fin<Unit>>(() => Fin<Unit>.Succ(unit))
            : _idempotency.TryGet(cmd.IdempotencyKey)
                .Map(fin => fin.Bind(opt => opt.Match(
                    Some: _ => Fin<Unit>.Fail(PaymentErrors.IdempotentConflict),
                    None: () => Fin<Unit>.Succ(unit)
                )));
}
