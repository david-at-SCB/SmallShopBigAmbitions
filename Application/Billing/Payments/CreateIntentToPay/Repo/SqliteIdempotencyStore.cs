namespace SmallShopBigAmbitions.Application.Billing.Payments.CreateIntentToPay.Repo;

using LanguageExt;
using Microsoft.Data.Sqlite;
using SmallShopBigAmbitions.Application.Billing.Payments.CreateIntentToPay;
using SmallShopBigAmbitions.Monads;
using static LanguageExt.Prelude;

public sealed class SqliteIdempotencyStore : IIdempotencyStore
{
    private readonly string _connectionString;

    public SqliteIdempotencyStore(string connectionString)
    {
        _connectionString = connectionString;
    }

    public IO<Fin<Option<Guid>>> TryGet(string key) => IOFin.From(() =>
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT PaymentIntentId FROM IdempotencyKeys WHERE Key=@Key LIMIT 1";
        cmd.Parameters.AddWithValue("@Key", key);
        var result = cmd.ExecuteScalar() as string;
        if (string.IsNullOrWhiteSpace(result)) return Option<Guid>.None;
        return Option<Guid>.Some(Guid.Parse(result));
    });

    public IO<Fin<Unit>> Put(string key, Guid intentId) => IOFin.From(() =>
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO IdempotencyKeys (Key, PaymentIntentId)
                            VALUES (@Key, @PaymentIntentId);";
        cmd.Parameters.AddWithValue("@Key", key);
        cmd.Parameters.AddWithValue("@PaymentIntentId", intentId.ToString());
        cmd.ExecuteNonQuery();
        return unit;
    });
}
