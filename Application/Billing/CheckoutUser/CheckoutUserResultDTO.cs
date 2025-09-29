using SmallShopBigAmbitions.Models;

namespace SmallShopBigAmbitions.Application.Billing.CheckoutUser;

public record CheckoutUserResultDTO(
    CustomerId CustomerId,
    CartSnapshot Cart, 
    (bool Success, string Message) Validated
    );

//{
//    public Guid UserId { get; init; }
//    public Guid CartId { get; init; }
//    public required Fin<ChargeResult> Charged { get; init; }
//}