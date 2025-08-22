using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Monads.TraceableTransformer;

namespace SmallShopBigAmbitions.Application.Billing.CheckoutUser;

public class CheckoutUserHandler : IFunctionalHandler<CheckoutUserCommand, UserCheckoutResult>
{
    private readonly UserService _userService;
    private readonly ILogger<CheckoutUserHandler> _logger;

    public CheckoutUserHandler(UserService userService, ILogger<CheckoutUserHandler> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    public IO<Fin<UserCheckoutResult>> Handle(CheckoutUserCommand request, TrustedContext context, CancellationToken ct)
    {
        var flow =
            from _ in TraceableTLifts.FromIO<Unit>(
                AuthorizationGuards.RequireTrustedORThrow(context),
                "RequireTrusted"
            )
            from result in _userService.CheckoutUserCart(request.UserId)
            select result;

        return flow
            .WithSpanName("CheckoutUser")
            .WithLogging(_logger)
            .RunTraceableFin(ct);
    }
}