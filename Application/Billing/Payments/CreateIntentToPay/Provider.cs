namespace SmallShopBigAmbitions.Application.Billing.Payments.CreatePaymentIntent;

using LanguageExt;
using SmallShopBigAmbitions.Application._Abstractions;
using static LanguageExt.Prelude;

public sealed class PaymentProviderSelector : IPaymentProviderSelector
{
    private readonly Map<PaymentMethod, IPaymentProvider> _map;
    private readonly ILogger<PaymentProviderSelector> _logger;

    public PaymentProviderSelector(IEnumerable<IPaymentProvider> providers, ILogger<PaymentProviderSelector> logger)
    {
        _logger = logger;
        var map = Map<PaymentMethod, IPaymentProvider>();
        foreach (var p in providers)
        {
            // naive mapping: stripe -> Card; extend with capabilities metadata if needed
            if (p.Name.Equals("stripe", StringComparison.OrdinalIgnoreCase))
            {
                map = map.Add(PaymentMethod.Card, p);
            }
        }
        _map = map;
        if (_map.IsEmpty)
        {
            _logger.LogWarning("No payment providers registered; intents will fail with payment.method_unsupported");
        }
        else
        {
            List<string> list = [];
            foreach (var (Key, Value) in _map) // pair is (PaymentMethod key, IPaymentProvider value)
            {
                list.Add(Key + "->" + Value.Name);
            }
            var entries = string.Join(", ", list);
            _logger.LogInformation("Payment provider map initialized: {Entries}", entries);
        }
    }

    public Fin<IPaymentProvider> Resolve(PaymentMethod method)
    {
        var fin = _map.Find(method).ToFin(PaymentErrors.MethodNotSupported);
        if (fin.IsFail)
        {
            _logger.LogWarning("Payment provider resolve failed for method {Method}", method);
        }
        return fin;
    }
}
