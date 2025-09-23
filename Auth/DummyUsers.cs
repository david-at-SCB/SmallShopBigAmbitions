namespace SmallShopBigAmbitions.Auth;

using System.Security.Cryptography;
using System.Text;

public record DummyUser(Guid Id, string Email, string DisplayName, string[] Roles, string PasswordHash);

public interface IDummyUserStore
{
    IReadOnlyList<DummyUser> All { get; }
    DummyUser? Get(Guid id);
    DummyUser? GetByEmail(string email);
    bool ValidateCredentials(string email, string password, out DummyUser? user);
}

public sealed class InMemoryDummyUserStore : IDummyUserStore
{
    private static string Hash(string input)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }

    private static readonly List<DummyUser> _users = new()
    {
        // Passwords (dev only): AlicePwd!, BobPwd!, AdminPwd!
        new DummyUser(Guid.Parse("11111111-1111-1111-1111-111111111111"), "alice@example.test", "Alice", new[]{"User"}, Hash("AlicePwd!")),
        new DummyUser(Guid.Parse("22222222-2222-2222-2222-222222222222"), "bob@example.test", "Bob", new[]{"User"}, Hash("BobPwd!")),
        new DummyUser(Guid.Parse("33333333-3333-3333-3333-333333333333"), "admin@example.test", "AdminUser", new[]{"Admin"}, Hash("AdminPwd!"))
    };

    public IReadOnlyList<DummyUser> All => _users;
    public DummyUser? Get(Guid id) => _users.FirstOrDefault(u => u.Id == id);
    public DummyUser? GetByEmail(string email) => _users.FirstOrDefault(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));

    public bool ValidateCredentials(string email, string password, out DummyUser? user)
    {
        user = GetByEmail(email); if (user is null) return false;
        var ok = user.PasswordHash == Hash(password);
        if (!ok) user = null; // hide which part failed
        return ok;
    }
}
