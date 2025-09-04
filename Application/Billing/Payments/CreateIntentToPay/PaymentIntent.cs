namespace SmallShopBigAmbitions.Application.Billing.Payments.CreatePaymentIntent;

using LanguageExt;

public sealed record PaymentIntent(
    Guid Id,
    Guid CartId, // change to depend on ORDER. So we gotta have an ORDER before we can have a PaymentIntent
    Guid UserId,
    string Provider,
    string ProviderIntentId,
    string Currency,
    decimal Amount,
    PaymentIntentStatus Status,
    Option<string> ClientSecret,
    Option<string> IdempotencyKey,
    Map<string, string> Metadata,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Option<DateTimeOffset> ExpiresAt,
    Guid ReservationId      // inventory reservation correlation
);
