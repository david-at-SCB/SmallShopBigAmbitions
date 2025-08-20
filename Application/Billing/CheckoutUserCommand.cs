using MediatR;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;

namespace SmallShopBigAmbitions.Application.Billing;

public record CheckoutUserCommand(Guid UserId, TrustedContext Context)
    : IRequest<Fin<UserCheckoutResult>>, IAuthorizedRequest;
