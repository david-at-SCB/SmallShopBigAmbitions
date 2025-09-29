using LanguageExt;
using Microsoft.Data.Sqlite;
using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.Database;
using static LanguageExt.Prelude;

namespace SmallShopBigAmbitions.Application._Abstractions;

public sealed class InMemoryCartQueries(DatabaseConfig cfg, ILogger<InMemoryCartQueries> logger) : ICartQueries
{
    private readonly string _cs = cfg.ConnectionString;
    private readonly ILogger _logger = logger;

    public IO<Fin<CartSnapshot>> GetCart(Guid cartId) => IO.lift<Fin<CartSnapshot>>(() =>
    {
        try
        {
            using var conn = new SqliteConnection(_cs);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, UserId FROM Carts WHERE Id=@Id LIMIT 1";
            cmd.Parameters.AddWithValue("@Id", cartId.ToString());
            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
                return Fin<CartSnapshot>.Fail(Error.New($"Cart {cartId} not found"));
            var dbCartId = Guid.Parse(reader.GetString(0));
            var userId = Guid.Parse(reader.GetString(1));

            using var linesCmd = conn.CreateCommand();
            linesCmd.CommandText = "SELECT ProductId, Quantity, UnitPrice, Currency FROM CartLines WHERE CartId=@CartId";
            linesCmd.Parameters.AddWithValue("@CartId", dbCartId.ToString());
            using var lineReader = linesCmd.ExecuteReader();
            HashMap<ProductId, CartLine> lines = HashMap<ProductId, CartLine>();
            Option<string> currencyOpt = None;
            while (lineReader.Read())
            {
                var productId = new ProductId(Guid.Parse(lineReader.GetString(0)));
                var qty = lineReader.GetInt32(1);
                var unitPrice = lineReader.GetDecimal(2);
                var currency = lineReader.GetString(3);
                currencyOpt = Some(currency);
                lines = lines.Add(productId, new CartLine(productId, qty, new Money(currency, unitPrice)));
            }
            var cart = new Models.Cart(dbCartId, userId, new CartItems(lines), currencyOpt);
            var subtotal = cart.GetTotal();

            // Raw snapshot, mark invalid initially; factory/enrichment can adjust later.
            var snapshot = new CartSnapshot(
                cart,
                new RegisteredCustomerId(userId),
                subtotal,
                "SE", // TODO: derive country / region
                "NA",
                false,
                []
            );

            // If you have a factory/enricher, call it here (uncomment when available)
            // snapshot = CartSnapshotFactory.Enrich(snapshot);

            return Fin<CartSnapshot>.Succ(snapshot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetCart failed for {CartId}", cartId);
            return Fin<CartSnapshot>.Fail(Error.New(ex));
        }
    });
}

// Minimal RegisteredCustomerId (if not already present elsewhere). Replace with real implementation if duplicated.
file sealed record RegisteredCustomerId(Guid Id) : CustomerId(Id)
{
    public override bool IsRegistered => true;
}
