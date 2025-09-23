using LanguageExt;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Application._Abstractions;
using SmallShopBigAmbitions.Application._Policy;
using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.Database;
using static LanguageExt.Prelude;
using SmallShopBigAmbitions.Monads.Traceable;
using SmallShopBigAmbitions.Monads.TraceableTransformer;
using SmallShopBigAmbitions.Application._PipelineBehaviours; // Added for ITraceEnrichment

namespace SmallShopBigAmbitions.Database.Commands;

// Commands (now enriched for observability + result enrichment)
public sealed record AddCartLineCommand(Guid CartId, Guid UserId, Guid ProductId, int Quantity, Money UnitPrice)
    : IFunctionalRequest<CartSnapshot>, ITraceResultEnrichment<CartSnapshot>
{
    public string? GetSpanName() => "cart.add_line";
    public IEnumerable<(string Key, object? Value)> GetTraceAttributes()
    {
        yield return ("cart.id", CartId);
        yield return ("user.id", UserId);
        yield return ("product.id", ProductId);
        yield return ("quantity", Quantity);
        yield return ("unit.price.amount", UnitPrice.Amount);
        yield return ("unit.price.currency", UnitPrice.Currency);
    }
    // Fix for CS8602: Dereference of a possibly null reference.
    // Update all usages of 'snap.Items.Count' and 'snap.Subtotal.Amount' to null-check 'snap' before dereferencing.

    public IEnumerable<(string Key, object? Value)> GetResultAttributes(Fin<CartSnapshot> result)
    {
        yield return ("result.status", result.IsSucc ? "success" : "failure");
        if (result.IsSucc)
        {
            var snap = result.Match(s => s, _ => default(CartSnapshot));
            if (snap != null)
            {
                yield return ("cart.items.count", snap.Items.Count);
                yield return ("cart.subtotal", snap.Subtotal.Amount);
            }
        }
        else
        {
            var msg = result.Match(_ => null as string, e => e.Message);
            if (msg is not null)
                yield return ("error.message", msg);
        }
    }
}

public sealed record SetCartLineQuantityCommand(Guid CartId, Guid UserId, Guid ProductId, int Quantity)
    : IFunctionalRequest<CartSnapshot>, ITraceResultEnrichment<CartSnapshot>
{
    public string? GetSpanName() => "cart.set_line_quantity";
    public IEnumerable<(string Key, object? Value)> GetTraceAttributes()
    {
        yield return ("cart.id", CartId);
        yield return ("user.id", UserId);
        yield return ("product.id", ProductId);
        yield return ("quantity", Quantity);
    }
    public IEnumerable<(string Key, object? Value)> GetResultAttributes(Fin<CartSnapshot> result)
    {
        yield return ("result.status", result.IsSucc ? "success" : "failure");
        if (result.IsSucc)
        {
            var snap = result.Match(s => s, _ => default(CartSnapshot));
            if (snap != null)
            {
                yield return ("cart.items.count", snap.Items.Count);
                yield return ("cart.subtotal", snap.Subtotal.Amount);
            }
        }
        else
        {
            var msg = result.Match(_ => null as string, e => e.Message);
            if (msg is not null)
                yield return ("error.message", msg);
        }
    }
}

public sealed record RemoveCartLineCommand(Guid CartId, Guid UserId, Guid ProductId)
    : IFunctionalRequest<CartSnapshot>, ITraceResultEnrichment<CartSnapshot>
{
    public string? GetSpanName() => "cart.remove_line";
    public IEnumerable<(string Key, object? Value)> GetTraceAttributes()
    {
        yield return ("cart.id", CartId);
        yield return ("user.id", UserId);
        yield return ("product.id", ProductId);
    }
    public IEnumerable<(string Key, object? Value)> GetResultAttributes(Fin<CartSnapshot> result)
    {
        yield return ("result.status", result.IsSucc ? "success" : "failure");
        if (result.IsSucc)
        {
            var snap = result.Match(s => s, _ => default(CartSnapshot));
            if (snap != null)
            {
                yield return ("cart.items.count", snap.Items.Count);
                yield return ("cart.subtotal", snap.Subtotal.Amount);
            }
        }
        else
        {
            var msg = result.Match(_ => null as string, e => e.Message);
            if (msg is not null)
                yield return ("error.message", msg);
        }
    }
}

public sealed record ClearCartCommand(Guid CartId, Guid UserId)
    : IFunctionalRequest<CartSnapshot>, ITraceResultEnrichment<CartSnapshot>
{
    public string? GetSpanName() => "cart.clear";
    public IEnumerable<(string Key, object? Value)> GetTraceAttributes()
    {
        yield return ("cart.id", CartId);
        yield return ("user.id", UserId);
    }
    public IEnumerable<(string Key, object? Value)> GetResultAttributes(Fin<CartSnapshot> result)
    {
        yield return ("result.status", result.IsSucc ? "success" : "failure");
        if (result.IsSucc)
        {
            var snap = result.Match(s => s, _ => default(CartSnapshot));
            if (snap != null)
            {
                yield return ("cart.items.count", snap.Items.Count);
                yield return ("cart.subtotal", snap.Subtotal.Amount);
            }
        }
        else
        {
            var msg = result.Match(_ => null as string, e => e.Message);
            if (msg is not null)
                yield return ("error.message", msg);
        }
    }
}

// Policy
public sealed class CartMutationPolicy : IAuthorizationPolicy<AddCartLineCommand>,
                                         IAuthorizationPolicy<SetCartLineQuantityCommand>,
                                         IAuthorizationPolicy<RemoveCartLineCommand>,
                                         IAuthorizationPolicy<ClearCartCommand>
{
    public Fin<Unit> Authorize(AddCartLineCommand request, TrustedContext context) => EnsureAuth(context);
    public Fin<Unit> Authorize(SetCartLineQuantityCommand request, TrustedContext context) => EnsureAuth(context);
    public Fin<Unit> Authorize(RemoveCartLineCommand request, TrustedContext context) => EnsureAuth(context);
    public Fin<Unit> Authorize(ClearCartCommand request, TrustedContext context) => EnsureAuth(context);
    private static Fin<Unit> EnsureAuth(TrustedContext ctx) => ctx.IsAuthenticated ? FinSucc(unit) : FinFail<Unit>(Error.New("Unauthorized"));
}

public interface ICartPersistence
{
    IO<Fin<CartSnapshot>> AddLine(Guid cartId, Guid userId, Guid productId, int qty, Money unitPrice);
    IO<Fin<CartSnapshot>> SetLine(Guid cartId, Guid userId, Guid productId, int qty);
    IO<Fin<CartSnapshot>> RemoveLine(Guid cartId, Guid userId, Guid productId);
    IO<Fin<CartSnapshot>> Clear(Guid cartId, Guid userId);
}

public sealed class CartPersistenceImplementation(DatabaseConfig cfg, ILogger<CartPersistenceImplementation> logger) : ICartPersistence
{
    private readonly string _cs = cfg.ConnectionString;
    private readonly ILogger _logger = logger;

    public IO<Fin<CartSnapshot>> AddLine(Guid cartId, Guid userId, Guid productId, int qty, Money unitPrice) =>
        Perform(cartId, userId, conn =>
        {
            EnsureCart(conn, cartId, userId);
            // Try update existing
            using var upd = conn.CreateCommand();
            upd.CommandText = "UPDATE CartLines SET Quantity = Quantity + @Q WHERE CartId=@C AND ProductId=@P";
            upd.Parameters.AddWithValue("@Q", qty);
            upd.Parameters.AddWithValue("@C", cartId.ToString());
            upd.Parameters.AddWithValue("@P", productId.ToString());
            var rows = upd.ExecuteNonQuery();
            if (rows == 0)
            {
                using var ins = conn.CreateCommand();
                ins.CommandText = "INSERT INTO CartLines (CartId, ProductId, Quantity, UnitPrice, Currency) VALUES (@C,@P,@Q,@U,@Cur)";
                ins.Parameters.AddWithValue("@C", cartId.ToString());
                ins.Parameters.AddWithValue("@P", productId.ToString());
                ins.Parameters.AddWithValue("@Q", qty);
                ins.Parameters.AddWithValue("@U", unitPrice.Amount);
                ins.Parameters.AddWithValue("@Cur", unitPrice.Currency);
                ins.ExecuteNonQuery();
            }
        });

    public IO<Fin<CartSnapshot>> SetLine(Guid cartId, Guid userId, Guid productId, int qty) =>
        Perform(cartId, userId, conn =>
        {
            EnsureCart(conn, cartId, userId);
            if (qty <= 0)
            {
                using var del = conn.CreateCommand();
                del.CommandText = "DELETE FROM CartLines WHERE CartId=@C AND ProductId=@P";
                del.Parameters.AddWithValue("@C", cartId.ToString());
                del.Parameters.AddWithValue("@P", productId.ToString());
                del.ExecuteNonQuery();
            }
            else
            {
                using var set = conn.CreateCommand();
                set.CommandText = "UPDATE CartLines SET Quantity=@Q WHERE CartId=@C AND ProductId=@P";
                set.Parameters.AddWithValue("@Q", qty);
                set.Parameters.AddWithValue("@C", cartId.ToString());
                set.Parameters.AddWithValue("@P", productId.ToString());
                var rows = set.ExecuteNonQuery();
                if (rows == 0)
                {
                    using var ins = conn.CreateCommand();
                    ins.CommandText = "INSERT INTO CartLines (CartId, ProductId, Quantity, UnitPrice, Currency) VALUES (@C,@P,@Q,0,'SEK')";
                    ins.Parameters.AddWithValue("@C", cartId.ToString());
                    ins.Parameters.AddWithValue("@P", productId.ToString());
                    ins.Parameters.AddWithValue("@Q", qty);
                    ins.ExecuteNonQuery();
                }
            }
        });

    public IO<Fin<CartSnapshot>> RemoveLine(Guid cartId, Guid userId, Guid productId) =>
        Perform(cartId, userId, conn =>
        {
            using var del = conn.CreateCommand();
            del.CommandText = "DELETE FROM CartLines WHERE CartId=@C AND ProductId=@P";
            del.Parameters.AddWithValue("@C", cartId.ToString());
            del.Parameters.AddWithValue("@P", productId.ToString());
            del.ExecuteNonQuery();
        });

    public IO<Fin<CartSnapshot>> Clear(Guid cartId, Guid userId) =>
        Perform(cartId, userId, conn =>
        {
            using var del = conn.CreateCommand();
            del.CommandText = "DELETE FROM CartLines WHERE CartId=@C";
            del.Parameters.AddWithValue("@C", cartId.ToString());
            del.ExecuteNonQuery();
        });

    private IO<Fin<CartSnapshot>> Perform(Guid cartId, Guid userId, Action<Microsoft.Data.Sqlite.SqliteConnection> mutator) =>
        IO.lift<Fin<CartSnapshot>>(() =>
        {
            try
            {
                using var conn = new Microsoft.Data.Sqlite.SqliteConnection(_cs);
                conn.Open();
                using var tx = conn.BeginTransaction();
                mutator(conn);
                tx.Commit();
                return FetchSnapshot(conn, cartId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cart mutation failed: {CartId}", cartId);
                return Fin<CartSnapshot>.Fail(Error.New(ex));
            }
        });

    private static void EnsureCart(Microsoft.Data.Sqlite.SqliteConnection conn, Guid cartId, Guid userId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO Carts (Id, UserId) SELECT @Id,@U WHERE NOT EXISTS (SELECT 1 FROM Carts WHERE Id=@Id)";
        cmd.Parameters.AddWithValue("@Id", cartId.ToString());
        cmd.Parameters.AddWithValue("@U", userId.ToString());
        cmd.ExecuteNonQuery();
    }

    private static Fin<CartSnapshot> FetchSnapshot(Microsoft.Data.Sqlite.SqliteConnection conn, Guid cartId, Guid userId)
    {
        using var lines = conn.CreateCommand();
        lines.CommandText = "SELECT ProductId, Quantity, UnitPrice, Currency FROM CartLines WHERE CartId=@C";
        lines.Parameters.AddWithValue("@C", cartId.ToString());
        using var rdr = lines.ExecuteReader();
        Map<ProductId, CartLine> map = Map<ProductId, CartLine>();
        while (rdr.Read())
        {
            var pid = new ProductId(Guid.Parse(rdr.GetString(0)));
            var q = rdr.GetInt32(1);
            var price = rdr.GetDecimal(2);
            var cur = rdr.GetString(3);
            map = map.Add(pid, new CartLine(pid, q, new Money(cur, price)));
        }
        var subtotal = map.Values.Fold(0m, (acc, l) => acc + l.UnitPrice.Amount * l.Quantity);
        var currency = map.Values.IsEmpty() ? "SEK" : map.Values.Head().Match(l => l.UnitPrice.Currency, () => "SEK");
        return FinSucc(new CartSnapshot(cartId, userId, map, new Money(currency, subtotal), "SE", "NA"));
    }
}

// Handlers
public sealed class AddCartLineHandler(ICartPersistence carts) : IFunctionalHandler<AddCartLineCommand, CartSnapshot>
{
    public IO<Fin<CartSnapshot>> Handle(AddCartLineCommand request, TrustedContext context, CancellationToken ct) =>
        carts.AddLine(request.CartId, request.UserId, request.ProductId, request.Quantity, request.UnitPrice);
}

public sealed class SetCartLineQuantityHandler(ICartPersistence carts) : IFunctionalHandler<SetCartLineQuantityCommand, CartSnapshot>
{
    public IO<Fin<CartSnapshot>> Handle(SetCartLineQuantityCommand request, TrustedContext context, CancellationToken ct) =>
        carts.SetLine(request.CartId, request.UserId, request.ProductId, request.Quantity);
}

public sealed class RemoveCartLineHandler(ICartPersistence carts) : IFunctionalHandler<RemoveCartLineCommand, CartSnapshot>
{
    public IO<Fin<CartSnapshot>> Handle(RemoveCartLineCommand request, TrustedContext context, CancellationToken ct) =>
        carts.RemoveLine(request.CartId, request.UserId, request.ProductId);
}

public sealed class ClearCartHandler(ICartPersistence carts) : IFunctionalHandler<ClearCartCommand, CartSnapshot>
{
    public IO<Fin<CartSnapshot>> Handle(ClearCartCommand request, TrustedContext context, CancellationToken ct) =>
        carts.Clear(request.CartId, request.UserId);
}
