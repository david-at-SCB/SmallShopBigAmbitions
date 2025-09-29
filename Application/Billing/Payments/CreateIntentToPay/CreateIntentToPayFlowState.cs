namespace SmallShopBigAmbitions.Application.Billing.Payments.CreateIntentToPay;

using SmallShopBigAmbitions.Application._Abstractions;
using SmallShopBigAmbitions.Application.Billing.Payments.CreatePaymentIntent;
using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.Monads.TraceableTransformer;
using SmallShopBigAmbitions.Monads.TraceableTransformer.Extensions.FinLinq; // enable LINQ over TraceableT<Fin<T>>
using static SmallShopBigAmbitions.Application.Billing.Payments.CreateIntentToPay.CreateIntentToPayHandler;

// ---------------------------------
// Combinators for typed-stage chaining (extension methods must be top-level static)
// ---------------------------------
public static class Flow
{
    public static TraceableT<Fin<FlowState<PolicyOk>>> Start(
        TraceableT<Fin<(CartSnapshot Cart, IPaymentProvider Provider)>> policy,
        string span,
        Func<Fin<(CartSnapshot, IPaymentProvider)>, IEnumerable<KeyValuePair<string, object>>>? attrs = null) =>
        from p in policy.WithSpanName(span)
                         .WithAttributes(fin => attrs?.Invoke(fin) ?? Seq<KeyValuePair<string, object>>())
        select FlowState<PolicyOk>.FromPolicy(p);

    public static TraceableT<Fin<FlowState<TTo>>> Step<TFrom, TTo, S>(
        this TraceableT<Fin<FlowState<TFrom>>> flow,
        Func<FlowState<TFrom>, TraceableT<Fin<S>>> op,
        Func<FlowState<TFrom>, S, FlowState<TTo>> update,
        string span,
        Func<FlowState<TFrom>, Fin<S>, IEnumerable<KeyValuePair<string, object>>>? attrs = null) =>
        from st in flow
        from s in op(st)
                    .WithSpanName(span)
                    .WithAttributes(fin => attrs?.Invoke(st, fin) ?? Seq<KeyValuePair<string, object>>())
        select update(st, s);

    public static TraceableT<Fin<FlowState<TTo>>> StepPure<TFrom, TTo, S>(
        this TraceableT<Fin<FlowState<TFrom>>> flow,
        Func<FlowState<TFrom>, S> compute,
        Func<FlowState<TFrom>, S, FlowState<TTo>> update,
        string span,
        Func<FlowState<TFrom>, S, IEnumerable<KeyValuePair<string, object>>>? attrs = null) =>
        from st in flow
        let s = compute(st) // compute once
        from _ in TraceableTLifts.FromIOFin(IO.lift<Fin<S>>(() => FinSucc(s)), span)
                     .WithAttributes(_ => attrs?.Invoke(st, s) ?? Seq<KeyValuePair<string, object>>())
        select update(st, s);

    public static TraceableT<Fin<TOut>> Map<TStage, TOut>(
        this TraceableT<Fin<FlowState<TStage>>> flow,
        Func<FlowState<TStage>, TOut> map) =>
        from st in flow
        select map(st);

    // Helper to lift a state back into the monad (used by handler)
    public static TraceableT<Fin<FlowState<T>>> ToTraceable<T>(this FlowState<T> st) =>
        TraceableTLifts.FromIOFin(IO.lift<Fin<FlowState<T>>>(() => FinSucc(st)), "flow.state");
}

// ---------------------------------
// Flow state (typed by stage)
// ---------------------------------
public sealed record FlowState<TStage>(
    CartSnapshot Cart,
    IPaymentProvider Provider,
    Option<Money> Shipping,
    Option<Money> Discounts,
    Option<Money> Tax,
    Option<Money> Total,
    Option<Guid> ReservationId,
    Option<TimeSpan> Ttl,
    Option<ProviderIntent> ProviderIntent,
    Option<PaymentIntent> Intent
)
{
    public static FlowState<PolicyOk> FromPolicy((CartSnapshot Cart, IPaymentProvider Provider) p)
        => new(p.Cart, p.Provider, None, None, None, None, None, None, None, None);
}