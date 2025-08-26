using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Monads.TraceableTransformer;

namespace SmallShopBigAmbitions.Application.Billing.CheckoutUser;

public class CheckoutUserHandler : IFunctionalHandler<CheckoutUserCommand, CheckoutUserResultDTO>
{
    private readonly UserService _userService;
    private readonly ILogger<CheckoutUserHandler> _logger;

    public CheckoutUserHandler(UserService userService, ILogger<CheckoutUserHandler> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    public IO<Fin<CheckoutUserResultDTO>> Handle(CheckoutUserCommand request, TrustedContext context, CancellationToken ct)
    {
        var flow = _userService.CheckoutUserCart(request.UserId)
            .RequireTrusted(context) // prepend the guard combinator
            .WithSpanName("CheckoutUser")
            .WithLogging(_logger);

        return flow.RunTraceableFin(ct);
    }
}