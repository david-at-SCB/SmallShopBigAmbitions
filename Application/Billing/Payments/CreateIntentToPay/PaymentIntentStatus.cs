namespace SmallShopBigAmbitions.Application.Billing.Payments.CreatePaymentIntent;

public enum PaymentIntentStatus
{
    Pending,         // created, provider intent created
    RequiresAction,  // 3DS or similar
    Authorized,      // authorized but not captured
    Succeeded,       // captured/settled
    Canceled,
    Expired
}