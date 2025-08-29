namespace SmallShopBigAmbitions.Application.Billing.Payments.CreatePaymentIntent.Repo;

using LanguageExt;
using Microsoft.Data.Sqlite;
using SmallShopBigAmbitions.Application.Billing.Payments.CreateIntentToPay;
using SmallShopBigAmbitions.Application.Billing.Payments.CreatePaymentIntent;
using SmallShopBigAmbitions.Monads;   // IOFin helpers
using static LanguageExt.Prelude;

public sealed class PaymentIntentRepository : IPaymentIntentRepository
{
    private readonly string _connectionString;

    public PaymentIntentRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public IO<Fin<PaymentIntent>> Insert(PaymentIntent intent) => IOFin.From(() =>
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();

        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"INSERT INTO PaymentIntents (
                Id, CartId, UserId, Provider, ProviderIntentId, Currency, Amount, Status,
                ClientSecret, IdempotencyKey, Metadata, CreatedAt, UpdatedAt, ExpiresAt, ReservationId)
                VALUES (@Id, @CartId, @UserId, @Provider, @ProviderIntentId, @Currency, @Amount, @Status,
                        @ClientSecret, @IdempotencyKey, @Metadata, @CreatedAt, @UpdatedAt, @ExpiresAt, @ReservationId);";

            var clientSecret = intent.ClientSecret.Match<string?>(s => s, () => null);
            var idempotency = intent.IdempotencyKey.Match<string?>(s => s, () => null);
            object expiresDb = Optional(intent.ExpiresAt);
            var metadataRaw = SerializeMetadata(intent.Metadata);

            cmd.Parameters.AddWithValue("@Id", intent.Id.ToString());
            cmd.Parameters.AddWithValue("@CartId", intent.CartId.ToString());
            cmd.Parameters.AddWithValue("@UserId", intent.UserId.ToString());
            cmd.Parameters.AddWithValue("@Provider", intent.Provider);
            cmd.Parameters.AddWithValue("@ProviderIntentId", intent.ProviderIntentId);
            cmd.Parameters.AddWithValue("@Currency", intent.Currency);
            cmd.Parameters.AddWithValue("@Amount", intent.Amount);
            cmd.Parameters.AddWithValue("@Status", (int)intent.Status);
            cmd.Parameters.AddWithValue("@ClientSecret", clientSecret ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@IdempotencyKey", idempotency ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Metadata", metadataRaw);
            cmd.Parameters.AddWithValue("@CreatedAt", intent.CreatedAt.UtcDateTime.ToString("O"));
            cmd.Parameters.AddWithValue("@UpdatedAt", intent.UpdatedAt.UtcDateTime.ToString("O"));
            cmd.Parameters.AddWithValue("@ExpiresAt", expiresDb);
            cmd.Parameters.AddWithValue("@ReservationId", intent.ReservationId.ToString());

            cmd.ExecuteNonQuery();
        }

        tx.Commit();
        return intent;
    });

    public IO<Fin<Option<PaymentIntent>>> GetById(Guid id) => IOFin.From(() =>
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM PaymentIntents WHERE Id=@Id LIMIT 1";
        cmd.Parameters.AddWithValue("@Id", id.ToString());
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return Option<PaymentIntent>.None;
        return Option<PaymentIntent>.Some(ReadIntent(reader));
    });

    public IO<Fin<Unit>> Update(PaymentIntent intent) => IOFin.From(() =>
    {
        using var conn = new SqliteConnection(_connectionString);
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

        var clientSecret = intent.ClientSecret.Match<string?>(s => s, () => null);
        var idempotency = intent.IdempotencyKey.Match<string?>(s => s, () => null);
        var expiresDb = Optional(intent.ExpiresAt);
        var metadataRaw = SerializeMetadata(intent.Metadata);

        cmd.Parameters.AddWithValue("@Id", intent.Id.ToString());
        cmd.Parameters.AddWithValue("@Provider", intent.Provider);
        cmd.Parameters.AddWithValue("@ProviderIntentId", intent.ProviderIntentId);
        cmd.Parameters.AddWithValue("@Currency", intent.Currency);
        cmd.Parameters.AddWithValue("@Amount", intent.Amount);
        cmd.Parameters.AddWithValue("@Status", (int)intent.Status);
        cmd.Parameters.AddWithValue("@ClientSecret", clientSecret ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@IdempotencyKey", idempotency ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@Metadata", metadataRaw);
        cmd.Parameters.AddWithValue("@CreatedAt", intent.CreatedAt.UtcDateTime.ToString("O"));
        cmd.Parameters.AddWithValue("@UpdatedAt", intent.UpdatedAt.UtcDateTime.ToString("O"));
        cmd.Parameters.AddWithValue("@ExpiresAt", expiresDb);
        cmd.Parameters.AddWithValue("@ReservationId", intent.ReservationId.ToString());

        var rows = cmd.ExecuteNonQuery();
        if (rows == 0) throw new InvalidOperationException($"PaymentIntent {intent.Id} not found");
        return unit;
    });

    public IO<Fin<Option<PaymentIntent>>> GetIdempotent(string idempotencyKey) => IO.lift<Fin<Option<PaymentIntent>>>(() =>
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT PaymentIntentId FROM IdempotencyKeys WHERE Key=@Key LIMIT 1";
        cmd.Parameters.AddWithValue("@Key", idempotencyKey);

        var intentId = cmd.ExecuteScalar() as string;
        if (string.IsNullOrWhiteSpace(intentId))
        {
            return FinSucc(Option<PaymentIntent>.None);
        }

        // This returns Fin<Option<PaymentIntent>>; return directly to avoid double Fin
        return GetById(Guid.Parse(intentId)).Run();
    });

    public IO<Fin<Unit>> SaveIdempotency(string key, Guid intentId) => IOFin.From(() =>
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

    private static string SerializeMetadata(Map<string, string> map)
    {
        var parts = map.Fold(new List<string>(), (acc, k, v) => { acc.Add($"{k}={v}"); return acc; });
        return string.Join(";", parts);
    }

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
            ? []
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