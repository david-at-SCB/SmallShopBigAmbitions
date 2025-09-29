namespace SmallShopBigAmbitions.Application.Billing.Payments.CreateIntentToPay;

using SmallShopBigAmbitions.Models;
using LanguageExt;
using LanguageExt.Common;
using SmallShopBigAmbitions.Application.Billing.Payments.CreatePaymentIntent;
using static LanguageExt.Prelude;
using SmallShopBigAmbitions.Monads.LanguageExtExtensions;
using SmallShopBigAmbitions.Application._Abstractions;
using SmallShopBigAmbitions.Application._Policy;
using SmallShopBigAmbitions.Auth;

/// <summary>
/// Functional validation rules for the <see cref="CreateIntentToPayPolicy"/>.
/// Pure (no I/O) - converts domain preconditions into a LanguageExt Validation so we   
/// can collect multiple errors before failing the policy (if desired in future).
/// </summary>
public static class CreateIntentToPayValidator
{
    /// <summary>
    /// Validate the inbound command against cheap, synchronous domain rules.
    /// (No provider lookups, inventory checks, etc. – those happen later.)
    /// </summary>
    public static Validation<Seq<Error>, Unit> Validate(IntentToPayCommand cmd, CartSnapshot cart) =>
        RuleCombiner.Apply(
            Rule.From("method_present", () => cmd.Method != PaymentMethod.Unknown, ErrorCodes.Payment_Intent_MethodRequired),
            Rule.From("cart_non_empty", () => cart.CartIsNotEmpty(), ErrorCodes.Payment_Intent_CartEmpty)
        );
}

/// <summary>
/// Helper lifts bridging <see cref="Validation"/> into <see cref="Fin"/> so the rest of the pipeline
/// can remain a single monadic shape (Fin) until we deliberately re-wrap with IO.
/// </summary>
internal static class FinIoLifts
{
    /// <summary>
    /// Convert a Validation into a Fin (first error semantics). If you need full error aggregation
    /// you could extend the tuple result instead of collapsing messages into one string.
    /// </summary>
    public static Fin<T> ToFin<T>(this Validation<Seq<Error>, T> validation) =>
        validation.Match(
            Fail: errs => Fin<T>.Fail(Error.New(string.Join("; ", errs.Map(e => e.Message)) )),
            Succ: v => Fin<T>.Succ(v));
}

/// <summary>
/// Authorization + pre‑condition policy executed before the main payment intent creation flow.
/// Responsibilities:
///  1. Authorize caller (cheap synchronous check).
///  2. Load immutable <see cref="CartSnapshot"/>.
///  3. Run pure validation rules.
///  4. Resolve payment provider for the requested method.
///  5. Ensure inventory availability for all cart lines.
///  6. Return the (CartSnapshot, Provider) pair to seed the downstream intent pipeline.
///
/// Design notes:
///  * Each repository/service call returns IO&lt;Fin&lt;T&gt;&gt; (effect + fallible result).
///  * For clarity we materialize (Run) each IO one time inside a single outer IO.lift
///    then compose the Fins with a LINQ comprehension. This keeps the comprehension noise‑free.
///  * If you need to preserve lazy effect sequencing or structured tracing at each stage,
///    replace this with a fully monadic IO chain instead of running inside IO.lift.
///  * Failure short‑circuits at the first failing Fin – which matches our intent to avoid
///    expensive provider / inventory calls when early checks fail.
/// </summary>
public sealed class CreateIntentToPayPolicy : IAuthorizationPolicy<IntentToPayCommand>
{
    private readonly ICartQueries _carts;
    private readonly IPaymentProviderSelector _providers;
    private readonly IInventoryService _inventory;

    public CreateIntentToPayPolicy(ICartQueries carts, IPaymentProviderSelector providers, IInventoryService inventory) =>
        (_carts, _providers, _inventory) = (carts, providers, inventory);

    /// <summary>
    /// Synchronous authorization: trusts <see cref="TrustedContext"/> already populated by the host.
    /// Cheap and side‑effect free. Returns Fin instead of bool to unify error representation.
    /// </summary>
    public Fin<Unit> Authorize(IntentToPayCommand request, TrustedContext context) =>
        (context.IsAuthenticated || context.CallerId != Guid.Empty)
            ? FinSucc(Unit.Default)
            : FinFail<Unit>(Error.New("payment.intent.unauthorized"));

    /// <summary>
    /// Full policy check returning an (immutable) cart snapshot and resolved payment provider.
    /// - Uses a LINQ comprehension over Fin after eagerly running each IO effect once.
    /// - Short‑circuits on first Fin failure (cart load, validation, provider resolution, inventory check).
    /// - Returned IO contains no further side effects; executing it is idempotent.
    ///
    /// Trade‑offs: Running IO effects eagerly inside IO.lift loses the ability to interleave additional
    /// tracing at each stage. If richer tracing / deferred execution is required, refactor to chain
    /// each IO<Fin<T>>; directly without Run() until the outer pipeline evaluation.
    /// </summary>
    public IO<Fin<(CartSnapshot Cart, IPaymentProvider Provider)>> Check(IntentToPayCommand cmd) =>
        IO.lift<Fin<(CartSnapshot, IPaymentProvider)>>(() =>
            from cart in _carts.GetCart(cmd.CartId).Run()                     // Fin<CartSnapshot>: load snapshot
            from _1 in CreateIntentToPayValidator.Validate(cmd, cart).ToFin() // Fin<Unit>: pure validation
            from provider in _providers.Resolve(cmd.Method)                   // Fin<IPaymentProvider>: resolve provider
            from _2 in _inventory.EnsureAvailable(cart.Lines).Run()           // Fin<Unit>: inventory availability
            select (cart, provider)                                           // Final pair fed into intent flow
        );
}