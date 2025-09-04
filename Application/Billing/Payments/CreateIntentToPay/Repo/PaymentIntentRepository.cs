namespace SmallShopBigAmbitions.Application.Billing.Payments.CreatePaymentIntent.Repo;

using LanguageExt;
using Microsoft.Data.Sqlite;
using SmallShopBigAmbitions.Application._Abstractions;
using SmallShopBigAmbitions.Application.Billing.Payments.CreatePaymentIntent;
using SmallShopBigAmbitions.Database;
using SmallShopBigAmbitions.Monads;   // IOFin helpers
using SmallShopBigAmbitions.Monads.TraceableTransformer;
using static LanguageExt.Prelude;


public interface IPaymentIntentRepository
{
    TraceableT<IO<Fin<PaymentIntent>>> Insert(PaymentIntent intent);
    TraceableT<IO<Fin<Option<PaymentIntent>>>> GetById(Guid id);
    TraceableT<IO<Fin<Unit>>> Update(PaymentIntent intent);
    //TraceableT<IO<Fin<Option<PaymentIntent>>>> GetIdempotent(string idempotencyKey);
    //TraceableT<IO<Fin<Unit>>> SaveIdempotency(string idempotencyKey, Guid paymentIntentId);
}


public sealed class PaymentIntentRepository(IDataAccess dataAccess) : IPaymentIntentRepository
{
    private readonly IDataAccess _dataAccess = dataAccess;

    public TraceableT<IO<Fin<Option<PaymentIntent>>>> GetById(Guid id) =>
        _dataAccess.GetPaymentIntentById(id);

    public TraceableT<IO<Fin<Unit>>> Update(PaymentIntent intent) =>
        _dataAccess.UpdatePaymentIntent(intent);

    public TraceableT<IO<Fin<PaymentIntent>>> Insert(PaymentIntent intent) =>
        _dataAccess.InsertPaymentIntent(intent);

    public TraceableT<IO<Fin<Option<PaymentIntent>>>> GetIdempotent(string idempotencyKey)
    {
        throw new NotImplementedException();
    }

    public TraceableT<IO<Fin<Unit>>> SaveIdempotency(string idempotencyKey, Guid paymentIntentId)
    {
        throw new NotImplementedException();
    }
}