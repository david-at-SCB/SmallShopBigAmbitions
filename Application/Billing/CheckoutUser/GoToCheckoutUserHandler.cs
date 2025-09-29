using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.Monads.TraceableTransformer;
using SmallShopBigAmbitions.Monads.TraceableTransformer.Extensions.BaseLinq;
using System.Xml;

namespace SmallShopBigAmbitions.Application.Billing.CheckoutUser;

public class GoToCheckoutUserHandler(
    ICartService CartService) 
    : IFunctionalHandler<GoToCheckoutUserCommand, CheckoutUserResultDTO>
{
    public IO<Fin<CheckoutUserResultDTO>> Handle(GoToCheckoutUserCommand request, TrustedContext context, CancellationToken ct)
    {
        var cartFin = request.Cart.ValidateForCharge(request.Customer);

        var resultFin = cartFin.Match(
            Succ: snapshot => Fin<CheckoutUserResultDTO>.Succ(MapToDTO(snapshot, request)),
            Fail: error => Fin<CheckoutUserResultDTO>.Fail(error) // propagate validation errors, will be logged below
        );

        resultFin.Match(
            Succ: dto => CartService.SaveCart(dto.Cart), // Persist only valid cart snapshot
            Fail: error => CartService.LogFailedCheckoutAttempt(error, request.Customer) 
        );

        return IO.lift<Fin<CheckoutUserResultDTO>>(resultFin);
    }

    private CheckoutUserResultDTO MapToDTO(CartSnapshot cart, GoToCheckoutUserCommand request)
    {
        var Message = cart.Valid ? "Checkout successful" : string.Join("; ", cart.Errors);
        var success = cart.Valid;

        var newDTO = new CheckoutUserResultDTO(
            CustomerId: cart.CustomerId,
            Cart: cart,
            Validated: (success, Message)
        );

        return newDTO;
    }
}