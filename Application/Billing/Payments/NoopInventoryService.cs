using LanguageExt;
using SmallShopBigAmbitions.Application._Abstractions;
using SmallShopBigAmbitions.Models;
using static LanguageExt.Prelude;

namespace SmallShopBigAmbitions.Application.Billing.Payments;

public sealed class NoopInventoryService : IInventoryService
{
    public IO<Fin<Unit>> EnsureAvailable(Seq<CartLine> items) => IO.lift<Fin<Unit>>(() => Fin<Unit>.Succ(unit));
    public IO<Fin<Unit>> Release(Guid reservationId) => IO.lift<Fin<Unit>>(() => Fin<Unit>.Succ(unit));
    public IO<Fin<Unit>> Reserve(CartSnapshot cart, Guid reservationId, TimeSpan ttl) => IO.lift<Fin<Unit>>(() => Fin<Unit>.Succ(unit));
}
