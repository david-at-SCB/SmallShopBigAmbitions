namespace SmallShopBigAmbitions.Application.Billing.Payments;

using LanguageExt;
using LanguageExt.Common;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Application._Policy;
using SmallShopBigAmbitions.Monads.TraceableTransformer;
using static LanguageExt.Prelude;

public sealed record ApplyCreditCommand(Guid OrderId, string Code) : IFunctionalRequest<Unit>;

public static class ApplyCreditValidator
{
    public static Validation<Seq<Error>, Unit> Validate(ApplyCreditCommand cmd, TrustedContext ctx) =>
        RuleCombiner.Apply(
            Rule.From("auth", () => ctx.IsAuthenticated, ErrorCodes.Auth_Unauthorized)
        );
}

public sealed class ApplyCreditPolicy : IAuthorizationPolicy<ApplyCreditCommand>
{
    public Fin<Unit> Authorize(ApplyCreditCommand request, TrustedContext context) =>
        ApplyCreditValidator.Validate(request, context).ToFin();
}

public interface ICreditService
{
    IO<Fin<Unit>> Apply(Guid orderId, string code);
}

public sealed class ApplyCreditHandler(
    ICreditService credits
) : IFunctionalHandler<ApplyCreditCommand, Unit>
{
    private readonly ICreditService _credits = credits;

    public IO<Fin<Unit>> Handle(ApplyCreditCommand request, TrustedContext context, CancellationToken ct)
    {
        var flow = TraceableTLifts
            .FromIOFin(_credits.Apply(request.OrderId, request.Code), "order.apply_credit");

        return flow.RunTraceable(ct);
    }
}
