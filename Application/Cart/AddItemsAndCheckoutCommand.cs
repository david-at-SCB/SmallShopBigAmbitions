using MediatR;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;

namespace SmallShopBigAmbitions.Application.Cart;

public record AddItemsAndCheckoutCommand(Guid UserId, IEnumerable<string> Items, TrustedContext Context)
    : IRequest<Fin<UserCheckoutResult>>, IAuthorizedRequest;
