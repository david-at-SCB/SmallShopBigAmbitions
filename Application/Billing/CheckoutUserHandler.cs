using MediatR;
using SmallShopBigAmbitions.Business.Services;

namespace SmallShopBigAmbitions.Application.Billing;

public class CheckoutUserHandler : IRequestHandler<CheckoutUserCommand, Fin<UserCheckoutResult>>
{
    private readonly UserService _userService;
    private readonly ILogger<CheckoutUserHandler> _logger;

    public CheckoutUserHandler(UserService userService, ILogger<CheckoutUserHandler> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    public Task<Fin<UserCheckoutResult>> Handle(CheckoutUserCommand request, CancellationToken ct)
    {
        // Compose the workflow and run it; wrap exceptions into Fin.Fail
        try
        {
            var traceable = _userService.CheckoutUserCart(request.UserId);
            var result = traceable.RunTraceable(ct).Run();
            // these tasks should all be IO<A> no ?? keep it functional!
            return Task.FromResult(Fin<UserCheckoutResult>.Succ(result));
        }
        catch (Exception ex)
        {
            // these tasks should all be IO<A> no ?? keep it functional!
            return Task.FromResult(Fin<UserCheckoutResult>.Fail(Error.New(ex.Message)));
        }
    }
}
