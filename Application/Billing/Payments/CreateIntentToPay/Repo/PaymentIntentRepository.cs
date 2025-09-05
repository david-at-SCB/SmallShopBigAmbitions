namespace SmallShopBigAmbitions.Application.Billing.Payments.CreatePaymentIntent.Repo;

using LanguageExt;
using SmallShopBigAmbitions.Application.Billing.Payments.CreatePaymentIntent;
using SmallShopBigAmbitions.Database;
using SmallShopBigAmbitions.Monads.TraceableTransformer;


public interface IPaymentIntentRepository
{
    TraceableT<Fin<PaymentIntent>> Insert(PaymentIntent intent);
    TraceableT<Fin<Option<PaymentIntent>>> GetById(Guid id);
    TraceableT<Fin<Unit>> Update(PaymentIntent intent);
}


public sealed class PaymentIntentRepository(IDataAccess dataAccess) : IPaymentIntentRepository
{
    private readonly IDataAccess _dataAccess = dataAccess;

    public TraceableT<Fin<Option<PaymentIntent>>> GetById(Guid id) =>
        _dataAccess.GetPaymentIntentById(id);

    public TraceableT<Fin<Unit>> Update(PaymentIntent intent) =>
        _dataAccess.UpdatePaymentIntent(intent);

    public TraceableT<Fin<PaymentIntent>> Insert(PaymentIntent intent) =>
        _dataAccess.InsertPaymentIntent(intent);
}