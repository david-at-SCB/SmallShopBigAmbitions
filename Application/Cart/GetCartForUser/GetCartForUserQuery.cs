using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.FunctionalDispatcher;

namespace SmallShopBigAmbitions.Application.Cart.GetCartForUser;

public record GetCartForUserQuery(Guid UserId)
    : IFunctionalRequest<Models.Cart>;