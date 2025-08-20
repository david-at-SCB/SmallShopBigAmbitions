using MediatR;
using SmallShopBigAmbitions.Auth;
using ServiceCart = SmallShopBigAmbitions.Business.Services.CartService.Cart; // Cant have a Cart namned cart apparently  

namespace SmallShopBigAmbitions.Application.Cart;


public record GetCartForUserQuery(Guid UserId, TrustedContext Context)
    : IRequest<Fin<ServiceCart>>, IAuthorizedRequest; 
