// Async/IO checklist (Razor Pages + LanguageExt)
// - Handlers return Task/Task<IActionResult>; always await RunTraceable(ct).RunAsync().
// - Avoid .Run(), .Result, .Wait() on IO/Aff in web code.
// - Accept and pass CancellationToken to all effects/HTTP calls.
// - Services: expose composable TraceableT/IO; add Task wrappers (e.g., GetXAsync) for UI.
// - Use Fin/Option for errors; handle via Match in handlers and return proper IActionResult.
// - Map DTO -> domain in a mapper; keep IO layer DTO-focused.
// - Ensure DI registers FunctionalHttpClient and service; no static singletons.
// - Add .WithLogging and telemetry attributes where useful.
using SmallShopBigAmbitions.Database;
using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.Monads.TraceableTransformer;

namespace SmallShopBigAmbitions.Business.Services;
public class UserService(IDataAccess DataAccess)
{
    private readonly IDataAccess _dataAccess = DataAccess;

    public TraceableT<Fin<Customer>> GetUserById(Guid userId) =>
        _dataAccess.GetCustomerById(userId);

    public TraceableT<Fin<Customer>> GetUserById(Customer customer) =>
        _dataAccess.GetCustomerById(customer.Id);
}
