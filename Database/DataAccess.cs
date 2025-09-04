using LanguageExt;
using Microsoft.Data.Sqlite;
using SmallShopBigAmbitions.Application.Billing.Payments.CreatePaymentIntent;
using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.Monads;
using static LanguageExt.Prelude;

namespace SmallShopBigAmbitions.Database;

// Environment for the Reader: keep DB config here
public sealed record DbEnv(string ConnectionString);

public interface IDataAccess
{
    // Reader-based data access: pass DbEnv at the call site
    Reader<DbEnv, IO<Fin<Option<PaymentIntent>>>> GetPaymentIntentById(Guid id);

    Reader<DbEnv, IO<Fin<Unit>>> UpdatePaymentIntent(PaymentIntent intent);

    Reader<DbEnv, IO<Fin<PaymentIntent>>> InsertPaymentIntent(PaymentIntent intent);

    Reader<DbEnv, Customer> GetCustomerById(Guid userId);

    Reader<DbEnv, Cart> GetCustomerCart(Guid userId);
}

public sealed class DataAccess : IDataAccess
{
    public Reader<DbEnv, IO<Fin<Option<PaymentIntent>>>> GetPaymentIntentById(Guid id) =>
        env => IOFin.From(() =>
        {
            using var conn = new SqliteConnection(env.ConnectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM PaymentIntents WHERE Id=@Id LIMIT 1";
            cmd.Parameters.AddWithValue("@Id", id.ToString());

            using var reader = cmd.ExecuteReader();
            return reader.Read()
                ? Option<PaymentIntent>.Some(ReadIntent(reader))
                : Option<PaymentIntent>.None;
        });

    public Reader<DbEnv, IO<Fin<Unit>>> UpdatePaymentIntent(PaymentIntent intent) =>
        env => IOFin.From(() =>
        {
            using var conn = new SqliteConnection(env.ConnectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"UPDATE PaymentIntents SET
                Provider=@Provider,
                ProviderIntentId=@ProviderIntentId,
                Currency=@Currency,
                Amount=@Amount,
                Status=@Status,
                ClientSecret=@ClientSecret,
                IdempotencyKey=@IdempotencyKey,
                Metadata=@Metadata,
                CreatedAt=@CreatedAt,
                UpdatedAt=@UpdatedAt,
                ExpiresAt=@ExpiresAt,
                ReservationId=@ReservationId
                WHERE Id=@Id";

            AddParameters(cmd, intent);

            var rows = cmd.ExecuteNonQuery();
            if (rows == 0) throw new InvalidOperationException($"PaymentIntent {intent.Id} not found");
            return unit;
        });

    public Reader<DbEnv, IO<Fin<PaymentIntent>>> InsertPaymentIntent(PaymentIntent intent) =>
        env => IOFin.From(() =>
        {
            using var conn = new SqliteConnection(env.ConnectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO PaymentIntents (
                Id, CartId, UserId, Provider, ProviderIntentId, Currency, Amount, Status,
                ClientSecret, IdempotencyKey, Metadata, CreatedAt, UpdatedAt, ExpiresAt, ReservationId)
                VALUES (@Id, @CartId, @UserId, @Provider, @ProviderIntentId, @Currency, @Amount, @Status,
                        @ClientSecret, @IdempotencyKey, @Metadata, @CreatedAt, @UpdatedAt, @ExpiresAt, @ReservationId);";

            AddParameters(cmd, intent);

            cmd.ExecuteNonQuery();
            return intent;
        });

    public Reader<DbEnv, Customer> GetCustomerById(Guid userId) =>
        _ => new Customer(userId, "Unknown", "unknown@example.com");

    public Reader<DbEnv, Cart> GetCustomerCart(Guid userId) =>
        _ => userId == Guid.Empty
            ? throw new Exception("Cannot create a Cart for non-existent customer")
            : Cart.Empty(userId);

    // ----------------- Helpers -----------------

    private static void AddParameters(SqliteCommand cmd, PaymentIntent intent)
    {
        string? ClientSecretOrNull() => intent.ClientSecret.Match<string?>(s => s, () => null);
        string? IdempotencyOrNull() => intent.IdempotencyKey.Match<string?>(s => s, () => null);
        object ExpiresDb() => intent.ExpiresAt.Match<object>(dt => dt.UtcDateTime.ToString("O"), () => DBNull.Value);

        cmd.Parameters.AddWithValue("@Id", intent.Id.ToString());
        cmd.Parameters.AddWithValue("@CartId", intent.CartId.ToString());
        cmd.Parameters.AddWithValue("@UserId", intent.UserId.ToString());
        cmd.Parameters.AddWithValue("@Provider", intent.Provider);
        cmd.Parameters.AddWithValue("@ProviderIntentId", intent.ProviderIntentId);
        cmd.Parameters.AddWithValue("@Currency", intent.Currency);
        cmd.Parameters.AddWithValue("@Amount", intent.Amount);
        cmd.Parameters.AddWithValue("@Status", (int)intent.Status);
        cmd.Parameters.AddWithValue("@ClientSecret", (object?)ClientSecretOrNull() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@IdempotencyKey", (object?)IdempotencyOrNull() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Metadata", SerializeMetadata(intent.Metadata));
        cmd.Parameters.AddWithValue("@CreatedAt", intent.CreatedAt.UtcDateTime.ToString("O"));
        cmd.Parameters.AddWithValue("@UpdatedAt", intent.UpdatedAt.UtcDateTime.ToString("O"));
        cmd.Parameters.AddWithValue("@ExpiresAt", ExpiresDb());
        cmd.Parameters.AddWithValue("@ReservationId", intent.ReservationId.ToString());
    }

    private static string SerializeMetadata(LanguageExt.Map<string, string> map) =>
        string.Join(";", map.AsEnumerable().Select(kv => $"{kv.Key}={kv.Value}"));

    private static PaymentIntent ReadIntent(SqliteDataReader reader)
    {
        Guid ParseGuid(int ordinal) => Guid.Parse(reader.GetString(ordinal));
        string? GetNullableString(int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

        var id = ParseGuid(reader.GetOrdinal("Id"));
        var cartId = ParseGuid(reader.GetOrdinal("CartId"));
        var userId = ParseGuid(reader.GetOrdinal("UserId"));
        var provider = reader.GetString(reader.GetOrdinal("Provider"));
        var providerIntentId = reader.GetString(reader.GetOrdinal("ProviderIntentId"));
        var currency = reader.GetString(reader.GetOrdinal("Currency"));
        var amount = reader.GetDecimal(reader.GetOrdinal("Amount"));
        var status = (PaymentIntentStatus)reader.GetInt32(reader.GetOrdinal("Status"));
        var clientSecret = GetNullableString(reader.GetOrdinal("ClientSecret"));
        var idempotencyKey = GetNullableString(reader.GetOrdinal("IdempotencyKey"));
        var metadataRaw = GetNullableString(reader.GetOrdinal("Metadata")) ?? string.Empty;
        var createdAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("CreatedAt")));
        var updatedAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("UpdatedAt")));
        var expiresAtStr = GetNullableString(reader.GetOrdinal("ExpiresAt"));
        var reservationId = ParseGuid(reader.GetOrdinal("ReservationId"));

        var metaPairs = metadataRaw.Length == 0
            ? Enumerable.Empty<KeyValuePair<string, string>>()
            : metadataRaw.Split(';', StringSplitOptions.RemoveEmptyEntries)
                         .Select(s => s.Split('=', 2))
                         .Where(a => a.Length == 2)
                         .Select(a => new KeyValuePair<string, string>(a[0], a[1]));

        return new PaymentIntent(
            Id: id,
            CartId: cartId,
            UserId: userId,
            Provider: provider,
            ProviderIntentId: providerIntentId,
            Currency: currency,
            Amount: amount,
            Status: status,
            ClientSecret: Optional(clientSecret),
            IdempotencyKey: Optional(idempotencyKey),
            Metadata: metaPairs.ToMap(),
            CreatedAt: createdAt,
            UpdatedAt: updatedAt,
            ExpiresAt: Optional(expiresAtStr)
                .Filter(str => !string.IsNullOrWhiteSpace(str))
                .Map(DateTimeOffset.Parse),
            ReservationId: reservationId
        );
    }
}