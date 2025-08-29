namespace SmallShopBigAmbitions.Application.Billing.Payments.CreateIntentToPay;

using LanguageExt;
using static LanguageExt.Prelude;

public interface IEventPublisher
{
    IO<Fin<Unit>> Publish<TEvent>(TEvent @event);
}

public sealed class NoopEventPublisher : IEventPublisher
{
    public IO<Fin<Unit>> Publish<TEvent>(TEvent @event) =>
        IO.lift<Fin<Unit>>(() => Fin<Unit>.Succ(unit));
}

public sealed record PaymentIntentCreatedEvent(
    Guid PaymentIntentId,
    Guid CartId,
    Guid UserId,
    string Provider,
    string ProviderIntentId,
    decimal Amount,
    string Currency
);